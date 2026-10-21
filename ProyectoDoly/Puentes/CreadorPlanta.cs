using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Planos;

namespace ProyectoDoly.Puentes
{
    public sealed class OpcionesPlanta
    {
        public EjeGuardado Eje { get; set; } = null!;

        //Rejillas transversales al eje en los cortes (placements y entre placements), de "LargoRejilla" m centradas en el eje
        public List<CorteSeccion> Rejillas { get; set; } = new List<CorteSeccion>();
        public double LargoRejilla { get; set; } = 30;
        public string PrefijoRejilla { get; set; } = "";

        //Marcas de estacion en la vista de planta: raya transversal y texto con la estacion
        public ViewPlan? Vista { get; set; }
        public List<double> Estaciones { get; set; } = new List<double>();
        public double LargoMarca { get; set; } = 4;
        public TextNoteType? TipoTexto { get; set; }
    }

    public sealed class ResultadoPlanta
    {
        public List<ElementId> Ids { get; } = new List<ElementId>();
        public string Resumen { get; set; } = "";
    }

    /// <summary>
    ///     Rejillas en placements y marcas de estacion en planta (como Grids y Axis Stations de SOFiSTiK)
    /// </summary>
    public static class CreadorPlanta
    {
        public static List<TextNoteType> TiposTexto(Document doc) =>
            new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().OrderBy(t => t.Name).ToList();

        public static ResultadoPlanta Crear(Document doc, OpcionesPlanta opciones)
        {
            Eje eje = opciones.Eje.Eje;
            Transform transformacion = opciones.Eje.Transformacion;
            ResultadoPlanta resultado = new ResultadoPlanta();
            List<string> lineas = new List<string>();

            double Pies(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
            XYZ Revit(Punto2 p, double z) => transformacion.OfPoint(CreadorEje.APies(new Punto3(p.X, p.Y, 0))) + new XYZ(0, 0, z);

            using (Transaction transaccion = new Transaction(doc, $"Rejillas y estaciones {eje.Nombre}"))
            {
                transaccion.Start();

                if (opciones.Rejillas.Count > 0)
                {
                    HashSet<string> nombres = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().Select(g => g.Name));
                    foreach (CorteSeccion corte in opciones.Rejillas)
                    {
                        (Punto2 izquierda, Punto2 derecha) = MarcasPlanta.Travesano(eje, corte.Estacion, corte.Giro, opciones.LargoRejilla / 2, opciones.LargoRejilla / 2);
                        //La burbuja queda a la izquierda del eje: la rejilla va de derecha a izquierda
                        Grid rejilla = Grid.Create(doc, Line.CreateBound(Revit(derecha, 0), Revit(izquierda, 0)));
                        rejilla.Name = Libre(nombres, opciones.PrefijoRejilla + corte.Nombre);
                        resultado.Ids.Add(rejilla.Id);
                    }

                    lineas.Add($"{opciones.Rejillas.Count} rejillas: " + string.Join(", ", resultado.Ids.Take(20).Select(id => doc.GetElement(id).Name)) +
                               (opciones.Rejillas.Count > 20 ? "…" : ""));
                }

                if (opciones.Vista != null && opciones.Estaciones.Count > 0)
                {
                    ViewPlan vista = opciones.Vista;
                    double z = vista.GenLevel?.Elevation ?? 0;
                    ElementId tipoTexto = opciones.TipoTexto?.Id ?? doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                    double mitad = opciones.LargoMarca / 2;

                    foreach (double estacion in opciones.Estaciones)
                    {
                        (Punto2 izquierda, Punto2 derecha) = MarcasPlanta.Travesano(eje, estacion, 0, mitad, mitad);
                        XYZ a = Revit(izquierda, z), b = Revit(derecha, z);
                        DetailCurve raya = doc.Create.NewDetailCurve(vista, Line.CreateBound(b, a));
                        resultado.Ids.Add(raya.Id);

                        //El texto sigue a la raya, por fuera del lado izquierdo del eje
                        XYZ hacia = (a - b).Normalize();
                        double angulo = MarcasPlanta.AnguloLegible(Math.Atan2(hacia.Y, hacia.X));
                        bool alReves = Math.Abs(angulo - Math.Atan2(hacia.Y, hacia.X)) > 1e-6;
                        TextNoteOptions formato = new TextNoteOptions(tipoTexto)
                        {
                            Rotation = angulo,
                            HorizontalAlignment = alReves ? HorizontalTextAlignment.Right : HorizontalTextAlignment.Left,
                            VerticalAlignment = VerticalTextAlignment.Middle
                        };
                        TextNote texto = TextNote.Create(doc, vista.Id, a + hacia * Pies(0.5), Eje.FormatoEstacion(estacion), formato);
                        resultado.Ids.Add(texto.Id);
                    }

                    lineas.Add($"{opciones.Estaciones.Count} marcas de estación en la vista \"{vista.Name}\", de {Eje.FormatoEstacion(opciones.Estaciones.First())} a {Eje.FormatoEstacion(opciones.Estaciones.Last())}");
                }

                if (transaccion.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit no pudo crear las rejillas y estaciones.");
            }

            resultado.Resumen = $"Eje \"{eje.Nombre}\":\n\n" + string.Join("\n\n", lineas);
            return resultado;
        }

        //Revit no admite nombres de rejilla repetidos
        private static string Libre(HashSet<string> usados, string nombre)
        {
            string libre = nombre;
            for (int i = 2; usados.Contains(libre); i++) libre = $"{nombre}.{i}";
            usados.Add(libre);
            return libre;
        }
    }
}
