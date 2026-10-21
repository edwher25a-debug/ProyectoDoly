using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Utils;

namespace ProyectoDoly.Commands
{
    /// <summary>
    ///     Invierte el sentido del eje seleccionado (como Invert Axis de SOFiSTiK)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class InvertirEjeCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Invertir eje", "Abra un proyecto de Revit con un eje importado.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                EjeGuardado? guardado = ejes.FirstOrDefault(e => uidoc.Selection.GetElementIds().Contains(e.Id)) ?? (ejes.Count == 1 ? ejes[0] : null);
                if (guardado == null)
                {
                    TaskDialog.Show("Invertir eje", ejes.Count == 0 ? "No hay ejes en el modelo." : "Seleccione el eje que quiere invertir y vuelva a pulsar el botón.");
                    return Result.Cancelled;
                }

                Eje eje = guardado.Eje;
                TaskDialog pregunta = new TaskDialog("Invertir eje")
                {
                    MainInstruction = $"¿Invertir el sentido del eje \"{eje.Nombre}\"?",
                    MainContent = $"El fin pasa a ser el inicio: las estaciones siguen yendo de {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)}, " +
                                  "pero desde el otro extremo. Los placements, la rasante y las variables se trasladan a su nueva estación; " +
                                  "la derecha y la izquierda del eje se intercambian.\n\n" +
                                  "Los tableros, pilas y secciones ya creados no se mueven. Vuelva a usar Cuantificar para actualizar sus estaciones.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (pregunta.Show() != TaskDialogResult.Yes) return Result.Cancelled;

                paso = "invertir y volver a dibujar el eje";
                using (Transaction transaccion = new Transaction(doc, $"Invertir eje {eje.Nombre}"))
                {
                    transaccion.Start();
                    guardado.Eje = InvertirEje.Invertir(eje);
                    CreadorEje.Actualizar(doc, guardado);
                    transaccion.Commit();
                }

                TaskDialog.Show("Invertir eje", $"Eje \"{eje.Nombre}\" invertido. Ahora empieza donde antes terminaba." +
                                                (guardado.Eje.Placements.Count > 0 ? $"\n\nPlacements: {string.Join(", ", guardado.Eje.Placements.Select(p => $"{p.Id} {Eje.FormatoEstacion(p.Estacion)}").Take(10))}" : ""));
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                TaskDialog.Show("Invertir eje", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Invertir eje", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
