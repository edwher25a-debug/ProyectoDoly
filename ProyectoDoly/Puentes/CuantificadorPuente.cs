using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Cuantificacion;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes
{
    public sealed class OpcionesCuantificacion
    {
        public EjeGuardado Eje { get; set; } = null!;
        public List<ElementId> Elementos { get; set; } = new List<ElementId>();
    }

    /// <summary>
    ///     Cuantifica elementos respecto al eje (como Quantification de SOFiSTiK): tramo de estaciones, placement,
    ///     desfase y volumen, escritos en parametros compartidos para tablas de planificacion
    /// </summary>
    public static class CuantificadorPuente
    {
        private const int MaximoPuntos = 3000;

        //Elementos que las herramientas de Puentes crearon en este eje (por su comentario)
        public static List<ElementId> CreadosEnEje(Document doc, Eje eje)
        {
            string tablero = $"Tablero {eje.Nombre}";
            string marca = $"| Eje {eje.Nombre} |";
            return new FilteredElementCollector(doc).WhereElementIsNotElementType()
                .Where(e => e is FamilyInstance || e is DirectShape)
                .Where(e =>
                {
                    string c = e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";
                    return c.StartsWith(tablero + " ", StringComparison.Ordinal) || c.Contains(marca);
                })
                .Select(e => e.Id).ToList();
        }

        public static string Cuantificar(Document doc, OpcionesCuantificacion opciones)
        {
            Eje eje = opciones.Eje.Eje;
            Transform aEje = opciones.Eje.Transformacion.Inverse;
            ProyectorEje proyector = new ProyectorEje(eje);
            List<Element> elementos = opciones.Elementos.Select(doc.GetElement).Where(e => e?.Category != null).ToList();
            if (elementos.Count == 0) throw new InvalidOperationException("No hay elementos con categoría para cuantificar.");

            double Metros(double pies) => UnitUtils.ConvertFromInternalUnits(pies, UnitTypeId.Meters);
            Punto2 Planta(XYZ p)
            {
                XYZ q = aEje.OfPoint(p);
                return new Punto2(Metros(q.X), Metros(q.Y));
            }

            int hechos = 0;
            double volumenTotal = 0;
            List<string> avisos = new List<string>();
            List<string> lineas = new List<string>();

            using (Transaction transaccion = new Transaction(doc, $"Cuantificar {eje.Nombre}"))
            {
                transaccion.Start();
                ParametrosPuente.Enlazar(doc, elementos.Select(e => e.Category).GroupBy(c => c.Id).Select(g => g.First()));
                foreach (ParametrosPuente.Definicion d in ParametrosPuente.Todos)
                {
                    //Los tableros por tramos van en grupos: cada ejemplar debe poder tener su valor
                    if (SharedParameterElement.Lookup(doc, d.Guid)?.GetDefinition() is InternalDefinition interna && !interna.VariesAcrossGroups)
                        try
                        {
                            interna.SetAllowVaryBetweenGroups(doc, true);
                        }
                        catch (Autodesk.Revit.Exceptions.ApplicationException)
                        {
                            //Algunos parametros no lo admiten: se escriben igual donde se pueda
                        }
                }

                doc.Regenerate();

                foreach (Element elemento in elementos)
                {
                    List<XYZ> puntos = new List<XYZ>();
                    double volumen = 0;
                    Recolectar(elemento.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine }), puntos, ref volumen);
                    if (puntos.Count == 0 && elemento.Location is LocationPoint lp) puntos.Add(lp.Point);
                    if (puntos.Count == 0)
                    {
                        avisos.Add($"{elemento.Name} ({elemento.Id}): sin geometría.");
                        continue;
                    }

                    List<Punto2> enPlanta = Ralear(puntos).Select(Planta).ToList();
                    Punto2 centro = elemento.Location is LocationPoint punto
                        ? Planta(punto.Point)
                        : new Punto2(enPlanta.Average(p => p.X), enPlanta.Average(p => p.Y));
                    DatosCuantificacion datos = CuantificacionEje.Calcular(proyector, eje, enPlanta, centro);
                    double m3 = UnitUtils.ConvertFromInternalUnits(volumen, UnitTypeId.CubicMeters);

                    bool ok = true;
                    ok &= Escribir(elemento, ParametrosPuente.Eje, eje.Nombre);
                    ok &= Escribir(elemento, ParametrosPuente.Placement, datos.Placement);
                    ok &= Escribir(elemento, ParametrosPuente.Estaciones, $"{Eje.FormatoEstacion(datos.EstacionInicio)} a {Eje.FormatoEstacion(datos.EstacionFin)}");
                    ok &= Escribir(elemento, ParametrosPuente.EstacionInicio, UnitUtils.ConvertToInternalUnits(datos.EstacionInicio, UnitTypeId.Meters));
                    ok &= Escribir(elemento, ParametrosPuente.EstacionFin, UnitUtils.ConvertToInternalUnits(datos.EstacionFin, UnitTypeId.Meters));
                    ok &= Escribir(elemento, ParametrosPuente.Longitud, UnitUtils.ConvertToInternalUnits(datos.Longitud, UnitTypeId.Meters));
                    ok &= Escribir(elemento, ParametrosPuente.Desfase, UnitUtils.ConvertToInternalUnits(datos.Desfase, UnitTypeId.Meters));
                    ok &= Escribir(elemento, ParametrosPuente.Volumen, volumen);
                    if (!ok) avisos.Add($"{elemento.Name} ({elemento.Id}): algunos parámetros no se pudieron escribir (¿elemento en un grupo?).");

                    hechos++;
                    volumenTotal += m3;
                    if (lineas.Count < 15)
                        lineas.Add($"{(datos.Placement.Length > 0 ? datos.Placement : elemento.Name)}   {Eje.FormatoEstacion(datos.EstacionInicio)} a {Eje.FormatoEstacion(datos.EstacionFin)}" +
                                   (m3 > 0 ? $"   {m3:F2} m³" : ""));
                }

                if (transaccion.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit no pudo escribir los parámetros de cuantificación.");
            }

            return $"{hechos} elementos cuantificados respecto al eje \"{eje.Nombre}\". Volumen total: {volumenTotal:F2} m³.\n\n" +
                   string.Join("\n", lineas) + (hechos > lineas.Count ? $"\n(y {hechos - lineas.Count} más)" : "") +
                   "\n\nParámetros (grupo Datos): " + string.Join(", ", ParametrosPuente.Todos.Select(d => d.Nombre)) +
                   ". Úselos en una tabla de planificación." +
                   (avisos.Count > 0 ? "\n\nAvisos:\n" + string.Join("\n", avisos.Distinct().Take(6)) : "");
        }

        private static bool Escribir(Element elemento, ParametrosPuente.Definicion d, string valor)
        {
            Parameter? p = elemento.get_Parameter(d.Guid);
            return p != null && !p.IsReadOnly && p.Set(valor);
        }

        private static bool Escribir(Element elemento, ParametrosPuente.Definicion d, double valor)
        {
            Parameter? p = elemento.get_Parameter(d.Guid);
            return p != null && !p.IsReadOnly && p.Set(valor);
        }

        //Vertices de solidos (aristas teseladas) y mallas, y volumen de los solidos en pies cubicos
        private static void Recolectar(GeometryElement? geometria, List<XYZ> puntos, ref double volumen)
        {
            if (geometria == null) return;
            foreach (GeometryObject objeto in geometria)
            {
                switch (objeto)
                {
                    case Solid solido when solido.Volume > 0:
                        volumen += solido.Volume;
                        foreach (Edge arista in solido.Edges) puntos.AddRange(arista.Tessellate());
                        break;
                    case Mesh malla:
                        puntos.AddRange(malla.Vertices);
                        break;
                    case GeometryInstance instancia:
                        Recolectar(instancia.GetInstanceGeometry(), puntos, ref volumen);
                        break;
                }
            }
        }

        private static IEnumerable<XYZ> Ralear(List<XYZ> puntos)
        {
            int salto = Math.Max(1, puntos.Count / MaximoPuntos);
            for (int i = 0; i < puntos.Count; i += salto) yield return puntos[i];
            yield return puntos[puntos.Count - 1];
        }
    }
}
