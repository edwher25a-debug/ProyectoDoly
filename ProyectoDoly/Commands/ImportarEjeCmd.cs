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
    ///     Importa un eje desde LandXML y lo dibuja en el modelo
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportarEjeCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document? doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Importar eje", "Abra un proyecto de Revit para importar el eje.");
                return Result.Cancelled;
            }

            vm_ImportarEje viewModel = new vm_ImportarEje();
            v_ImportarEje view = new v_ImportarEje { DataContext = viewModel };
            viewModel.Cerrar = aceptado => view.DialogResult = aceptado;

            if (view.ShowDialog() != true || viewModel.Opciones == null) return Result.Cancelled;

            try
            {
                ResultadoEje resultado = CreadorEje.Crear(doc, viewModel.Opciones);
                commandData.Application.ActiveUIDocument!.Selection.SetElementIds(new List<ElementId> { resultado.Id });
                TaskDialog.Show("Importar eje", resultado.Resumen);
                return Result.Succeeded;
            }
            catch (InvalidOperationException ex)
            {
                //Errores esperados (eje fuera de rango, geometria no valida): se explican al usuario
                TaskDialog.Show("Importar eje", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Importar eje", "crear el eje en el modelo", ex);
                return Result.Cancelled;
            }
        }
    }
}
