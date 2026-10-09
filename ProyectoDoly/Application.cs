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
        }
    }
}
