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

        //Marca de placement: linea en su plano, mas larga que las de estacion, y un poste vertical en el eje
        private const double PlacementAncho = 12;
        private const double PlacementAlto = 3;

        //Geometria del eje con curvas suaves y, por si Revit no las acepta, la misma con segmentos cortos
        private sealed class GeometriaEje
        {
            public List<GeometryObject> Suave { get; } = new List<GeometryObject>();
            public List<GeometryObject> Segmentos { get; } = new List<GeometryObject>();
            public int Tramos, Lineas, Curvas, Marcas, Placements;

            public void Ambas(GeometryObject objeto)
            {
                Suave.Add(objeto);
                Segmentos.Add(objeto);
            }
        }

        public static ResultadoEje Crear(Document doc, OpcionesEje opciones)
        {
            Eje eje = opciones.Eje;
            eje.MarcasCada = opciones.MarcasCada;
            Transform transformacion = Transformacion(doc, opciones);
            return Crear(doc, eje, transformacion, opciones.Ubicacion, Descripcion(eje, opciones), $"Importar eje {eje.Nombre}",
                $"Ubicación: {(opciones.Ubicacion == Ubicacion.Compartidas ? "coordenadas compartidas" : "inicio en el origen interno")}");
        }

        /// <summary>
        ///     Crea un eje derivado de otro ya importado (eje secundario), con la misma transformacion que el principal
        /// </summary>
        public static ResultadoEje CrearDerivado(Document doc, Eje eje, EjeGuardado principal, string descripcion) =>
            Crear(doc, eje, principal.Transformacion, null, descripcion, $"Eje secundario {eje.Nombre}", $"Eje principal: {principal.Eje.Nombre}");

        private static ResultadoEje Crear(Document doc, Eje eje, Transform transformacion, Ubicacion? ubicacion, string descripcion, string nombreTransaccion, string ultimaLinea)
        {
            GeometriaEje geometria = Dibujar(doc, eje, transformacion, ubicacion);

            using (Transaction transaccion = new Transaction(doc, nombreTransaccion))
            {
                transaccion.Start();

                DirectShape forma = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                forma.ApplicationId = "ProyectoDoly";
                forma.ApplicationDataId = eje.Nombre;

                bool suave = AplicarForma(forma, geometria);
                forma.SetName($"Eje {eje.Nombre}");
                forma.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(descripcion);

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
                                  ? $"Tramos: {geometria.Lineas} rectas y {geometria.Curvas} curvas continuas, marcas de estación: {geometria.Marcas}\n"
                                  : $"Tramos: {geometria.Tramos} (Revit no aceptó curvas suaves: se dibujaron con segmentos de 1 m), marcas: {geometria.Marcas}\n") +
                              ultimaLinea
                };
            }
        }

        /// <summary>
        ///     Vuelve a dibujar un eje ya importado (por ejemplo, tras cambiar sus placements) y guarda sus datos.
        ///     Debe llamarse dentro de una transaccion.
        /// </summary>
        public static void Actualizar(Document doc, EjeGuardado guardado)
        {
            if (!(doc.GetElement(guardado.Id) is DirectShape forma))
                throw new InvalidOperationException("No encontré el elemento del eje en el modelo.");

            GeometriaEje geometria = Dibujar(doc, guardado.Eje, guardado.Transformacion, null);
            AplicarForma(forma, geometria);
            DatosEje.Guardar(forma, guardado.Eje, guardado.Transformacion);
        }

        private static bool AplicarForma(DirectShape forma, GeometriaEje geometria)
        {
            bool suave = forma.IsValidShape(geometria.Suave);
            if (!suave && !forma.IsValidShape(geometria.Segmentos))
                throw new InvalidOperationException("Revit no aceptó la geometría del eje.");

            forma.SetShape(suave ? geometria.Suave : geometria.Segmentos);
            return suave;
        }

        private static GeometriaEje Dibujar(Document doc, Eje eje, Transform transformacion, Ubicacion? ubicacion)
        {
            double tolerancia = doc.Application.ShortCurveTolerance;
            XYZ Revit(Punto3 p) => transformacion.OfPoint(APies(p));

            //01_Eje continuo: una linea por recta con pendiente constante y una curva suave por cada curva, clotoide
            //o curva vertical (sin partirlo cada pocos metros)
            List<TramoEje> tramos = TramosEje.Calcular(eje);
            if (ubicacion != null)
                ValidarDistancia(tramos.SelectMany(t => t.Estaciones).Select(e => Revit(eje.Evaluar(e).Posicion)).ToList(), ubicacion.Value);

            GeometriaEje geometria = new GeometriaEje { Tramos = tramos.Count };
            foreach (TramoEje tramo in tramos)
            {
                List<XYZ> puntos = tramo.Estaciones.Select(e => Revit(eje.Evaluar(e).Posicion)).ToList();
                if (puntos[0].DistanceTo(puntos[puntos.Count - 1]) < tolerancia) continue;

                if (tramo.EsLinea)
                {
                    geometria.Ambas(Line.CreateBound(puntos[0], puntos[puntos.Count - 1]));
                    geometria.Lineas++;
                    continue;
                }

                List<Line> cortos = Segmentos(puntos, tolerancia);
                geometria.Segmentos.AddRange(cortos);

                Curve? curva = CurvaSuave(puntos, transformacion.OfVector(Tangente(eje, tramo.Desde)), transformacion.OfVector(Tangente(eje, tramo.Hasta)));
                if (curva != null)
                {
                    geometria.Suave.Add(curva);
                    geometria.Curvas++;
                }
                else
                {
                    geometria.Suave.AddRange(cortos);
                }
            }

            //02_Marcas de estacion perpendiculares al eje
            if (eje.MarcasCada > 0)
            {
                double primera = Math.Ceiling(eje.EstacionInicial / eje.MarcasCada) * eje.MarcasCada;
                for (double estacion = primera; estacion <= eje.EstacionFinal + 1e-6; estacion += eje.MarcasCada)
                {
                    bool mayor = Math.Abs(Math.IEEERemainder(estacion, 100)) < 1e-6;
                    double mitad = (mayor ? MarcaMayorMetros : MarcaMetros) / 2;
                    geometria.Ambas(Line.CreateBound(Revit(eje.PuntoDesplazado(estacion, mitad, 0)), Revit(eje.PuntoDesplazado(estacion, -mitad, 0))));
                    geometria.Marcas++;
                }
            }

            //03_Placements: linea en el plano del placement (con su giro) y poste vertical
            foreach (Placement placement in eje.Placements)
            {
                (Punto3 izquierda, Punto3 derecha) = PlacementsEje.Marca(eje, placement, PlacementAncho);
                geometria.Ambas(Line.CreateBound(Revit(izquierda), Revit(derecha)));
                Punto3 centro = eje.Evaluar(placement.Estacion).Posicion;
                geometria.Ambas(Line.CreateBound(Revit(centro), Revit(new Punto3(centro.X, centro.Y, centro.Z + PlacementAlto))));
                geometria.Placements++;
            }

            return geometria;
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
