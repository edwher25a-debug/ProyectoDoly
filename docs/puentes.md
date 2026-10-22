# Puentes en ProyectoDoly

Las herramientas de puentes viven dentro del add-in ProyectoDoly, en el panel **Puentes** de su pestaña.
El análisis de restricciones está en [sofistik-bridge-modeler-restricciones.md](sofistik-bridge-modeler-restricciones.md).

| Ruta | Contenido |
|---|---|
| `ProyectoDoly/Puentes/Ejes` | Motor del eje sin Revit: rectas, curvas, clotoides, rasante parabólica, placements y variables. |
| `ProyectoDoly/Puentes/LandXml` | Lector de LandXML de Civil 3D (unidades, `pntRef`, avisos de cierre). |
| `ProyectoDoly/Puentes/CreadorEje.cs` | Dibuja el eje 3D en Revit (DirectShape de Modelo genérico con marcas de estación). |
| `ProyectoDoly/Commands/ImportarEjeCmd.cs` y `Views/v_ImportarEje.xaml` | Botón **Importar eje** y su ventana con vista previa. |
| `ProyectoDoly/Puentes/DatosEje.cs` | Guarda el eje dentro del elemento dibujado (Extensible Storage) para que las demás herramientas lo usen. |
| `ProyectoDoly/Puentes/Superestructura` | Sección transversal (contornos, huecos) y barrido a lo largo del eje, sin Revit. |
| `ProyectoDoly/Puentes/SeccionFamilia.cs` | Lee la sección de una familia (Modelo genérico o Perfil) para el tipo elegido. |
| `ProyectoDoly/Commands/SuperestructuraCmd.cs` y `Views/v_Superestructura.xaml` | Botón **Superestructura**: crea el tablero (categoría Tableros de puente). |
| `Puentes.Tests` | Pruebas del motor: `dotnet test Puentes.Tests` (no necesita Revit ni está en la solución). |

Archivo de prueba: `Puentes.Tests/Datos/eje_prueba.xml`.

## Placements

Botón **Placements**: puntos con nombre a lo largo del eje (como "Add Placements" de SOFiSTiK), con ID, estación y giro
del plano respecto a la perpendicular al eje (0 = apoyo recto). Se agregan uno a uno o cada cierta distancia
(`Puentes/Ejes/PlacementsEje.cs`), se guardan con el eje (líneas `L|` del serializador) y se dibujan en él: una línea de
12 m en su plano y un poste vertical de 3 m. Son la referencia para colocar pilas y estribos y cortar el tablero en vanos.

## Exportar e importar el eje en JSON

Botón **Exportar eje**: guarda el eje seleccionado (o el único del modelo) en un JSON abierto con la planta (tramos), la rasante (PIV), los placements y las variables. **Importar eje** también lee ese archivo .json.
El formato está documentado en `Puentes/Ejes/JsonEje.cs`: metros, rumbos en radianes desde el Este en sentido antihorario y curvatura positiva hacia la izquierda.

## Invertir eje

Botón **Invertir eje**: el fin del eje seleccionado pasa a ser el inicio. El rango de estaciones no cambia, y los placements, la rasante y las variables se trasladan a su nueva estación. El plano de cada placement queda igual. Los elementos ya creados no se mueven; para actualizar sus estaciones, vuelva a usar Cuantificar. Se puede deshacer con Ctrl+Z.

## Eje secundario

Botón **Eje secundario**, como los ejes secundarios de SOFiSTiK: crea un eje nuevo desplazado del principal, por ejemplo un borde de tablero, el eje de una viga o un ramal paralelo.

- El desplazamiento a la derecha puede ser constante, o variable con una tabla "estación = desplazamiento" que interpola en línea recta entre los puntos. También hay un desplazamiento en altura respecto a la rasante.
- Si el desplazamiento es constante, las rectas y los arcos salen exactos: una recta paralela y un arco de radio R ± d. Las clotoides y los desplazamientos variables se aproximan con clotoides de hasta 10 m, continuas en posición y rumbo.
- El eje secundario tiene su propio estacionamiento, que es su longitud, y empieza en la estación inicial del principal. Los placements, las variables y los PIV de la rasante pasan a su estación equivalente.
- Es un eje como cualquier otro, así que sirve en Superestructura, Subestructura, Secciones, Rejillas y Cuantificar.

Código en `Puentes/Ejes/EjeSecundario.cs`.

## Subestructura

Botón **Subestructura** (como Substructure de SOFiSTiK): coloca una familia de pila, cimentación o estribo en los placements elegidos.
Acepta cualquier familia cargable de modelo y la coloca con el método que pide su tipo de colocación:

| Familia | Cómo se coloca |
|---|---|
| Adaptativa con **un** punto | El punto va al eje en el placement. Ejes: X en el sentido del eje, Y a la izquierda, Z arriba. |
| Basada en nivel o plano de trabajo (Cimentación estructural, Modelo genérico...) | Por su origen, en el nivel más cercano por debajo y llevada a la cota del placement; girada con el eje. Las familias anidadas compartidas (pilas de Pilares estructurales, dados y pilotes de Cimentación estructural) vienen dentro. |
| Pilar estructural (dos niveles) | Desde la cota de fundación hasta el placement, como elemento estructural. |
| Armazón estructural u otra basada en línea (estribos) | Por una línea en el plano del placement (sigue el esviaje), centrada en el punto de inserción, con la longitud indicada. |

Desplazamientos a la derecha y hacia arriba en el plano del placement; con *Siempre vertical* desactivado una adaptativa se
inclina con la rasante. Opcional: un parámetro de longitud de ejemplar recibe la altura desde una cota de fundación hasta el
punto de inserción. **Cuantificar** escribe también los parámetros en cada familia anidada compartida, para que pilas, dados
y pilotes salgan en las tablas. Cálculo en `Puentes/Subestructura/UbicacionSubestructura.cs`.

### Vigas y diafragmas

- **Vigas**, como el Girder Layout de SOFiSTiK: cree un eje secundario por cada viga (por ejemplo a -4.5, -1.5, 1.5 y 4.5 m). Después, en Superestructura, barra sobre cada uno la familia de la viga con Componente "Viga".
- **Diafragmas**, como el Cross Member Array de SOFiSTiK: en Subestructura escriba varios desplazamientos a la derecha separados por punto y coma (-3; 0; 3). En cada placement se coloca una fila de ejemplares, uno por desplazamiento.

## Secciones

Botón **Secciones**: crea vistas de sección transversal del eje (como *Sections* de SOFiSTiK).

- Una sección en cada placement elegido, girada con el placement si se marca (apoyos esviados).
- Secciones entre placements consecutivos: cada intervalo se divide en partes iguales que no pasan la separación dada. Se llaman "P1-P2 1", "P1-P2 2"...
- Estaciones sueltas separadas por punto y coma (0+050; 0+125.5).
- Cada vista mira en el sentido del eje, con la derecha del eje a la derecha, y se recorta con el ancho, la altura sobre y bajo la rasante y la profundidad indicados.
- Las vistas se llaman "Sección {eje} {nombre} ({estación})" y quedan en el Navegador de proyectos.

## Rejillas y estaciones

Botón **Rejillas y estaciones** (como *Grids* y *Axis Stations* de SOFiSTiK):

- Rejillas transversales al eje en los placements elegidos, con el ID del placement como nombre (y un prefijo opcional). Se pueden añadir también entre placements a una separación máxima y girarlas con el placement. La burbuja queda a la izquierda del eje.
- Marcas de estación en la vista de planta activa: una línea de detalle transversal cada N m, con el texto de la estación a su izquierda, que siempre se lee derecho. Opcionalmente se marcan también el inicio y el fin del eje.

## Cuantificar

Botón **Cuantificar** (como *Quantification* de SOFiSTiK): escribe en parámetros compartidos de ejemplar (grupo Datos) los datos de cada elemento respecto al eje.
Aplica a los elementos seleccionados (los grupos cuentan por sus miembros) o a todo lo creado con Puentes en el eje.

- **DOLY Eje**, **DOLY Placement**: el ID si el elemento está en un placement, o "P1-P2" si ocupa ese vano.
- **DOLY Estaciones** (texto), **DOLY Estación inicio**, **DOLY Estación fin**, **DOLY Longitud en eje**.
- **DOLY Desfase**: el centro del elemento respecto al eje, positivo a la derecha.
- **DOLY Volumen**: el de sus sólidos.

Las estaciones salen de proyectar la geometría sobre el eje (`Puentes/Cuantificacion/ProyectorEje.cs`). Los parámetros tienen GUID fijos y se guardan en `%APPDATA%\ProyectoDoly\ParametrosPuente.txt`, así que las tablas sirven en cualquier proyecto. Vuelva a cuantificar si mueve el eje o los elementos.

## Superestructura

1. Importar el eje (los ejes importados antes de esta versión deben importarse de nuevo: ahora el eje queda guardado en el modelo).
2. Superestructura: elegir el eje, la familia de sección y su tipo (o *Cargar familia…*), el tramo de estaciones y el paso.
3. *Parámetros a lo largo del tablero* (como las variables de SOFiSTiK): la tabla muestra los parámetros numéricos de la
   familia (longitud, ángulo o número). Para variar uno se escriben sus valores por estación, `0+000 = 1.20; 0+040 = 1.80`;
   entre estaciones se interpola en línea recta. La familia se evalúa en cada estación del barrido (`Puentes/LectorSeccion.cs`,
   sin modificar la familia del proyecto) y las secciones se alinean vértice a vértice (`SeccionVariable.Alinear`), por lo que
   la forma debe conservar el mismo número de vértices. *Ver sección en la estación* muestra la sección en cualquier punto.
4. *Cómo se crea*: **familia adaptativa** (por defecto, como SOFiSTiK) o **sólido directo**. La familia adaptativa
   (`Puentes/FamiliaTablero.cs`) se genera desde la plantilla "Modelo genérico adaptativo": sus puntos adaptativos son los
   vértices de la sección al inicio y al final de un tramo, unidos por una solevación sólida (los huecos, vacías), con
   parámetro de material. Se coloca un ejemplar por tramo y todos quedan en un grupo.
5. El tablero se crea como un sólido con secciones verticales, perpendiculares al eje en planta y a la cota de la rasante.

El eje se dibuja continuo: una línea por cada recta con pendiente constante y una curva suave (spline con las tangentes
del eje) por cada curva, clotoide o curva vertical, sin partirlo cada pocos metros (`Puentes/Ejes/TramosEje.cs`).

**Inserción de la sección:** como en los perfiles de SOFiSTiK (Modelo genérico adaptativo), la sección se ubica en el eje
por el **punto adaptativo 1** de la familia, no por su origen. Si la familia no es adaptativa, se usa su origen.
Una sección dibujada en el plano Centro (izquierda/derecha) se mira con X en el sentido del eje (derecha = -Y).

**Tramo entre placements** (From/To de SOFiSTiK): el tablero va de un placement a otro; con *Un tablero por vano*
(como el Repeater) se crea uno entre cada par de placements consecutivos, con una junta opcional repartida a cada lado.
Cada tablero se llama "Tablero <eje> <P1-P2>".

La sección se lee de la familia así: si tiene sólidos, se toma la cara plana del lado donde la familia es más delgada
(una extrusión fina dibujada de frente); si no, las líneas cerradas. El punto de inserción (punto adaptativo 1, u origen) es el punto del eje sobre la rasante,
X hacia la derecha mirando en el sentido del eje y Z hacia arriba. *Reflejar* invierte izquierda y derecha.

### Parapetos, barandas y otros componentes

En el paso 4 de Superestructura, **Componente** elige qué se barre: Tablero, Parapeto, Baranda, Bordillo o Viga. El nombre se usa en el elemento, el grupo y los comentarios, por ejemplo "Parapeto Eje1 P1-P2 der".
Con **A ambos lados del eje**, como los parapetos de SOFiSTiK, se crean dos: el de la derecha con el desplazamiento lateral dado, y el de la izquierda con la sección reflejada y el desplazamiento contrario.
La familia del parapeto se dibuja como la del tablero (Modelo genérico adaptativo, lazo en el plano Centro izquierda/derecha), con su punto adaptativo en el punto de apoyo sobre el tablero. Sube el parapeto con "Subir" hasta la cota del borde.
