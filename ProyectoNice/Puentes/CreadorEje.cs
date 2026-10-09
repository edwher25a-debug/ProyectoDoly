using Autodesk.Revit.DB;
using ProyectoNice.Puentes.Ejes;

namespace ProyectoNice.Puentes
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
        public double Paso { get; set; } = 2;
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

            //01_Polilinea del eje
            List<XYZ> puntos = eje.Muestrear(opciones.Paso)
                .Select(p => transformacion.OfPoint(APies(p.Posicion)))
                .ToList();

            ValidarDistancia(puntos, opciones.Ubicacion);

            List<GeometryObject> geometria = new List<GeometryObject>();
            XYZ anterior = puntos[0];
            for (int i = 1; i < puntos.Count; i++)
            {
                //Puntos mas cercanos que la tolerancia de Revit no forman linea valida
                if (puntos[i].DistanceTo(anterior) < tolerancia) continue;

                geometria.Add(Line.CreateBound(anterior, puntos[i]));
                anterior = puntos[i];
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
                    geometria.Add(Line.CreateBound(izquierda, derecha));
                    marcas++;
                }
            }

            //03_Elemento en el modelo
            using (Transaction transaccion = new Transaction(doc, $"Importar eje {eje.Nombre}"))
            {
                transaccion.Start();

                DirectShape forma = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                forma.ApplicationId = "ProyectoNice";
                forma.ApplicationDataId = eje.Nombre;

                if (!forma.IsValidShape(geometria))
                    throw new InvalidOperationException("Revit no acepto la geometria del eje. Pruebe con un paso de muestreo mayor.");

                forma.SetShape(geometria);
                forma.SetName($"Eje {eje.Nombre}");
                forma.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(Descripcion(eje, opciones));

                transaccion.Commit();

                return new ResultadoEje
                {
                    Id = forma.Id,
                    Resumen = $"Eje \"{eje.Nombre}\" creado.\n\n" +
                              $"Estaciones: {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)}\n" +
                              $"Longitud: {eje.Horizontal.Longitud:F3} m\n" +
                              $"Segmentos: {geometria.Count - marcas}, marcas de estación: {marcas}\n" +
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

            //Misma transformacion que "Colocar por coordenadas" de ProyectoNice
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

        private static XYZ APies(Punto3 p) => new XYZ(
            UnitUtils.ConvertToInternalUnits(p.X, UnitTypeId.Meters),
            UnitUtils.ConvertToInternalUnits(p.Y, UnitTypeId.Meters),
            UnitUtils.ConvertToInternalUnits(p.Z, UnitTypeId.Meters));
    }
}
