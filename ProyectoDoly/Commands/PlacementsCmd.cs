using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Utils;
using ProyectoDoly.ViewModels;
using ProyectoDoly.Views;

namespace ProyectoDoly.Commands
{
    /// <summary>
    ///     Agrega, edita y quita los placements de un eje importado (como "Add Placements" de SOFiSTiK)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PlacementsCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Placements", "Abra un proyecto de Revit con un eje importado.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                if (ejes.Count == 0)
                {
                    TaskDialog.Show("Placements", "No hay ejes en el modelo.\n\nImporte el eje con el botón Importar eje.");
                    return Result.Cancelled;
                }

                ElementId? seleccionado = uidoc.Selection.GetElementIds().FirstOrDefault(id => ejes.Any(e => e.Id == id));

                paso = "abrir la ventana de placements";
                vm_Placements viewModel = new vm_Placements(ejes, seleccionado);
                v_Placements view = new v_Placements { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Resultado == null || viewModel.EjeSelecc == null) return Result.Cancelled;

                paso = "guardar los placements en el eje";
                EjeGuardado eje = viewModel.EjeSelecc;
                using (Transaction transaccion = new Transaction(doc, $"Placements {eje.Eje.Nombre}"))
                {
                    transaccion.Start();
                    eje.Eje.Placements.Clear();
                    eje.Eje.Placements.AddRange(viewModel.Resultado);
                    CreadorEje.Actualizar(doc, eje);
                    transaccion.Commit();
                }

                uidoc.Selection.SetElementIds(new List<ElementId> { eje.Id });
                TaskDialog.Show("Placements",
                    $"Eje \"{eje.Eje.Nombre}\": {eje.Eje.Placements.Count} placements guardados.\n\n" +
                    string.Join("\n", eje.Eje.Placements.Take(12).Select(p => $"{p.Id}   {Eje.FormatoEstacion(p.Estacion)}" + (Math.Abs(p.Rotacion) > 1e-9 ? $"   giro {p.Rotacion:0.##}°" : ""))) +
                    (eje.Eje.Placements.Count > 12 ? $"\n(y {eje.Eje.Placements.Count - 12} más)" : ""));
                return Result.Succeeded;
            }
            catch (InvalidOperationException ex)
            {
                TaskDialog.Show("Placements", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Placements", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
