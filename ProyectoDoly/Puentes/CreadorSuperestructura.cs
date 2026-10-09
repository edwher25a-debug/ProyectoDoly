using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Superestructura;

namespace ProyectoDoly.Puentes
{
    public sealed class OpcionesSuperestructura
    {
        public EjeGuardado Eje { get; set; } = null!;
        public SeccionTransversal Seccion { get; set; } = null!;
        public OpcionesBarrido Barrido { get; set; } = new OpcionesBarrido();
    }

    public sealed class ResultadoSuperestructura
    {
        public ElementId Id { get; set; } = ElementId.InvalidElementId;
        public string Resumen { get; set; } = "";
    }

    /// <summary>
    ///     Crea el tablero barriendo la seccion a lo largo del eje: DirectShape en la categoria Tableros de puente
    /// </summary>
    public static class CreadorSuperestructura
    {
        public static ResultadoSuperestructura Crear(Document doc, OpcionesSuperestructura opciones)
        {
            Eje eje = opciones.Eje.Eje;
            Transform transformacion = opciones.Eje.Transformacion;
            List<SolidoBarrido> solidos = BarridoTablero.Generar(eje, opciones.Seccion, opciones.Barrido);
            int secciones = BarridoTablero.Estaciones(eje, opciones.Barrido).Count;

            using (Transaction transaccion = new Transaction(doc, $"Superestructura {eje.Nombre}"))
            {
                transaccion.Start();

                ElementId material = MaterialHormigon(doc);

                //Primero como solido cerrado; si Revit no lo acepta, como malla salvando las caras validas
                TessellatedShapeBuilderResult resultado;
                try
                {
                    resultado = Construir(solidos, transformacion, material, TessellatedShapeBuilderTarget.Solid, TessellatedShapeBuilderFallback.Mesh);
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    resultado = Construir(solidos, transformacion, material, TessellatedShapeBuilderTarget.AnyGeometry, TessellatedShapeBuilderFallback.Salvage);
                }

                if (resultado.Outcome == TessellatedShapeBuilderOutcome.Nothing)
                    throw new InvalidOperationException("Revit no pudo construir el tablero. Pruebe con otro paso entre secciones.");

                DirectShape forma = DirectShape.CreateElement(doc, Categoria(doc));
                forma.ApplicationId = "ProyectoDoly";
                forma.ApplicationDataId = $"Superestructura {eje.Nombre}";
                forma.SetShape(resultado.GetGeometricalObjects());
                forma.SetName($"Tablero {eje.Nombre}");
                forma.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(Descripcion(opciones));

                transaccion.Commit();

                double longitud = opciones.Barrido.EstacionFinal - opciones.Barrido.EstacionInicial;
                string tipoGeometria = resultado.Outcome == TessellatedShapeBuilderOutcome.Solid
                    ? "sólido"
                    : "malla (Revit no lo cerró como sólido: no tendrá volumen en tablas)";

                return new ResultadoSuperestructura
                {
                    Id = forma.Id,
                    Resumen = $"Tablero creado sobre el eje \"{eje.Nombre}\".\n\n" +
                              $"Sección: {opciones.Seccion.Nombre}\n" +
                              $"Desde {Eje.FormatoEstacion(opciones.Barrido.EstacionInicial)} hasta {Eje.FormatoEstacion(opciones.Barrido.EstacionFinal)} ({longitud:F2} m)\n" +
                              $"Secciones: {secciones}, piezas: {solidos.Count}\n" +
                              $"Volumen aproximado: {solidos.Sum(BarridoTablero.Volumen):F2} m³\n" +
                              $"Geometría: {tipoGeometria}"
                };
            }
        }

        private static TessellatedShapeBuilderResult Construir(List<SolidoBarrido> solidos, Transform transformacion, ElementId material,
            TessellatedShapeBuilderTarget objetivo, TessellatedShapeBuilderFallback alternativa)
        {
            TessellatedShapeBuilder constructor = new TessellatedShapeBuilder
            {
                Target = objetivo,
                Fallback = alternativa,
                GraphicsStyleId = ElementId.InvalidElementId
            };

            foreach (SolidoBarrido solido in solidos)
            {
                constructor.OpenConnectedFaceSet(true);
                foreach (Cara cara in solido.Caras)
                {
                    List<IList<XYZ>> lazos = cara.Lazos
                        .Select(l => (IList<XYZ>)l.Select(p => transformacion.OfPoint(CreadorEje.APies(p))).ToList())
                        .ToList();
                    constructor.AddFace(new TessellatedFace(lazos, material));
                }

                constructor.CloseConnectedFaceSet();
            }

            constructor.Build();
            return constructor.GetBuildResult();
        }

        //Tableros de puente si la version de Revit lo admite para DirectShape; si no, Modelo generico
        private static ElementId Categoria(Document doc)
        {
            ElementId tablero = new ElementId(BuiltInCategory.OST_BridgeDecks);
            return DirectShape.IsValidCategoryId(tablero, doc) ? tablero : new ElementId(BuiltInCategory.OST_GenericModel);
        }

        //Primer material de hormigon/concreto del proyecto, si existe
        private static ElementId MaterialHormigon(Document doc)
        {
            string[] nombres = { "hormig", "concret", "concrete" };
            Material? material = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .FirstOrDefault(m => nombres.Any(n => m.Name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0));
            return material?.Id ?? ElementId.InvalidElementId;
        }

        private static string Descripcion(OpcionesSuperestructura opciones) =>
            $"Superestructura | Eje {opciones.Eje.Eje.Nombre} | {opciones.Seccion.Nombre} | " +
            $"{Eje.FormatoEstacion(opciones.Barrido.EstacionInicial)} a {Eje.FormatoEstacion(opciones.Barrido.EstacionFinal)}";
    }
}
