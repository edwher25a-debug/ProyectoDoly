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
    ///     Crea un eje escribiendo sus PI, radios y clotoides (como el editor de ejes de SOFiSTiK), para proyectos sin LandXML
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class EjePorTablaCmd : IExternalCommand
    {
        private const string Titulo = "Eje por tabla";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show(Titulo, "Abra un proyecto de Revit para crear el eje.");
                return Result.Cancelled;
            }

            string paso = "abrir la ventana del eje";
            try
            {
                List<string> existentes = DatosEje.Ejes(doc).Select(e => e.Eje.Nombre).ToList();
                vm_EjeDesdeLineas viewModel = new vm_EjeDesdeLineas(existentes);
                v_EjeDesdeLineas view = new v_EjeDesdeLineas { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Resultado == null) return Result.Cancelled;

                paso = "dibujar el eje";
                viewModel.Resultado.MarcasCada = viewModel.MarcasCadaValor;
                ResultadoEje resultado = CreadorEje.CrearDibujado(doc, viewModel.Resultado, viewModel.Descripcion, "Creado desde una tabla de PI, en coordenadas compartidas");
                uidoc.Selection.SetElementIds(new List<ElementId> { resultado.Id });
                TaskDialog.Show(Titulo, resultado.Resumen);
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
