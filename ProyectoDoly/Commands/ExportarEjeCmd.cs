using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Utils;

namespace ProyectoDoly.Commands
{
    /// <summary>
    ///     Guarda un eje del modelo (con rasante, placements y variables) en JSON abierto, para otro proyecto u otro programa
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportarEjeCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Exportar eje", "Abra un proyecto de Revit con un eje importado.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                if (ejes.Count == 0)
                {
                    TaskDialog.Show("Exportar eje", "No hay ejes en el modelo.");
                    return Result.Cancelled;
                }

                EjeGuardado? eje = ejes.FirstOrDefault(e => uidoc.Selection.GetElementIds().Contains(e.Id)) ?? (ejes.Count == 1 ? ejes[0] : null);
                if (eje == null)
                {
                    TaskDialog.Show("Exportar eje", "Hay varios ejes en el modelo. Seleccione el que quiere exportar y vuelva a pulsar el botón.\n\n" +
                                                    string.Join("\n", ejes.Select(e => e.Eje.Nombre)));
                    return Result.Cancelled;
                }

                SaveFileDialog dialogo = new SaveFileDialog
                {
                    Title = $"Exportar el eje {eje.Eje.Nombre}",
                    Filter = "Eje en JSON (*.json)|*.json",
                    FileName = string.Concat(eje.Eje.Nombre.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) + ".json"
                };
                if (dialogo.ShowDialog() != true) return Result.Cancelled;

                paso = "escribir el archivo";
                File.WriteAllText(dialogo.FileName, JsonEje.Escribir(eje.Eje), new System.Text.UTF8Encoding(false));
                TaskDialog.Show("Exportar eje", $"Eje \"{eje.Eje.Nombre}\" guardado en\n{dialogo.FileName}\n\n" +
                                                $"{eje.Eje.Horizontal.Elementos.Count} tramos, {(eje.Eje.TieneRasante ? eje.Eje.Vertical.Puntos.Count + " PIV" : "sin rasante")}, " +
                                                $"{eje.Eje.Placements.Count} placements. Se vuelve a importar con el botón Importar eje.");
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TaskDialog.Show("Exportar eje", $"No se pudo escribir el archivo: {ex.Message}");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Exportar eje", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
