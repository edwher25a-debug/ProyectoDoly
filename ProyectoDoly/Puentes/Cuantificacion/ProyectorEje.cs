using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes.Cuantificacion
{
    //Estacion y desfase de un punto en planta respecto al eje (desfase + a la derecha)
    public readonly struct PuntoProyectado
    {
        public double Estacion { get; }
        public double Desfase { get; }
        public double Distancia => Math.Abs(Desfase);

        public PuntoProyectado(double estacion, double desfase)
        {
            Estacion = estacion;
            Desfase = desfase;
        }
    }

    /// <summary>
    ///     Proyecta puntos en planta sobre el eje: busca la muestra mas cercana y afina con seccion aurea
    /// </summary>
    public sealed class ProyectorEje
    {
        private readonly Eje eje;
        private readonly double paso;
        private readonly List<(double estacion, Punto2 punto)> muestras;

        public ProyectorEje(Eje eje, double paso = 2)
        {
            this.eje = eje;
            this.paso = Math.Min(paso, Math.Max(eje.Horizontal.Longitud / 4, 1e-3));
            muestras = eje.Estaciones(this.paso).Select(e => (e, eje.Horizontal.PuntoEn(e))).ToList();
        }

        public PuntoProyectado Proyectar(Punto2 p)
        {
            int cercano = 0;
            double mejor = double.MaxValue;
            for (int i = 0; i < muestras.Count; i++)
            {
                double dx = muestras[i].punto.X - p.X, dy = muestras[i].punto.Y - p.Y;
                double d = dx * dx + dy * dy;
                if (d < mejor)
                {
                    mejor = d;
                    cercano = i;
                }
            }

            double a = Math.Max(eje.EstacionInicial, muestras[cercano].estacion - paso);
            double b = Math.Min(eje.EstacionFinal, muestras[cercano].estacion + paso);
            double Distancia(double e) => eje.Horizontal.PuntoEn(e).Distancia(p);

            const double razon = 0.6180339887498949;
            double c = b - razon * (b - a), d2 = a + razon * (b - a);
            double fc = Distancia(c), fd = Distancia(d2);
            for (int k = 0; k < 60 && b - a > 1e-7; k++)
            {
                if (fc < fd)
                {
                    b = d2;
                    d2 = c;
                    fd = fc;
                    c = b - razon * (b - a);
                    fc = Distancia(c);
                }
                else
                {
                    a = c;
                    c = d2;
                    fc = fd;
                    d2 = a + razon * (b - a);
                    fd = Distancia(d2);
                }
            }

            double estacion = (a + b) / 2;
            Punto2 centro = eje.Horizontal.PuntoEn(estacion);
            double rumbo = eje.Horizontal.RumboEn(estacion);
            Punto2 derecha = new Punto2(Math.Sin(rumbo), -Math.Cos(rumbo));
            Punto2 v = p - centro;
            return new PuntoProyectado(estacion, v.X * derecha.X + v.Y * derecha.Y);
        }
    }

    //Datos de un elemento respecto al eje, para escribirlos en parametros compartidos
    public sealed class DatosCuantificacion
    {
        public double EstacionInicio { get; set; }
        public double EstacionFin { get; set; }
        public double Longitud => EstacionFin - EstacionInicio;
        public double Desfase { get; set; }
        public string Placement { get; set; } = "";
    }

    public static class CuantificacionEje
    {
        /// <summary>
        ///     Tramo de eje que ocupa un elemento (por los puntos de su geometria) y desfase de su centro.
        ///     Placement: el ID si el elemento esta en un placement, o "P1-P2" si empieza y termina en placements.
        /// </summary>
        public static DatosCuantificacion Calcular(ProyectorEje proyector, Eje eje, IReadOnlyList<Punto2> puntos, Punto2 centro, double tolerancia = 0.5)
        {
            if (puntos.Count == 0) throw new ArgumentException("El elemento no tiene geometría.");

            List<PuntoProyectado> proyectados = puntos.Select(proyector.Proyectar).ToList();
            DatosCuantificacion datos = new DatosCuantificacion
            {
                EstacionInicio = proyectados.Min(p => p.Estacion),
                EstacionFin = proyectados.Max(p => p.Estacion),
                Desfase = proyector.Proyectar(centro).Desfase
            };

            Placement? Cerca(double estacion) => eje.Placements.Where(p => Math.Abs(p.Estacion - estacion) <= tolerancia)
                .OrderBy(p => Math.Abs(p.Estacion - estacion)).FirstOrDefault();

            double medio = proyector.Proyectar(centro).Estacion;
            Placement? enCentro = Cerca(medio);
            Placement? inicio = Cerca(datos.EstacionInicio), fin = Cerca(datos.EstacionFin);
            if (inicio != null && fin != null && inicio != fin) datos.Placement = $"{inicio.Id}-{fin.Id}";
            else if (enCentro != null) datos.Placement = enCentro.Id;
            else if (eje.Placements.Count > 0)
            {
                //Entre dos placements: el tramo que lo contiene
                List<Placement> orden = eje.Placements.OrderBy(p => p.Estacion).ToList();
                Placement? antes = orden.LastOrDefault(p => p.Estacion <= medio);
                Placement? despues = orden.FirstOrDefault(p => p.Estacion > medio);
                if (antes != null && despues != null) datos.Placement = $"{antes.Id}-{despues.Id}";
            }

            return datos;
        }
    }
}
