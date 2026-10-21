using System;
using System.Collections.Generic;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes.Planos
{
    /// <summary>
    ///     Geometria en planta de las rejillas y marcas de estacion (como Grids y Axis Stations de SOFiSTiK)
    /// </summary>
    public static class MarcasPlanta
    {
        /// <summary>
        ///     Estaciones multiplo de "cada" dentro del eje; con extremos, tambien el inicio y el fin del eje
        /// </summary>
        public static List<double> EstacionesCada(Eje eje, double cada, bool extremos)
        {
            if (cada <= 0) throw new ArgumentException("La separación de las estaciones debe ser mayor que cero.");

            double inicio = eje.EstacionInicial, fin = eje.EstacionFinal;
            List<double> estaciones = new List<double>();
            if (extremos) estaciones.Add(inicio);
            for (double e = Math.Ceiling(inicio / cada - 1e-9) * cada; e <= fin + 1e-6; e += cada)
                if (estaciones.Count == 0 || e - estaciones[estaciones.Count - 1] > 1e-3) estaciones.Add(e);
            if (extremos && fin - estaciones[estaciones.Count - 1] > 1e-3) estaciones.Add(fin);
            return estaciones;
        }

        /// <summary>
        ///     Linea transversal al eje en una estacion, girada "giro" grados: "izquierda" metros a la izquierda del eje
        ///     y "derecha" metros a la derecha
        /// </summary>
        public static (Punto2 izquierda, Punto2 derecha) Travesano(Eje eje, double estacion, double giro, double izquierda, double derecha)
        {
            Punto3 p = eje.Evaluar(estacion).Posicion;
            Punto2 centro = new Punto2(p.X, p.Y);
            Punto2 d = PlacementsEje.DireccionPlano(eje, new Placement { Estacion = estacion, Rotacion = giro });
            return (centro + d * izquierda, centro - d * derecha);
        }

        /// <summary>
        ///     Angulo de un texto (radianes) para que se lea de izquierda a derecha o de abajo arriba, nunca al reves
        /// </summary>
        public static double AnguloLegible(double angulo)
        {
            double a = Math.IEEERemainder(angulo, 2 * Math.PI);
            if (a > Math.PI / 2 + 1e-9) a -= Math.PI;
            else if (a <= -Math.PI / 2 + 1e-9) a += Math.PI;
            return a;
        }
    }
}
