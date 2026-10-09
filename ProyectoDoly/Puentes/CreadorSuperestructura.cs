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

            //La seccion queda escrita en un archivo para poder revisarla si Revit no acepta la geometria
            string archivoSeccion = GuardarSeccion(opciones);

            using (Transaction transaccion = new Transaction(doc, $"Superestructura {eje.Nombre}"))
            {
                transaccion.Start();

                ElementId material = MaterialHormigon(doc);

                //Cada pieza por separado: si una falla, las demas se crean igual
                List<GeometryObject> geometria = new List<GeometryObject>();
                int comoSolido = 0;
                List<string> fallidas = new List<string>();
                for (int i = 0; i < solidos.Count; i++)
                {
                    TessellatedShapeBuilderResult? resultado = ConstruirPieza(solidos[i], transformacion, material, out string? error);
                    if (resultado == null || resultado.Outcome == TessellatedShapeBuilderOutcome.Nothing)
                    {
                        fallidas.Add($"pieza {i + 1}: {error ?? "sin geometría"}");
                        continue;
                    }

                    if (resultado.Outcome == TessellatedShapeBuilderOutcome.Solid) comoSolido++;
                    geometria.AddRange(resultado.GetGeometricalObjects());
                }

                if (geometria.Count == 0)
                    throw new InvalidOperationException("Revit no aceptó la geometría del tablero.\n\n" + string.Join("\n", fallidas) +
                                                        $"\n\nLa sección usada está en:\n{archivoSeccion}");

                DirectShape forma = DirectShape.CreateElement(doc, Categoria(doc));
                forma.ApplicationId = "ProyectoDoly";
                forma.ApplicationDataId = $"Superestructura {eje.Nombre}";
                forma.SetShape(geometria);
                forma.SetName($"Tablero {eje.Nombre}");
                forma.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(Descripcion(opciones));

                transaccion.Commit();

                double longitud = opciones.Barrido.EstacionFinal - opciones.Barrido.EstacionInicial;
                string tipoGeometria = comoSolido == solidos.Count
                    ? "sólido"
                    : $"{comoSolido} de {solidos.Count} piezas como sólido; el resto como malla (sin volumen en tablas)";

                return new ResultadoSuperestructura
                {
                    Id = forma.Id,
                    Resumen = $"Tablero creado sobre el eje \"{eje.Nombre}\".\n\n" +
                              $"Sección: {opciones.Seccion.Nombre}\n" +
                              $"Desde {Eje.FormatoEstacion(opciones.Barrido.EstacionInicial)} hasta {Eje.FormatoEstacion(opciones.Barrido.EstacionFinal)} ({longitud:F2} m)\n" +
                              $"Secciones: {secciones}, piezas: {solidos.Count}\n" +
                              $"Volumen aproximado: {solidos.Sum(BarridoTablero.Volumen):F2} m³\n" +
                              $"Geometría: {tipoGeometria}" +
                              (fallidas.Count > 0 ? $"\n\nNo se pudieron crear:\n{string.Join("\n", fallidas)}\nSección guardada en {archivoSeccion}" : "")
                };
            }
        }

        //Primero como solido cerrado; si Revit no lo acepta, como malla salvando las caras validas
        private static TessellatedShapeBuilderResult? ConstruirPieza(SolidoBarrido solido, Transform transformacion, ElementId material, out string? error)
        {
            error = null;
            try
            {
                return Construir(solido, transformacion, material, TessellatedShapeBuilderTarget.Solid, TessellatedShapeBuilderFallback.Mesh);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                //Se intenta como malla
            }

            try
            {
                return Construir(solido, transformacion, material, TessellatedShapeBuilderTarget.AnyGeometry, TessellatedShapeBuilderFallback.Salvage);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                error = ex.Message;
                return null;
            }
        }

        //Contornos de la seccion en metros (x derecha, y arriba), uno por linea
        private static string GuardarSeccion(OpcionesSuperestructura opciones)
        {
            string archivo = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProyectoDoly_seccion.txt");
            System.Text.StringBuilder texto = new System.Text.StringBuilder();
            texto.AppendLine($"Sección: {opciones.Seccion.Nombre}");
            texto.AppendLine($"Eje: {opciones.Eje.Eje.Nombre} | {opciones.Barrido.EstacionInicial} a {opciones.Barrido.EstacionFinal} | paso {opciones.Barrido.Paso}");
            texto.AppendLine($"Desplazamiento: derecha {opciones.Barrido.DesplazamientoLateral}, arriba {opciones.Barrido.DesplazamientoVertical}");
            for (int i = 0; i < opciones.Seccion.Piezas.Count; i++)
            {
                Pieza pieza = opciones.Seccion.Piezas[i];
                int k = 0;
                foreach (Contorno contorno in pieza.Contornos)
                {
                    string puntos = string.Join(" ", contorno.Puntos.Select(p =>
                        $"{p.X.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},{p.Y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}"));
                    texto.AppendLine($"Pieza {i + 1} {(k++ == 0 ? "exterior" : "hueco")} ({contorno.Puntos.Count} puntos): {puntos}");
                }
            }

            try
            {
                System.IO.File.WriteAllText(archivo, texto.ToString());
                return archivo;
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                return "(no se pudo guardar)";
            }
        }

        private static TessellatedShapeBuilderResult Construir(SolidoBarrido solido, Transform transformacion, ElementId material,
            TessellatedShapeBuilderTarget objetivo, TessellatedShapeBuilderFallback alternativa)
        {
            TessellatedShapeBuilder constructor = new TessellatedShapeBuilder
            {
                Target = objetivo,
                Fallback = alternativa,
                GraphicsStyleId = ElementId.InvalidElementId
            };

            constructor.OpenConnectedFaceSet(true);
            foreach (Cara cara in solido.Caras)
            {
                List<IList<XYZ>> lazos = cara.Lazos
                    .Select(l => (IList<XYZ>)l.Select(p => transformacion.OfPoint(CreadorEje.APies(p))).ToList())
                    .ToList();

                //Triangulos sin area (puntos alineados) hacen fallar a Revit
                IList<XYZ> v = lazos[0];
                if (v.Count == 3 && (v[1] - v[0]).CrossProduct(v[2] - v[0]).GetLength() < 1e-9) continue;

                constructor.AddFace(new TessellatedFace(lazos, material));
            }

            constructor.CloseConnectedFaceSet();

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
