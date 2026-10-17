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
    ///     Coloca pilas y estribos en los placements de un eje (como Substructure de SOFiSTiK)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SubestructuraCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Subestructura", "Abra un proyecto de Revit con un eje importado.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                if (ejes.Count == 0)
                {
                    TaskDialog.Show("Subestructura", "No hay ejes en el modelo.\n\nImporte el eje con el botón Importar eje y cree sus placements.");
                    return Result.Cancelled;
                }

                ElementId? seleccionado = uidoc.Selection.GetElementIds().FirstOrDefault(id => ejes.Any(e => e.Id == id));

                paso = "abrir la ventana de subestructura";
                vm_Subestructura viewModel = new vm_Subestructura(doc, ejes, seleccionado);
                v_Subestructura view = new v_Subestructura { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Opciones == null) return Result.Cancelled;

                paso = "colocar la subestructura en el modelo";
                ResultadoSubestructura resultado = CreadorSubestructura.Crear(doc, viewModel.Opciones);
                uidoc.Selection.SetElementIds(resultado.Ids);
                TaskDialog.Show("Subestructura", resultado.Resumen);
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                TaskDialog.Show("Subestructura", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Subestructura", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
