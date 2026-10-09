# Puentes en ProyectoDoly

Las herramientas de puentes viven dentro del add-in ProyectoDoly, en el panel **Puentes** de su pestaña.
El análisis de restricciones está en [sofistik-bridge-modeler-restricciones.md](sofistik-bridge-modeler-restricciones.md).

| Ruta | Contenido |
|---|---|
| `ProyectoDoly/Puentes/Ejes` | Motor del eje sin Revit: rectas, curvas, clotoides, rasante parabólica, placements y variables. |
| `ProyectoDoly/Puentes/LandXml` | Lector de LandXML de Civil 3D (unidades, `pntRef`, avisos de cierre). |
| `ProyectoDoly/Puentes/CreadorEje.cs` | Dibuja el eje 3D en Revit (DirectShape de Modelo genérico con marcas de estación). |
| `ProyectoDoly/Commands/ImportarEjeCmd.cs` y `Views/v_ImportarEje.xaml` | Botón **Importar eje** y su ventana con vista previa. |
| `Puentes.Tests` | Pruebas del motor: `dotnet test Puentes.Tests` (no necesita Revit ni está en la solución). |

Archivo de prueba: `Puentes.Tests/Datos/eje_prueba.xml`.
