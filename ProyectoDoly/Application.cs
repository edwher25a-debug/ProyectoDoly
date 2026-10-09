using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using ProyectoDoly.Commands;

namespace ProyectoDoly
{
    /// <summary>
    ///     Punto de entrada: crea la pestaña "Doly Puentes" con un panel por paso del flujo de trabajo
    /// </summary>
    public class Application : IExternalApplication
    {
        public const string Pestana = "Doly Puentes";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbon(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("ProyectoDoly", $"No se pudo crear la pestaña {Pestana}.\n\n{ex}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

        //Cinta solo con la API de Revit: no depende de la version de Nice3point que cargue otro complemento
        private static void CreateRibbon(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(Pestana);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                //La pestaña ya existe
            }

            //Paneles numerados en el orden en que se modela el puente
            RibbonPanel panelEje = application.CreateRibbonPanel(Pestana, "1 · Eje");

            var importarEje = new PushButtonData(nameof(ImportarEjeCmd), "Importar\neje",
                Assembly.GetExecutingAssembly().Location, typeof(ImportarEjeCmd).FullName)
            {
                ToolTip = "Crea el eje del puente a partir de un LandXML exportado desde Civil 3D.",
                LongDescription = "Lee el alineamiento (rectas, curvas y clotoides) y su rasante, muestra una vista previa " +
                                  "y dibuja el eje 3D con marcas de estación en coordenadas compartidas o en el origen del proyecto.",
                LargeImage = Icono("ImportarEje32.png"),
                Image = Icono("ImportarEje16.png")
            };

            panelEje.AddItem(importarEje);
        }

        private static BitmapImage Icono(string archivo) =>
            new BitmapImage(new Uri($"pack://application:,,,/ProyectoDoly;component/Resources/Icons/{archivo}"));
    }
}
