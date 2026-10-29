using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Utils;
using ProyectoDoly.ViewModels;
using ProyectoDoly.Views;

namespace ProyectoDoly.Commands
{
    /// <summary>
    ///     Crea un eje a partir de rectas y arcos del modelo o de un DWG, para proyectos sin LandXML
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class EjeDesdeLineasCmd : IExternalCommand
    {
        private const string Titulo = "Eje desde líneas";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = commandData.Application.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (uidoc == null || doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show(Titulo, "Abra un proyecto de Revit con las líneas del eje dibujadas.");
                return Result.Cancelled;
            }

            string paso = "elegir las líneas del eje";
            try
            {
                Transform aCompartidas = CreadorEje.InternaACompartida(doc);
                List<Curve> curvas = Preseleccion(doc, uidoc);
                if (curvas.Count == 0)
                {
                    List<Curve>? elegidas = Elegir(doc, uidoc);
                    if (elegidas == null) return Result.Cancelled;
                    curvas = elegidas;
                }

                if (curvas.Count == 0)
                {
                    TaskDialog.Show(Titulo, "No eligió ninguna recta ni arco.");
                    return Result.Cancelled;
                }

                List<TramoDibujado> tramos = curvas.Select(c => Tramo(c, aCompartidas)).ToList();
                Punto2 cercaDe = tramos[0].Inicio;
                double cota = Metros(aCompartidas.OfPoint(curvas[0].GetEndPoint(0)).Z);

                paso = "abrir la ventana del eje";
                List<string> existentes = DatosEje.Ejes(doc).Select(e => e.Eje.Nombre).ToList();
                vm_EjeDesdeLineas viewModel = new vm_EjeDesdeLineas(tramos, cercaDe, cota, existentes);
                v_EjeDesdeLineas view = new v_EjeDesdeLineas { DataContext = viewModel };
                viewModel.Cerrar = aceptado => view.DialogResult = aceptado;
                if (view.ShowDialog() != true || viewModel.Resultado == null) return Result.Cancelled;

                paso = "dibujar el eje";
                viewModel.Resultado.MarcasCada = viewModel.MarcasCadaValor;
                ResultadoEje resultado = CreadorEje.CrearDibujado(doc, viewModel.Resultado, viewModel.Descripcion, $"Dibujado desde {curvas.Count} líneas, en coordenadas compartidas");
                uidoc.Selection.SetElementIds(new List<ElementId> { resultado.Id });
                TaskDialog.Show(Titulo, resultado.Resumen);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
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

        //Lineas del modelo o de detalle ya seleccionadas, en el orden de la seleccion
        private static List<Curve> Preseleccion(Document doc, UIDocument uidoc) =>
            uidoc.Selection.GetElementIds().Select(doc.GetElement).OfType<CurveElement>()
                .Select(c => c.GeometryCurve).Where(c => c != null).ToList();

        private static List<Curve>? Elegir(Document doc, UIDocument uidoc)
        {
            TaskDialog dialogo = new TaskDialog(Titulo)
            {
                MainInstruction = "¿Dónde está dibujado el eje?",
                MainContent = "Elija las rectas y arcos del eje en cualquier orden y termine con Finalizar. " +
                              "El eje empieza en el extremo más cercano al inicio de la primera línea elegida.",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };
            dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Líneas del modelo o de detalle", "Dibujadas en Revit en una vista de planta.");
            dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Líneas de un DWG vinculado o importado", "Se elige cada línea o arco del CAD; las polilíneas se toman por tramos rectos.");

            TaskDialogResult opcion = dialogo.Show();
            if (opcion == TaskDialogResult.CommandLink1)
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, new FiltroLineas(), "Elija las rectas y arcos del eje y pulse Finalizar");
                return refs.Select(r => doc.GetElement(r)).OfType<CurveElement>().Select(c => c.GeometryCurve).ToList();
            }

            if (opcion == TaskDialogResult.CommandLink2)
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.PointOnElement, new FiltroCad(), "Elija las líneas y arcos del eje en el DWG y pulse Finalizar");
                List<Curve> curvas = new List<Curve>();
                foreach (Reference r in refs)
                {
                    if (!(doc.GetElement(r) is ImportInstance cad)) continue;
                    Transform t = cad.GetTotalTransform();
                    switch (cad.GetGeometryObjectFromReference(r))
                    {
                        case Curve c:
                            curvas.Add(c.CreateTransformed(t));
                            break;
                        case PolyLine p:
                            IList<XYZ> v = p.GetCoordinates();
                            for (int i = 1; i < v.Count; i++)
                                if (v[i].DistanceTo(v[i - 1]) > doc.Application.ShortCurveTolerance)
                                    curvas.Add(Line.CreateBound(t.OfPoint(v[i - 1]), t.OfPoint(v[i])));
                            break;
                    }
                }

                //Varias picadas sobre la misma linea del CAD la repetirian
                return curvas.GroupBy(c => Clave(c)).Select(g => g.First()).ToList();
            }

            return null;
        }

        private static string Clave(Curve c)
        {
            XYZ a = c.GetEndPoint(0), b = c.GetEndPoint(1);
            return $"{a.X:F4},{a.Y:F4},{b.X:F4},{b.Y:F4},{c.GetType().Name}";
        }

        //Recta o arco de Revit en planta y en metros de coordenadas compartidas
        private static TramoDibujado Tramo(Curve curva, Transform aCompartidas)
        {
            Punto2 Planta(XYZ p)
            {
                XYZ q = aCompartidas.OfPoint(p);
                return new Punto2(Metros(q.X), Metros(q.Y));
            }

            if (!curva.IsBound) throw new ArgumentException("Hay un círculo o una curva cerrada entre las líneas elegidas. Parta el círculo en arcos.");

            switch (curva)
            {
                case Line linea:
                    return TramoDibujado.Recta(Planta(linea.GetEndPoint(0)), Planta(linea.GetEndPoint(1)));
                case Arc arco:
                    if (Math.Abs(arco.Normal.Z) < 0.999)
                        throw new ArgumentException("Hay un arco que no está en un plano horizontal. Dibuje el eje en una vista de planta.");
                    return TramoDibujado.Arco(Planta(arco.GetEndPoint(0)), Planta(arco.GetEndPoint(1)), Planta(arco.Center), arco.Normal.Z > 0);
                default:
                    throw new ArgumentException($"Hay una curva de tipo {curva.GetType().Name}. El eje admite solo rectas y arcos; convierta splines y elipses en arcos.");
            }
        }

        private static double Metros(double pies) => UnitUtils.ConvertFromInternalUnits(pies, UnitTypeId.Meters);

        private sealed class FiltroLineas : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is CurveElement c && (c.GeometryCurve is Line || c.GeometryCurve is Arc);
            public bool AllowReference(Reference reference, XYZ position) => true;
        }

        private sealed class FiltroCad : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is ImportInstance;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
