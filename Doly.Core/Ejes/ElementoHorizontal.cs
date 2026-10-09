using System;

namespace Doly.Core.Ejes
{
    public enum TipoElemento
    {
        Recta,
        Arco,
        Clotoide
    }

    /// <summary>
    ///     Tramo del alineamiento horizontal. Se describe con punto y rumbo iniciales y una curvatura que
    ///     varia linealmente con la longitud: recta (k = 0), arco (k constante) o clotoide (k lineal).
    ///     Curvatura positiva = gira a la izquierda (antihorario).
    /// </summary>
    public sealed class ElementoHorizontal
    {
        public TipoElemento Tipo { get; }
        public Punto2 Inicio { get; }
        public double RumboInicio { get; }
        public double Longitud { get; }
        public double CurvaturaInicio { get; }
        public double CurvaturaFin { get; }

        public ElementoHorizontal(TipoElemento tipo, Punto2 inicio, double rumboInicio, double longitud,
            double curvaturaInicio, double curvaturaFin)
        {
            if (longitud <= 0) throw new ArgumentOutOfRangeException(nameof(longitud), "La longitud del tramo debe ser mayor que cero.");

            Tipo = tipo;
            Inicio = inicio;
            RumboInicio = rumboInicio;
            Longitud = longitud;
            CurvaturaInicio = curvaturaInicio;
            CurvaturaFin = curvaturaFin;
        }

        public Punto2 Fin => PuntoEn(Longitud);
        public double RumboFin => RumboEn(Longitud);

        public double CurvaturaEn(double s) => CurvaturaInicio + (CurvaturaFin - CurvaturaInicio) * s / Longitud;

        public double RumboEn(double s) =>
            RumboInicio + CurvaturaInicio * s + (CurvaturaFin - CurvaturaInicio) * s * s / (2 * Longitud);

        //s = distancia desde el inicio del tramo
        public Punto2 PuntoEn(double s)
        {
            switch (Tipo)
            {
                case TipoElemento.Recta:
                    return Inicio + Punto2.Direccion(RumboInicio) * s;

                case TipoElemento.Arco:
                    double radio = 1 / CurvaturaInicio;
                    Punto2 centro = Inicio + new Punto2(-Math.Sin(RumboInicio), Math.Cos(RumboInicio)) * radio;
                    double rumbo = RumboEn(s);
                    return centro + new Punto2(Math.Sin(rumbo), -Math.Cos(rumbo)) * radio;

                default:
                    return Inicio + IntegrarDireccion(s);
            }
        }

        //Integral de (cos, sin) del rumbo entre 0 y s: Gauss-Legendre de 5 puntos por subtramos de 2 m
        private Punto2 IntegrarDireccion(double s)
        {
            if (s <= 0) return new Punto2(0, 0);

            int subtramos = Math.Max(4, (int)Math.Ceiling(s / 2.0));
            double h = s / subtramos;
            double x = 0, y = 0;

            for (int i = 0; i < subtramos; i++)
            {
                double centro = (i + 0.5) * h;
                for (int j = 0; j < Nodos.Length; j++)
                {
                    double t = centro + Nodos[j] * h / 2;
                    double rumbo = RumboEn(t);
                    x += Pesos[j] * Math.Cos(rumbo);
                    y += Pesos[j] * Math.Sin(rumbo);
                }
            }

            return new Punto2(x * h / 2, y * h / 2);
        }

        private static readonly double[] Nodos =
        {
            0, -0.5384693101056831, 0.5384693101056831, -0.9061798459386640, 0.9061798459386640
        };

        private static readonly double[] Pesos =
        {
            0.5688888888888889, 0.4786286704993665, 0.4786286704993665, 0.2369268850561891, 0.2369268850561891
        };
    }
}
