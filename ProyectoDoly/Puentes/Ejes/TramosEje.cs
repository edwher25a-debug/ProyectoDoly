using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    //Parte del eje sin cambios de geometria: una recta con pendiente constante se dibuja como linea; lo demas, como curva suave
    public sealed class TramoEje
    {
        public double Desde { get; set; }
        public double Hasta { get; set; }
        public bool EsLinea { get; set; }

        //Puntos de paso para la curva (incluye los extremos); en una linea, solo los extremos
        public List<double> Estaciones { get; set; } = new List<double>();
    }

    /// <summary>
    ///     Divide el eje en sus tramos reales (cambios de recta, curva o clotoide y bordes de las curvas verticales)
    ///     para dibujarlo con una linea o una curva continua por tramo, sin partirlo cada pocos metros.
    /// </summary>
    public static class TramosEje
    {
        public static List<TramoEje> Calcular(Eje eje, double separacionPuntos = 1)
        {
            if (separacionPuntos <= 0) throw new ArgumentOutOfRangeException(nameof(separacionPuntos));

            //Solo los quiebres del eje: un paso mayor que el eje no agrega estaciones intermedias
            List<double> quiebres = eje.Estaciones(eje.Horizontal.Longitud + 1);
            List<TramoEje> tramos = new List<TramoEje>();
            for (int i = 0; i < quiebres.Count - 1; i++)
            {
                double desde = quiebres[i], hasta = quiebres[i + 1];
                double medio = (desde + hasta) / 2;
                bool recta = eje.Horizontal.TipoEn(medio) == TipoElemento.Recta;
                bool pendienteConstante = Math.Abs(eje.Evaluar(desde + 1e-6).Pendiente - eje.Evaluar(hasta - 1e-6).Pendiente) < 1e-9
                                          && Math.Abs(eje.Evaluar(medio).Pendiente - eje.Evaluar(desde + 1e-6).Pendiente) < 1e-9;

                TramoEje tramo = new TramoEje { Desde = desde, Hasta = hasta, EsLinea = recta && pendienteConstante };
                int partes = tramo.EsLinea ? 1 : Math.Max(3, (int)Math.Ceiling((hasta - desde) / separacionPuntos));
                for (int k = 0; k <= partes; k++) tramo.Estaciones.Add(desde + (hasta - desde) * k / partes);
                tramos.Add(tramo);
            }

            return tramos;
        }
    }
}
