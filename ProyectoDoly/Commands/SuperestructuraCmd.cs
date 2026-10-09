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
    ///     Crea el tablero barriendo una seccion (familia) a lo largo de un eje importado
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SuperestructuraCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Superestructura", "Abra un proyecto de Revit para crear el tablero.");
                return Result.Cancelled;
            }

            //Paso en curso, para decir donde fallo si Revit lanza un error inesperado
            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                if (ejes.Count == 0)
                {
                    TaskDialog.Show("Superestructura",
                        "No hay ejes en el modelo.\n\nImporte el eje con el botón Importar eje. " +
                        "Los ejes importados con una versión anterior de ProyectoDoly deben importarse de nuevo.");
                    return Result.Cancelled;
                }

                //Si el usuario ya tenia un eje seleccionado, se propone ese
                ElementId? seleccionado = uidoc.Selection.GetElementIds().FirstOrDefault(id => ejes.Any(e => e.Id == id));

                paso = "abrir la ventana y leer la sección de la familia";
                vm_Superestructura viewModel = new vm_Superestructura(doc, ejes, seleccionado);
                v_Superestructura view = new v_Superestructura { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;

                //La familia de seccion queda abierta en segundo plano mientras la ventana esta abierta
                bool aceptado;
                try
                {
                    aceptado = view.ShowDialog() == true;
                }
                finally
                {
                    viewModel.Liberar();
                }

                if (!aceptado || viewModel.Opciones == null) return Result.Cancelled;

                paso = "crear el tablero en el modelo";
                ResultadoSuperestructura resultado = viewModel.Opciones.FamiliaAdaptativa
                    ? FamiliaTablero.Crear(doc, viewModel.Opciones)
                    : CreadorSuperestructura.Crear(doc, viewModel.Opciones);
                uidoc.Selection.SetElementIds(new List<ElementId> { resultado.Id });
                TaskDialog.Show("Superestructura", resultado.Resumen);
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                //Errores esperados (geometria no valida, tramo fuera del eje): se explican al usuario
                TaskDialog.Show("Superestructura", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Superestructura", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
