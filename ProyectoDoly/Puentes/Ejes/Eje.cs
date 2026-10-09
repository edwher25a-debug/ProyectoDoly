using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    //Resultado de evaluar el eje en una estacion
    public readonly struct PuntoEje
    {
        public double Estacion { get; }
        public Punto3 Posicion { get; }
        public double Rumbo { get; }
        public double Pendiente { get; }

        public PuntoEje(double estacion, Punto3 posicion, double rumbo, double pendiente)
        {
            Estacion = estacion;
            Posicion = posicion;
            Rumbo = rumbo;
            Pendiente = pendiente;
        }
    }

    //Punto con nombre a lo largo del eje (ejes de apoyo, juntas, inicio de tablero...)
    public sealed class Placement
    {
        public string Id { get; set; } = "";
        public double Estacion { get; set; }
    }

    //Valor que cambia a lo largo del eje (canto, ancho, espesor...), interpolado linealmente
    public sealed class Variable
    {
        public string Nombre { get; set; } = "";
        public List<(double estacion, double valor)> Valores { get; } = new List<(double, double)>();

        public double ValorEn(double estacion)
        {
            if (Valores.Count == 0) throw new InvalidOperationException($"La variable {Nombre} no tiene valores.");

            List<(double estacion, double valor)> orden = Valores.OrderBy(v => v.estacion).ToList();
            if (estacion <= orden[0].estacion) return orden[0].valor;

            for (int i = 1; i < orden.Count; i++)
            {
                if (estacion > orden[i].estacion) continue;
                double t = (estacion - orden[i - 1].estacion) / (orden[i].estacion - orden[i - 1].estacion);
                return orden[i - 1].valor + t * (orden[i].valor - orden[i - 1].valor);
            }

            return orden[orden.Count - 1].valor;
        }
    }

    /// <summary>
    ///     Eje del puente: alineamiento horizontal + rasante, con placements y variables
    /// </summary>
    public sealed class Eje
    {
        public Eje(string nombre, AlineamientoHorizontal horizontal, PerfilVertical? vertical)
        {
            Nombre = nombre;
            Horizontal = horizontal;
            Vertical = vertical ?? PerfilVertical.Horizontal(horizontal.EstacionInicial, horizontal.EstacionFinal, 0);
            TieneRasante = vertical != null;
        }

        public string Nombre { get; }
        public AlineamientoHorizontal Horizontal { get; }
        public PerfilVertical Vertical { get; }
        public bool TieneRasante { get; }
        public List<Placement> Placements { get; } = new List<Placement>();
        public List<Variable> Variables { get; } = new List<Variable>();

        public double EstacionInicial => Horizontal.EstacionInicial;
        public double EstacionFinal => Horizontal.EstacionFinal;

        public PuntoEje Evaluar(double estacion)
        {
            Punto2 planta = Horizontal.PuntoEn(estacion);
            (double cota, double pendiente) = Vertical.Evaluar(estacion);
            return new PuntoEje(estacion, new Punto3(planta.X, planta.Y, cota), Horizontal.RumboEn(estacion), pendiente);
        }

        //Punto desplazado u metros a la izquierda (perpendicular en planta) y v metros hacia arriba
        public Punto3 PuntoDesplazado(double estacion, double u, double v)
        {
            PuntoEje p = Evaluar(estacion);
            Punto2 izquierda = new Punto2(-Math.Sin(p.Rumbo), Math.Cos(p.Rumbo));
            return new Punto3(p.Posicion.X + izquierda.X * u, p.Posicion.Y + izquierda.Y * u, p.Posicion.Z + v);
        }

        //Estaciones cada "paso" metros, incluyendo extremos, cambios de tramo, PIV y bordes de curvas verticales
        public List<double> Estaciones(double paso)
        {
            if (paso <= 0) throw new ArgumentOutOfRangeException(nameof(paso), "El paso debe ser mayor que cero.");

            SortedSet<double> estaciones = new SortedSet<double> { EstacionInicial, EstacionFinal };
            for (double e = EstacionInicial + paso; e < EstacionFinal; e += paso) estaciones.Add(e);
            for (int i = 0; i < Horizontal.Elementos.Count; i++) estaciones.Add(Horizontal.EstacionInicioDe(i));

            if (TieneRasante)
            {
                foreach (PuntoVertical p in Vertical.Puntos)
                {
                    estaciones.Add(p.Estacion);
                    if (p.LongitudCurva > 0)
                    {
                        estaciones.Add(p.Estacion - p.LongitudCurva / 2);
                        estaciones.Add(p.Estacion + p.LongitudCurva / 2);
                    }
                }
            }

            foreach (Placement p in Placements) estaciones.Add(p.Estacion);

            //Quita duplicados casi iguales y lo que cae fuera del eje
            List<double> resultado = new List<double>();
            foreach (double e in estaciones)
            {
                if (e < EstacionInicial - 1e-9 || e > EstacionFinal + 1e-9) continue;
                if (resultado.Count > 0 && e - resultado[resultado.Count - 1] < 1e-4) continue;
                resultado.Add(e);
            }

            return resultado;
        }

        public List<PuntoEje> Muestrear(double paso) => Estaciones(paso).Select(Evaluar).ToList();

        //Formato vial: 1234.5 -> "1+234.500"
        public static string FormatoEstacion(double estacion)
        {
            double redondeada = Math.Round(estacion, 3);
            string signo = redondeada < 0 ? "-" : "";
            double absoluta = Math.Abs(redondeada);
            int km = (int)Math.Floor(absoluta / 1000 + 1e-9);
            double metros = absoluta - km * 1000;
            return $"{signo}{km}+{metros.ToString("000.000", System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }
}
