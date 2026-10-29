using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProyectoDoly.Puentes;
using ProyectoDoly.Utils;

namespace ProyectoDoly.Commands
{
    /// <summary>
    ///     Vuelve a ajustar al eje seleccionado todo lo que depende de el (ejes secundarios, tableros, pilas), por ejemplo
    ///     despues de cambiarlo con otra herramienta
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ActualizarEjeCmd : IExternalCommand
    {
        private const string Titulo = "Actualizar desde el eje";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show(Titulo, "Abra un proyecto de Revit con un eje.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                EjeGuardado? eje = ejes.FirstOrDefault(e => uidoc.Selection.GetElementIds().Contains(e.Id)) ?? (ejes.Count == 1 ? ejes[0] : null);
                if (eje == null)
                {
                    TaskDialog.Show(Titulo, ejes.Count == 0 ? "No hay ejes en el modelo." : "Seleccione el eje y vuelva a pulsar el botón.");
                    return Result.Cancelled;
                }

                paso = "actualizar lo que depende del eje";
                ResultadoActualizacion resultado;
                using (TransactionGroup grupo = new TransactionGroup(doc, $"Actualizar desde eje {eje.Eje.Nombre}"))
                {
                    grupo.Start();
                    resultado = ActualizadorEje.Actualizar(doc, eje);
                    grupo.Assimilate();
                }

                TaskDialog.Show(Titulo, $"Eje \"{eje.Eje.Nombre}\"\n\n{resultado.Texto}");
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                TaskDialog.Show(Titulo, ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError(Titulo, paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
