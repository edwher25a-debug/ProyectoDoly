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

La sección se lee de la familia así: si tiene sólidos, se toma la cara plana del lado donde la familia es más delgada
(una extrusión fina dibujada de frente); si no, las líneas cerradas. El origen de la familia es el punto del eje sobre la rasante,
X hacia la derecha mirando en el sentido del eje y Z hacia arriba. *Reflejar* invierte izquierda y derecha.
