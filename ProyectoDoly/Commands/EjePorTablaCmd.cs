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
    ///     Crea o edita un eje elemento por elemento (como el cuadro Axis Data de SOFiSTiK), para proyectos sin LandXML.
    ///     Con un eje seleccionado, lo abre para editarlo.
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

            string paso = "leer los ejes del modelo";
            try
            {
                List<EjeGuardado> ejes = DatosEje.Ejes(doc);
                ICollection<ElementId> seleccion = uidoc.Selection.GetElementIds();
                EjeGuardado? editar = ejes.FirstOrDefault(e => seleccion.Contains(e.Id));

                paso = "abrir la ventana del eje";
                vm_EditorEje viewModel = new vm_EditorEje(ejes, editar);
                v_EditorEje view = new v_EditorEje { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Resultado == null) return Result.Cancelled;

                paso = "dibujar el eje";
                ResultadoEje resultado;
                if (editar != null)
                {
                    //Eje y todo lo atado a el en un solo paso de deshacer
                    using (TransactionGroup grupo = new TransactionGroup(doc, $"Editar eje {viewModel.Resultado.Nombre}"))
                    {
                        grupo.Start();
                        resultado = CreadorEje.Reemplazar(doc, editar, viewModel.Resultado, viewModel.Descripcion);
                        paso = "actualizar lo que depende del eje";
                        ResultadoActualizacion dependientes = ActualizadorEje.Actualizar(doc, new EjeGuardado { Id = editar.Id, Eje = viewModel.Resultado, Transformacion = editar.Transformacion });
                        grupo.Assimilate();
                        resultado.Resumen = resultado.Resumen.Substring(0, resultado.Resumen.IndexOf("\n\nLo que ya", StringComparison.Ordinal) is int corte && corte > 0 ? corte : resultado.Resumen.Length) +
                                            "\n\n" + dependientes.Texto;
                    }
                }
                else
                {
                    Transform transformacion = viewModel.Sistema switch
                    {
                        SistemaEje.Compartidas => CreadorEje.InternaACompartida(doc).Inverse,
                        SistemaEje.Interno => Transform.Identity,
                        _ => CreadorEje.ProyectoAInterna(doc)
                    };
                    string sistema = viewModel.Sistema switch
                    {
                        SistemaEje.Compartidas => "coordenadas compartidas",
                        SistemaEje.Interno => "origen interno",
                        _ => "coordenadas de proyecto (punto base)"
                    };
                    resultado = CreadorEje.CrearDibujado(doc, viewModel.Resultado, transformacion, viewModel.Descripcion, $"Creado por tabla, en {sistema}");
                }

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
