using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using ProyectoDoly.ViewModels;
using ProyectoDoly.Views;

namespace ProyectoDoly.Commands
{
    /// <summary>
    ///     Ventana unica con todas las herramientas
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PrincipalCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null)
            {
                TaskDialog.Show("ProyectoDoly", "Abra un proyecto para usar las herramientas.");
                return Result.Cancelled;
            }

            vm_Principal viewModel = new vm_Principal(uiDocument.Document, uiDocument.Selection);

            //La ventana se cierra para trabajar en Revit y se vuelve a abrir en la misma herramienta
            do
            {
                v_Principal view = new v_Principal { DataContext = viewModel };
                viewModel.v_Principal = view;
                view.ShowDialog();
            }
            while (viewModel.EjecutarPendiente());

            return Result.Succeeded;
        }
    }
}
