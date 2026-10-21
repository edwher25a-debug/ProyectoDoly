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
    ///     Escribe datos del eje y volumen en parametros compartidos de los elementos del puente (como Quantification de SOFiSTiK)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CuantificarCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Cuantificar", "Abra un proyecto de Revit con un eje importado.");
                return Result.Cancelled;
            }

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                if (ejes.Count == 0)
                {
                    TaskDialog.Show("Cuantificar", "No hay ejes en el modelo.\n\nImporte el eje con el botón Importar eje.");
                    return Result.Cancelled;
                }

                List<ElementId> seleccion = uidoc.Selection.GetElementIds().ToList();
                ElementId? ejeElegido = seleccion.FirstOrDefault(id => ejes.Any(e => e.Id == id));

                //Los grupos se cuantifican por sus miembros
                List<ElementId> elementosSeleccion = new List<ElementId>();
                foreach (ElementId id in seleccion.Where(id => ejes.All(e => e.Id != id)))
                    if (doc.GetElement(id) is Group grupo) elementosSeleccion.AddRange(grupo.GetMemberIds());
                    else elementosSeleccion.Add(id);

                paso = "abrir la ventana de cuantificación";
                vm_Cuantificar viewModel = new vm_Cuantificar(doc, ejes, ejeElegido, elementosSeleccion.Distinct().ToList());
                v_Cuantificar view = new v_Cuantificar { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Opciones == null) return Result.Cancelled;

                paso = "escribir los parámetros de cuantificación";
                string resumen = CuantificadorPuente.Cuantificar(doc, viewModel.Opciones);
                TaskDialog.Show("Cuantificar", resumen);
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                TaskDialog.Show("Cuantificar", ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Diagnostico.MostrarError("Cuantificar", paso, ex);
                return Result.Cancelled;
            }
        }
    }
}
