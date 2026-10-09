# ProyectoDoly · Puentes en Revit

Add-in de Revit 2024 para modelar puentes a partir de un eje, inspirado en las funciones de SOFiSTiK Bridge Modeler,
con código, familias e iconos propios. El análisis de aplicaciones y restricciones está en
[docs/sofistik-bridge-modeler-restricciones.md](../docs/sofistik-bridge-modeler-restricciones.md).

## Estructura

| Carpeta | Contenido |
|---|---|
| `Doly.Core` | Motor de cálculo sin Revit (netstandard2.0): rectas, curvas, clotoides, rasante con curvas parabólicas, placements, variables y lector LandXML. |
| `Doly.Core.Tests` | Pruebas del motor (xUnit). Se ejecutan sin Revit: `dotnet test Doly.Core.Tests`. |
| `ProyectoDoly` | Add-in de Revit: pestaña **Doly Puentes**, comandos, ventanas WPF e iconos. |

## Pestaña "Doly Puentes"

Los paneles van numerados en el orden en que se modela el puente:

1. **Eje** · *Importar eje*: lee un LandXML de Civil 3D, muestra planta y perfil, y dibuja el eje 3D
   (Modelo genérico) con marcas de estación, en coordenadas compartidas o con el inicio en el origen.
2. Tablero (siguiente paso)
3. Pilas y estribos
4. Planos

## Compilar y probar

1. Abrir `ProyectoDoly/ProyectoDoly.slnx` en Rider o Visual Studio.
2. Elegir la configuración `Debug R24` y compilar: el add-in se copia a la carpeta Addins de Revit 2024.
3. En Revit 2024, abrir un proyecto: pestaña **Doly Puentes > 1 · Eje > Importar eje**.

Archivo de prueba: `Doly.Core.Tests/Datos/eje_prueba.xml` (recta, clotoide, curva R=200, clotoide, recta y rasante con dos curvas verticales).
