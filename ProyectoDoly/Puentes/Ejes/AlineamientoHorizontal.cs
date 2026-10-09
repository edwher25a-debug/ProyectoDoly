using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    /// <summary>
    ///     Sucesion de tramos (rectas, arcos y clotoides) con su estacionamiento
    /// </summary>
    public sealed class AlineamientoHorizontal
    {
        private readonly List<ElementoHorizontal> elementos;
        private readonly double[] estacionesInicio;

        public AlineamientoHorizontal(IEnumerable<ElementoHorizontal> elementos, double estacionInicial)
        {
            this.elementos = elementos.ToList();
            if (this.elementos.Count == 0) throw new ArgumentException("El alineamiento no tiene tramos.", nameof(elementos));

            EstacionInicial = estacionInicial;
            estacionesInicio = new double[this.elementos.Count];
            double acumulada = estacionInicial;
            for (int i = 0; i < this.elementos.Count; i++)
            {
                estacionesInicio[i] = acumulada;
                acumulada += this.elementos[i].Longitud;
            }

            EstacionFinal = acumulada;
        }

        public IReadOnlyList<ElementoHorizontal> Elementos => elementos;
        public double EstacionInicial { get; }
        public double EstacionFinal { get; }
        public double Longitud => EstacionFinal - EstacionInicial;

        public double EstacionInicioDe(int indice) => estacionesInicio[indice];

        public Punto2 PuntoEn(double estacion)
        {
            (ElementoHorizontal elemento, double s) = Localizar(estacion);
            return elemento.PuntoEn(s);
        }

        public double RumboEn(double estacion)
        {
            (ElementoHorizontal elemento, double s) = Localizar(estacion);
            return elemento.RumboEn(s);
        }

        //Tramo que contiene la estacion y distancia local dentro de el (se recorta a los extremos)
        public (ElementoHorizontal elemento, double s) Localizar(double estacion)
        {
            if (estacion <= EstacionInicial) return (elementos[0], 0);

            for (int i = 0; i < elementos.Count; i++)
            {
                double fin = estacionesInicio[i] + elementos[i].Longitud;
                if (estacion <= fin) return (elementos[i], estacion - estacionesInicio[i]);
            }

            ElementoHorizontal ultimo = elementos[elementos.Count - 1];
            return (ultimo, ultimo.Longitud);
        }
    }
}
