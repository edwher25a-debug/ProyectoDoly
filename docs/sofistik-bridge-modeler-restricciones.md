# SOFiSTiK Bridge + Infrastructure Modeler: aplicaciones y restricciones para replicarlo

Fuente: documentación oficial 2024 (https://docs.sofistik.com/2024/en/bridge_modeler/index.html), revisada el 2026-10-09.
Lo marcado como *(inferido)* no aparece en la documentación; es mi lectura técnica.

## 1. Qué es

Un add-in para **Autodesk Revit (2019 o superior)** que modela puentes a partir de un **eje** (alineamiento).
Las secciones transversales son **familias de perfil adaptativas y paramétricas** que se barren a lo largo del eje.
Sus parámetros pueden variar por estación.

## 2. Aplicaciones, ordenadas por importancia

| # | Aplicación | Qué hace | Importancia para la réplica |
|---|---|---|---|
| 1 | **Eje (Axis)** | Alineamiento horizontal y vertical, estacionamiento, *placements* (puntos con ID y estación), *variables* (valores que cambian a lo largo del eje) y ejes secundarios desplazados U/V. Importa LandXML, TRA/GRA, CSV/XYZ, JSON y CDB. | **Núcleo.** Todo lo demás depende de esto. |
| 2 | **Superestructura (Superstructure)** | Barre una familia de perfil (T, doble T, cajón, losa, parapeto) a lo largo del eje y crea un sólido 3D. Los parámetros del perfil pueden ligarse a variables o ejes secundarios. Tiene modos de rotación en *placements*, un "Repeater" entre *placements* y parámetros compartidos (estación inicio/fin, volumen). | **Núcleo.** Es la aplicación más representativa. |
| 3 | **Subestructura (Substructure)** | Pilas y estribos (familias genéricas adaptativas) colocados en un *placement* del eje. | Alta |
| 4 | **Planos (Shop Drawings)** | Secciones en estaciones, rejillas, vista en planta, sección desplegada, cotas de eje y borde, estaciones, cotas de elevación y medición 3D. | Alta: es lo que se entrega en obra. |
| 5 | Vigas, Girder Layout, Cross Member Array | Puentes de vigas múltiples y diafragmas. | Media |
| 6 | Parapetos, barandas, terreno | Elementos complementarios a lo largo del eje. | Media/Baja |
| 7 | Armadura (Rebar/Shape) y Pretensado (Tendon) | Barras y cables que siguen la geometría del eje. | Baja al inicio: es lo más complejo. |
| 8 | Transferencia (Import/Export) | Formatos propios .pck, .sbim y JSON, y un "Analysis Link" hacia SOFiPLUS para el cálculo. | Media: conviene reemplazarlo por formatos abiertos. |
| 9 | Cuantificación, Infra Browser, Dynamo | Cómputos, árbol de elementos y nodos de Dynamo. | Media |

## 3. Restricciones

### 3.1 Legales (las más importantes)
- **Es software propietario de SOFiSTiK AG.** No se puede copiar su código, descompilar sus DLL ni redistribuir sus familias (.rfa), su contenido suministrado ni sus textos. El EULA de este tipo de software normalmente prohíbe la ingeniería inversa *(inferido: no revisé el EULA concreto)*.
- **Sí se pueden replicar las funciones y los conceptos** (eje, *placements*, variables, barrido de perfil) con código y familias propios. Las ideas y los flujos de trabajo no están protegidos por derecho de autor.
- **Nombres y marcas:** no usar "SOFiSTiK", "Infra Browser", "SBIM" ni sus íconos. La herramienta necesita nombre propio.
- **Patentes:** no encontré indicios, pero no hice una búsqueda formal *(inferido)*.

### 3.2 Formatos de datos
- **Abiertos y utilizables:** LandXML (alineamientos de Civil 3D), CSV/XYZ e IFC 4.3 (IfcAlignment, el estándar abierto para infraestructura; SOFiSTiK no lo lista en este flujo).
- **TRA/GRA** (formato alemán de alineamientos) está documentado públicamente en parte; es factible, pero con menor prioridad *(inferido)*.
- **Cerrados, que conviene evitar:** CDB (base de datos de SOFiSTiK; requiere su instalación y licencia), .pck, .sbim y el "Analysis Link" hacia SOFiPLUS. **Sin SOFiSTiK no hay enlace directo a su cálculo estructural.** La alternativa es exportar a IFC, JSON propio o directamente a otro programa de análisis.

### 3.3 Técnicas
- **Plataforma:** add-in de Revit en **C# con la Revit API**. Revit 2025 y posteriores usan .NET 8; las versiones anteriores, .NET Framework 4.8. Hace falta **Revit instalado y licenciado** para desarrollar y probar. Este entorno en la nube no puede ejecutar Revit; las pruebas tienen que hacerse en tu equipo.
- **Matemática del eje:** clotoides (integrales de Fresnel), curvas verticales parabólicas, peralte y estacionamiento por proyección o por longitud de arco. La documentación **no publica los algoritmos**, así que hay que implementarlos desde la teoría de trazado vial. Es conocido y factible.
- **Barrido de sección variable:** la Revit API no ofrece un "barrido con parámetros variables" directo. Hay dos caminos *(inferido)*:
  1. **Componentes adaptativos** (lo que hace SOFiSTiK): perfiles adaptativos colocados en cada estación y *loft* entre ellos.
  2. **DirectShape** con `GeometryCreationUtilities` (loft o swept blend por tramos). Es más simple y rápido, pero menos editable en Revit.
- **Precisión:** LandXML tiene 8 decimales y Revit trabaja internamente en pies; hay que convertir unidades y controlar tolerancias.
- **Regeneración:** al cambiar el eje hay que regenerar todo lo que depende de él. Eso exige guardar las definiciones (por ejemplo, en Extensible Storage) y un mecanismo de "refresh".
- **Armadura y pretensado en 3D curvo** son lo más difícil de la API de Revit (formas de barra libres). Conviene dejarlos para el final.
- **Alternativa de prototipo rápido:** Dynamo (visual o Python) para validar la geometría antes de escribir el add-in en C#.

### 3.4 De alcance
- El producto completo representa años de desarrollo. Una **copia personalizada razonable** se centra en las aplicaciones 1 a 4 y deja armadura, pretensado y el enlace a cálculo para fases posteriores.
