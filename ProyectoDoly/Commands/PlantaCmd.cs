using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProyectoDoly.Puentes;
using ProyectoDoly.Utils;
using ProyectoDoly.ViewModels;
using ProyectoDoly.Views;

namespace ProyectoDoly.Commands
{
    /// <summary>
    ///     Rejillas en los placements y marcas de estacion en planta (como Grids y Axis Stations de SOFiSTiK)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PlantaCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Rejillas y estaciones", "Abra un proyecto de Revit con un eje importado.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                if (ejes.Count == 0)
                {
                    TaskDialog.Show("Rejillas y estaciones", "No hay ejes en el modelo.\n\nImporte el eje con el botón Importar eje.");
                    return Result.Cancelled;
                }

                ElementId? seleccionado = uidoc.Selection.GetElementIds().FirstOrDefault(id => ejes.Any(e => e.Id == id));
                ViewPlan? planta = doc.ActiveView is ViewPlan vista && !vista.IsTemplate ? vista : null;

                paso = "abrir la ventana de rejillas y estaciones";
                vm_Planta viewModel = new vm_Planta(doc, ejes, seleccionado, planta);
                v_Planta view = new v_Planta { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Opciones == null) return Result.Cancelled;

                paso = "crear las rejillas y estaciones";
                ResultadoPlanta resultado = CreadorPlanta.Crear(doc, viewModel.Opciones);
                uidoc.Selection.SetElementIds(resultado.Ids);
                TaskDialog.Show("Rejillas y estaciones", resultado.Resumen);
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                TaskDialog.Show("Rejillas y estaciones", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Rejillas y estaciones", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
