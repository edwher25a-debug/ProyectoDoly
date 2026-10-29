using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    //Fila del editor de ejes: tipo, longitud y radios (m). Radio + = curva a la derecha, - = a la izquierda; 0 o null = recta (radio infinito)
    public sealed class DatoElemento
    {
        public DatoElemento(TipoElemento tipo, double longitud, double? radioInicio = null, double? radioFin = null)
        {
            Tipo = tipo;
            Longitud = longitud;
            RadioInicio = radioInicio;
            RadioFin = radioFin;
        }

        public TipoElemento Tipo { get; }
        public double Longitud { get; }
        public double? RadioInicio { get; }
        public double? RadioFin { get; }
    }

    /// <summary>
    ///     Eje escrito elemento por elemento, como el cuadro Axis Data de SOFiSTiK: origen, direccion y estacion
    ///     inicial, y una lista de rectas, clotoides y arcos con su longitud y radios
    /// </summary>
    public static class EjePorElementos
    {
        public static AlineamientoHorizontal Crear(Punto2 origen, Punto2 direccion, double estacionInicial, IReadOnlyList<DatoElemento> filas)
        {
            if (filas.Count == 0) throw new ArgumentException("Agregue al menos un elemento (recta, clotoide o arco).");
            if (direccion.Longitud < 1e-9) throw new ArgumentException("La dirección inicial no puede ser (0, 0).");

            List<ElementoHorizontal> elementos = new List<ElementoHorizontal>();
            Punto2 punto = origen;
            double rumbo = Math.Atan2(direccion.Y, direccion.X);
            for (int i = 0; i < filas.Count; i++)
            {
                DatoElemento f = filas[i];
                string fila = $"Elemento {i + 1} ({Nombre(f.Tipo)})";
                if (f.Longitud <= 0) throw new ArgumentException($"{fila}: la longitud debe ser mayor que cero.");

                double k0, k1;
                switch (f.Tipo)
                {
                    case TipoElemento.Recta:
                        k0 = k1 = 0;
                        break;
                    case TipoElemento.Arco:
                        if (!f.RadioInicio.HasValue || Math.Abs(f.RadioInicio.Value) < 1e-9)
                            throw new ArgumentException($"{fila}: escriba el radio (+ derecha, - izquierda).");
                        k0 = k1 = Curvatura(f.RadioInicio);
                        break;
                    default:
                        k0 = Curvatura(f.RadioInicio);
                        k1 = Curvatura(f.RadioFin);
                        if (Math.Abs(k0 - k1) < 1e-12)
                            throw new ArgumentException($"{fila}: el radio inicial y el final deben ser distintos (0 = recta).");
                        break;
                }

                ElementoHorizontal e = new ElementoHorizontal(f.Tipo, punto, rumbo, f.Longitud, k0, k1);
                elementos.Add(e);
                punto = e.Fin;
                rumbo = e.RumboFin;
            }

            return new AlineamientoHorizontal(elementos, estacionInicial);
        }

        /// <summary>
        ///     Filas del editor a partir de un eje ya creado (para editarlo)
        /// </summary>
        public static List<DatoElemento> Filas(AlineamientoHorizontal horizontal) =>
            horizontal.Elementos.Select(e => new DatoElemento(e.Tipo, e.Longitud,
                e.Tipo == TipoElemento.Recta ? (double?)null : Radio(e.CurvaturaInicio),
                e.Tipo == TipoElemento.Clotoide ? Radio(e.CurvaturaFin) : (double?)null)).ToList();

        //Radio con signo (+ derecha) a curvatura (+ izquierda, antihorario)
        public static double Curvatura(double? radio) => !radio.HasValue || Math.Abs(radio.Value) < 1e-9 ? 0 : -1 / radio.Value;

        public static double Radio(double curvatura) => Math.Abs(curvatura) < 1e-12 ? 0 : -1 / curvatura;

        public static string Nombre(TipoElemento tipo) => tipo == TipoElemento.Recta ? "Recta" : tipo == TipoElemento.Arco ? "Arco" : "Clotoide";
    }
}
