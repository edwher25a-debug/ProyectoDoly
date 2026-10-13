using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes
{
    public enum Ubicacion
    {
        //El eje queda en sus coordenadas reales usando la ubicacion compartida del proyecto
        Compartidas,

        //El inicio del eje se lleva al origen interno (las cotas se conservan)
        Origen
    }

    public sealed class OpcionesEje
    {
        public Eje Eje { get; set; } = null!;
        public Ubicacion Ubicacion { get; set; } = Ubicacion.Compartidas;
        public double MarcasCada { get; set; } = 20;
        public string Archivo { get; set; } = "";
    }

    public sealed class ResultadoEje
    {
        public ElementId Id { get; set; } = ElementId.InvalidElementId;
        public string Resumen { get; set; } = "";
    }

    /// <summary>
    ///     Dibuja el eje como un DirectShape de Modelo generico: polilinea 3D y marcas de estacion
    /// </summary>
    public static class CreadorEje
    {
        //Revit pierde precision y avisa con geometria a mas de ~32 km del origen interno
        private const double DistanciaMaximaMetros = 30000;

        //Largo de las marcas: normales cada MarcasCada y mayores cada 100 m
        private const double MarcaMetros = 2;
        private const double MarcaMayorMetros = 5;

        public static ResultadoEje Crear(Document doc, OpcionesEje opciones)
        {
            Eje eje = opciones.Eje;
            Transform transformacion = Transformacion(doc, opciones);
            double tolerancia = doc.Application.ShortCurveTolerance;

            //01_Eje continuo: una linea por recta con pendiente constante y una curva suave por cada curva, clotoide
            //o curva vertical (sin partirlo cada pocos metros)
            List<TramoEje> tramos = TramosEje.Calcular(eje);
            ValidarDistancia(tramos.SelectMany(t => t.Estaciones).Select(e => transformacion.OfPoint(APies(eje.Evaluar(e).Posicion))).ToList(),
                opciones.Ubicacion);

            //Geometria con curvas suaves y, por si Revit no las acepta, la misma con segmentos cortos
            List<GeometryObject> geometria = new List<GeometryObject>();
            List<GeometryObject> segmentos = new List<GeometryObject>();
            int curvas = 0, lineas = 0;
            foreach (TramoEje tramo in tramos)
            {
                List<XYZ> puntos = tramo.Estaciones.Select(e => transformacion.OfPoint(APies(eje.Evaluar(e).Posicion))).ToList();
                if (puntos[0].DistanceTo(puntos[puntos.Count - 1]) < tolerancia) continue;

                if (tramo.EsLinea)
                {
                    Line linea = Line.CreateBound(puntos[0], puntos[puntos.Count - 1]);
                    geometria.Add(linea);
                    segmentos.Add(linea);
                    lineas++;
                    continue;
                }

                List<Line> cortos = Segmentos(puntos, tolerancia);
                segmentos.AddRange(cortos);

                Curve? curva = CurvaSuave(puntos, transformacion.OfVector(Tangente(eje, tramo.Desde)), transformacion.OfVector(Tangente(eje, tramo.Hasta)));
                if (curva != null)
                {
                    geometria.Add(curva);
                    curvas++;
                }
                else
                {
                    geometria.AddRange(cortos);
                }
            }

            //02_Marcas de estacion perpendiculares al eje
            int marcas = 0;
            if (opciones.MarcasCada > 0)
            {
                double primera = Math.Ceiling(eje.EstacionInicial / opciones.MarcasCada) * opciones.MarcasCada;
                for (double estacion = primera; estacion <= eje.EstacionFinal + 1e-6; estacion += opciones.MarcasCada)
                {
                    bool mayor = Math.Abs(Math.IEEERemainder(estacion, 100)) < 1e-6;
                    double mitad = (mayor ? MarcaMayorMetros : MarcaMetros) / 2;
                    XYZ izquierda = transformacion.OfPoint(APies(eje.PuntoDesplazado(estacion, mitad, 0)));
                    XYZ derecha = transformacion.OfPoint(APies(eje.PuntoDesplazado(estacion, -mitad, 0)));
                    Line marca = Line.CreateBound(izquierda, derecha);
                    geometria.Add(marca);
                    segmentos.Add(marca);
                    marcas++;
                }
            }

            //03_Elemento en el modelo
            using (Transaction transaccion = new Transaction(doc, $"Importar eje {eje.Nombre}"))
            {
                transaccion.Start();

                DirectShape forma = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                forma.ApplicationId = "ProyectoDoly";
                forma.ApplicationDataId = eje.Nombre;

                bool suave = forma.IsValidShape(geometria);
                if (!suave && !forma.IsValidShape(segmentos))
                    throw new InvalidOperationException("Revit no aceptó la geometría del eje.");

                forma.SetShape(suave ? geometria : segmentos);
                forma.SetName($"Eje {eje.Nombre}");
                forma.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(Descripcion(eje, opciones));

                //El tablero y las demas herramientas leen el eje desde aqui
                DatosEje.Guardar(forma, eje, transformacion);

                transaccion.Commit();

                return new ResultadoEje
                {
                    Id = forma.Id,
                    Resumen = $"Eje \"{eje.Nombre}\" creado.\n\n" +
                              $"Estaciones: {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)}\n" +
                              $"Longitud: {eje.Horizontal.Longitud:F3} m\n" +
                              (suave
                                  ? $"Tramos: {lineas} rectas y {curvas} curvas continuas, marcas de estación: {marcas}\n"
                                  : $"Tramos: {tramos.Count} (Revit no aceptó curvas suaves: se dibujaron con segmentos de 1 m), marcas: {marcas}\n") +
                              $"Ubicación: {(opciones.Ubicacion == Ubicacion.Compartidas ? "coordenadas compartidas" : "inicio en el origen interno")}"
                };
            }
        }

        //Coordenadas del eje (metros, Este/Norte/Cota) a coordenadas internas de Revit (pies)
        private static Transform Transformacion(Document doc, OpcionesEje opciones)
        {
            if (opciones.Ubicacion == Ubicacion.Origen)
            {
                XYZ inicio = APies(opciones.Eje.Evaluar(opciones.Eje.EstacionInicial).Posicion);
                return Transform.CreateTranslation(new XYZ(-inicio.X, -inicio.Y, 0));
            }

            //Misma transformacion que "Colocar por coordenadas" de ProyectoDoly
            ProjectPosition origen = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            Transform internaACompartida = Transform.CreateTranslation(new XYZ(origen.EastWest, origen.NorthSouth, origen.Elevation))
                .Multiply(Transform.CreateRotation(XYZ.BasisZ, origen.Angle));
            return internaACompartida.Inverse;
        }

        private static void ValidarDistancia(List<XYZ> puntos, Ubicacion ubicacion)
        {
            double maximaPies = UnitUtils.ConvertToInternalUnits(DistanciaMaximaMetros, UnitTypeId.Meters);
            double lejos = puntos.Max(p => new XYZ(p.X, p.Y, 0).GetLength());
            if (lejos <= maximaPies) return;

            string distancia = $"{UnitUtils.ConvertFromInternalUnits(lejos, UnitTypeId.Meters) / 1000:F1} km";
            throw new InvalidOperationException(ubicacion == Ubicacion.Compartidas
                ? $"El eje quedaría a {distancia} del origen interno de Revit.\n\n" +
                  "El proyecto no parece estar georreferenciado. Adquiera las coordenadas desde Civil 3D o el levantamiento " +
                  "(Gestionar > Coordenadas) o importe con la opción \"Inicio del eje en el origen\"."
                : $"El eje mide más de {DistanciaMaximaMetros / 1000:F0} km desde su inicio y Revit no lo admite en un solo modelo.");
        }

        private static string Descripcion(Eje eje, OpcionesEje opciones) =>
            $"Eje | {eje.Nombre} | {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)} | " +
            $"Rasante: {(eje.TieneRasante ? eje.Vertical.Nombre : "ninguna")} | {System.IO.Path.GetFileName(opciones.Archivo)}";

        //Curva suave por los puntos del tramo, con las tangentes del eje en sus extremos para que los tramos empalmen sin quiebre
        private static Curve? CurvaSuave(List<XYZ> puntos, XYZ tangenteInicio, XYZ tangenteFin)
        {
            try
            {
                HermiteSplineTangents tangentes = new HermiteSplineTangents { StartTangent = tangenteInicio, EndTangent = tangenteFin };
                return HermiteSpline.Create(puntos, false, tangentes);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return null;
            }
        }

        private static List<Line> Segmentos(List<XYZ> puntos, double tolerancia)
        {
            List<Line> lineas = new List<Line>();
            XYZ anterior = puntos[0];
            for (int i = 1; i < puntos.Count; i++)
            {
                if (puntos[i].DistanceTo(anterior) < tolerancia) continue;
                lineas.Add(Line.CreateBound(anterior, puntos[i]));
                anterior = puntos[i];
            }

            return lineas;
        }

        //Direccion 3D del eje en una estacion (rumbo en planta y pendiente de la rasante)
        private static XYZ Tangente(Eje eje, double estacion)
        {
            PuntoEje p = eje.Evaluar(estacion);
            return new XYZ(Math.Cos(p.Rumbo), Math.Sin(p.Rumbo), p.Pendiente).Normalize();
        }

        internal static XYZ APies(Punto3 p) => new XYZ(
            UnitUtils.ConvertToInternalUnits(p.X, UnitTypeId.Meters),
            UnitUtils.ConvertToInternalUnits(p.Y, UnitTypeId.Meters),
            UnitUtils.ConvertToInternalUnits(p.Z, UnitTypeId.Meters));
    }
}
