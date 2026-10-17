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
    ///     Crea vistas de seccion en los placements y estaciones de un eje (como Sections de SOFiSTiK)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SeccionesCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Secciones", "Abra un proyecto de Revit con un eje importado.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                if (ejes.Count == 0)
                {
                    TaskDialog.Show("Secciones", "No hay ejes en el modelo.\n\nImporte el eje con el botón Importar eje.");
                    return Result.Cancelled;
                }

                ElementId? seleccionado = uidoc.Selection.GetElementIds().FirstOrDefault(id => ejes.Any(e => e.Id == id));

                paso = "abrir la ventana de secciones";
                vm_Secciones viewModel = new vm_Secciones(doc, ejes, seleccionado);
                v_Secciones view = new v_Secciones { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Opciones == null) return Result.Cancelled;

                paso = "crear las vistas de sección";
                List<ElementId> vistas = CreadorSecciones.Crear(doc, viewModel.Opciones);
                if (vistas.Count > 0 && doc.GetElement(vistas[0]) is View primera) uidoc.ActiveView = primera;

                List<string> nombres = vistas.Select(id => doc.GetElement(id)?.Name ?? "").ToList();
                TaskDialog.Show("Secciones", $"{vistas.Count} vistas de sección creadas en el Navegador de proyectos (Secciones):\n\n" +
                                             string.Join("\n", nombres.Take(15)) + (nombres.Count > 15 ? $"\n(y {nombres.Count - 15} más)" : ""));
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                TaskDialog.Show("Secciones", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Secciones", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
