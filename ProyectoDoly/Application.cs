using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using ProyectoDoly.Commands;

namespace ProyectoDoly
{
    /// <summary>
    ///     Application entry point
    /// </summary>
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbon(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("ProyectoDoly", $"No se pudo crear la pestaña ProyectoDoly.\n\n{ex}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

        //Cinta solo con la API de Revit: no depende de la version de Nice3point que cargue otro complemento
        private static void CreateRibbon(UIControlledApplication application)
        {
            const string pestana = "ProyectoDoly";
            try
            {
                application.CreateRibbonTab(pestana);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                //La pestaña ya existe
            }

            RibbonPanel panel = application.CreateRibbonPanel(pestana, "MisBotones");

            var boton = new PushButtonData(nameof(PrincipalCmd), "ProyectoDoly",
                Assembly.GetExecutingAssembly().Location, typeof(PrincipalCmd).FullName)
            {
                ToolTip = "Abre la ventana con todas las herramientas",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/RibbonIcon32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/RibbonIcon16.png"))
            };

            panel.AddItem(boton);

            //Puentes: eje desde Civil 3D (siguientes pasos: tablero, pilas y estribos, planos)
            RibbonPanel panelPuentes = application.CreateRibbonPanel(pestana, "Puentes");

            var importarEje = new PushButtonData(nameof(ImportarEjeCmd), "Importar\neje",
                Assembly.GetExecutingAssembly().Location, typeof(ImportarEjeCmd).FullName)
            {
                ToolTip = "Crea el eje del puente a partir de un LandXML exportado desde Civil 3D.",
                LongDescription = "Lee el alineamiento (rectas, curvas y clotoides) y su rasante, muestra una vista previa " +
                                  "y dibuja el eje 3D con marcas de estación en coordenadas compartidas o en el origen del proyecto.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/ImportarEje32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/ImportarEje16.png"))
            };

            panelPuentes.AddItem(importarEje);

            var ejeDesdeLineas = new PushButtonData(nameof(EjeDesdeLineasCmd), "Eje desde\nlíneas",
                Assembly.GetExecutingAssembly().Location, typeof(EjeDesdeLineasCmd).FullName)
            {
                ToolTip = "Crea el eje con rectas y arcos dibujados en Revit o en un DWG, cuando no hay LandXML.",
                LongDescription = "Las líneas se encadenan en cualquier orden y sentido. La rasante se da con cota inicial y pendiente " +
                                  "o con una tabla de PIV. El eje queda igual que uno importado y sirve para todas las herramientas de Puentes.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/EjeDesdeLineas32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/EjeDesdeLineas16.png"))
            };

            panelPuentes.AddItem(ejeDesdeLineas);

            var ejePorTabla = new PushButtonData(nameof(EjePorTablaCmd), "Eje por\ntabla",
                Assembly.GetExecutingAssembly().Location, typeof(EjePorTablaCmd).FullName)
            {
                ToolTip = "Crea el eje elemento por elemento (rectas, clotoides y arcos), con vista previa. Con un eje seleccionado, lo edita.",
                LongDescription = "Como el cuadro Axis Data de SOFiSTiK: origen, dirección y estación inicial, y pestañas Horizontal, " +
                                  "Vertical (PIV) y Placements. También convierte una tabla de PI con radios y clotoides en elementos.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/EjePorTabla32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/EjePorTabla16.png"))
            };

            panelPuentes.AddItem(ejePorTabla);

            var placements = new PushButtonData(nameof(PlacementsCmd), "Place-\nments",
                Assembly.GetExecutingAssembly().Location, typeof(PlacementsCmd).FullName)
            {
                ToolTip = "Agrega puntos con nombre a lo largo del eje (ejes de apoyo, juntas, inicio y fin de tablero).",
                LongDescription = "Cada placement tiene ID, estación y giro respecto a la perpendicular al eje. Se pueden agregar uno a uno " +
                                  "o cada cierta distancia, y se dibujan en el eje. Las pilas, estribos y vanos se colocan en ellos.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Placements32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Placements16.png"))
            };

            panelPuentes.AddItem(placements);

            var ejeSecundario = new PushButtonData(nameof(EjeSecundarioCmd), "Eje\nsecundario",
                Assembly.GetExecutingAssembly().Location, typeof(EjeSecundarioCmd).FullName)
            {
                ToolTip = "Crea un eje desplazado del principal: bordes de tablero, ejes de vigas o ramales paralelos.",
                LongDescription = "Desplazamiento a la derecha constante o variable entre estaciones, y en altura respecto a la rasante. " +
                                  "El nuevo eje lleva los placements del principal y sirve para todas las herramientas de Puentes.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/EjeSecundario32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/EjeSecundario16.png"))
            };

            panelPuentes.AddItem(ejeSecundario);

            var invertirEje = new PushButtonData(nameof(InvertirEjeCmd), "Invertir\neje",
                Assembly.GetExecutingAssembly().Location, typeof(InvertirEjeCmd).FullName)
            {
                ToolTip = "Invierte el sentido del eje seleccionado: el fin pasa a ser el inicio.",
                LongDescription = "Las estaciones conservan su rango; placements, rasante y variables se trasladan a su nueva estación.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/InvertirEje32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/InvertirEje16.png"))
            };

            panelPuentes.AddItem(invertirEje);

            var superestructura = new PushButtonData(nameof(SuperestructuraCmd), "Super-\nestructura",
                Assembly.GetExecutingAssembly().Location, typeof(SuperestructuraCmd).FullName)
            {
                ToolTip = "Crea el tablero barriendo una sección transversal a lo largo del eje.",
                LongDescription = "La sección sale de una familia de Revit (Modelo genérico o Perfil), por ejemplo las secciones de SOFiSTiK. " +
                                  "Se elige el tipo, el tramo de estaciones y el paso; el tablero sigue la planta y la rasante del eje.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Superestructura32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Superestructura16.png"))
            };

            panelPuentes.AddItem(superestructura);

            var subestructura = new PushButtonData(nameof(SubestructuraCmd), "Sub-\nestructura",
                Assembly.GetExecutingAssembly().Location, typeof(SubestructuraCmd).FullName)
            {
                ToolTip = "Coloca pilas y estribos en los placements del eje.",
                LongDescription = "La familia (Modelo genérico adaptativo con un punto, como las de SOFiSTiK) se inserta por su punto adaptativo " +
                                  "en el punto del eje de cada placement, orientada con el eje y su giro, con desplazamientos y altura hasta la fundación.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Subestructura32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Subestructura16.png"))
            };

            panelPuentes.AddItem(subestructura);

            var secciones = new PushButtonData(nameof(SeccionesCmd), "Secciones",
                Assembly.GetExecutingAssembly().Location, typeof(SeccionesCmd).FullName)
            {
                ToolTip = "Crea vistas de sección transversal en los placements y estaciones del eje.",
                LongDescription = "Una vista por placement, entre placements cada cierta distancia o en estaciones sueltas, " +
                                  "mirando en el sentido del eje y giradas con el placement si se desea.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Secciones32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Secciones16.png"))
            };

            panelPuentes.AddItem(secciones);

            var planta = new PushButtonData(nameof(PlantaCmd), "Rejillas y\nestaciones",
                Assembly.GetExecutingAssembly().Location, typeof(PlantaCmd).FullName)
            {
                ToolTip = "Crea rejillas en los placements y marca las estaciones del eje en la planta.",
                LongDescription = "Rejillas transversales al eje con el nombre del placement (y entre placements si se desea), " +
                                  "y rayas con el texto de la estación cada cierta distancia en la vista de planta activa.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Planta32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Planta16.png"))
            };

            panelPuentes.AddItem(planta);

            var cuantificar = new PushButtonData(nameof(CuantificarCmd), "Cuantificar",
                Assembly.GetExecutingAssembly().Location, typeof(CuantificarCmd).FullName)
            {
                ToolTip = "Escribe estaciones, placement, desfase y volumen de los elementos del puente en parámetros compartidos.",
                LongDescription = "Proyecta cada elemento sobre el eje y llena los parámetros DOLY (Eje, Placement, Estaciones, Estación inicio y fin, " +
                                  "Longitud en eje, Desfase, Volumen) para usarlos en tablas de planificación.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Cuantificar32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/Cuantificar16.png"))
            };

            panelPuentes.AddItem(cuantificar);

            var exportarEje = new PushButtonData(nameof(ExportarEjeCmd), "Exportar\neje",
                Assembly.GetExecutingAssembly().Location, typeof(ExportarEjeCmd).FullName)
            {
                ToolTip = "Guarda el eje seleccionado en JSON (planta, rasante, placements y variables).",
                LongDescription = "Formato abierto y documentado para llevar el eje a otro proyecto u otro programa. " +
                                  "Se vuelve a leer con Importar eje, eligiendo el archivo .json.",
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/ExportarEje32.png")),
                Image = new BitmapImage(new Uri("pack://application:,,,/ProyectoDoly;component/Resources/Icons/ExportarEje16.png"))
            };

            panelPuentes.AddItem(exportarEje);
        }
    }
}
