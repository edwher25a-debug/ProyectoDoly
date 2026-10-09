using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    //Punto de inflexion vertical: estacion, cota y longitud de la curva parabolica (0 = sin curva)
    public sealed class PuntoVertical
    {
        public double Estacion { get; }
        public double Cota { get; }
        public double LongitudCurva { get; }

        public PuntoVertical(double estacion, double cota, double longitudCurva = 0)
        {
            Estacion = estacion;
            Cota = cota;
            LongitudCurva = longitudCurva;
        }
    }

    /// <summary>
    ///     Rasante: tangentes entre PIV con curvas parabolicas simetricas en los PIV intermedios
    /// </summary>
    public sealed class PerfilVertical
    {
        private readonly List<PuntoVertical> piv;
        private readonly double[] pendientes;

        public PerfilVertical(string nombre, IEnumerable<PuntoVertical> puntos)
        {
            Nombre = nombre;
            piv = puntos.OrderBy(p => p.Estacion).ToList();
            if (piv.Count < 2) throw new ArgumentException("La rasante necesita al menos dos PIV.", nameof(puntos));

            pendientes = new double[piv.Count - 1];
            for (int i = 0; i < pendientes.Length; i++)
            {
                double largo = piv[i + 1].Estacion - piv[i].Estacion;
                if (largo <= 0) throw new ArgumentException($"Hay dos PIV en la misma estacion ({piv[i].Estacion:F3}).", nameof(puntos));
                pendientes[i] = (piv[i + 1].Cota - piv[i].Cota) / largo;
            }
        }

        public string Nombre { get; }
        public IReadOnlyList<PuntoVertical> Puntos => piv;

        public double CotaEn(double estacion) => Evaluar(estacion).cota;
        public double PendienteEn(double estacion) => Evaluar(estacion).pendiente;

        public (double cota, double pendiente) Evaluar(double estacion)
        {
            //Curva parabolica que contiene la estacion (solo PIV intermedios)
            for (int j = 1; j < piv.Count - 1; j++)
            {
                double largo = piv[j].LongitudCurva;
                if (largo <= 0) continue;

                double inicio = piv[j].Estacion - largo / 2;
                if (estacion < inicio || estacion > inicio + largo) continue;

                double g1 = pendientes[j - 1];
                double g2 = pendientes[j];
                double x = estacion - inicio;
                double cotaInicio = piv[j].Cota - g1 * largo / 2;
                return (cotaInicio + g1 * x + (g2 - g1) * x * x / (2 * largo), g1 + (g2 - g1) * x / largo);
            }

            //Tangente (se extrapola fuera de los extremos)
            int i = 0;
            while (i < pendientes.Length - 1 && estacion > piv[i + 1].Estacion) i++;
            return (piv[i].Cota + pendientes[i] * (estacion - piv[i].Estacion), pendientes[i]);
        }

        //Rasante horizontal a cota constante, para ejes sin perfil
        public static PerfilVertical Horizontal(double estacionInicial, double estacionFinal, double cota) =>
            new PerfilVertical("Sin rasante", new[]
            {
                new PuntoVertical(estacionInicial, cota),
                new PuntoVertical(Math.Max(estacionFinal, estacionInicial + 1), cota)
            });
    }
}
