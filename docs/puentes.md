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

La sección se lee de la familia así: si tiene sólidos, se toma la cara plana del lado donde la familia es más delgada
(una extrusión fina dibujada de frente); si no, las líneas cerradas. El punto de inserción (punto adaptativo 1, u origen) es el punto del eje sobre la rasante,
X hacia la derecha mirando en el sentido del eje y Z hacia arriba. *Reflejar* invierte izquierda y derecha.
