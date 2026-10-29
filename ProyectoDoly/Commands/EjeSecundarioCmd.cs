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
    ///     Crea un eje desplazado del principal (como los ejes secundarios de SOFiSTiK)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class EjeSecundarioCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Eje secundario", "Abra un proyecto de Revit con un eje importado.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                if (ejes.Count == 0)
                {
                    TaskDialog.Show("Eje secundario", "No hay ejes en el modelo.\n\nImporte primero el eje principal con el botón Importar eje.");
                    return Result.Cancelled;
                }

                ElementId? seleccionado = uidoc.Selection.GetElementIds().FirstOrDefault(id => ejes.Any(e => e.Id == id));

                paso = "abrir la ventana del eje secundario";
                vm_EjeSecundario viewModel = new vm_EjeSecundario(ejes, seleccionado);
                v_EjeSecundario view = new v_EjeSecundario { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Resultado == null || viewModel.EjeSelecc == null) return Result.Cancelled;

                paso = "dibujar el eje secundario";
                ResultadoEje resultado = CreadorEje.CrearDerivado(doc, viewModel.Resultado, viewModel.EjeSelecc, viewModel.Descripcion);

                //Queda atado al principal: si este cambia, se vuelve a calcular
                if (viewModel.OpcionesUsadas != null && doc.GetElement(resultado.Id) is Element secundario && doc.GetElement(viewModel.EjeSelecc.Id) is Element principal)
                    using (Transaction transaccion = new Transaction(doc, "Atar eje secundario"))
                    {
                        transaccion.Start();
                        DatosDependencia.Guardar(secundario, principal, Puentes.Dependencias.Recetas.DeEjeSecundario(viewModel.OpcionesUsadas));
                        transaccion.Commit();
                    }
                uidoc.Selection.SetElementIds(new List<ElementId> { resultado.Id });
                TaskDialog.Show("Eje secundario", resultado.Resumen);
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                TaskDialog.Show("Eje secundario", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Eje secundario", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
