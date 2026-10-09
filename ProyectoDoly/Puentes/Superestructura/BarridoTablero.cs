using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes.Superestructura
{
    //Cara plana de la malla: primer lazo exterior, los demas huecos. Vertices ordenados con la normal hacia afuera
    public sealed class Cara
    {
        public Cara(IReadOnlyList<IReadOnlyList<Punto3>> lazos)
        {
            Lazos = lazos;
        }

        public IReadOnlyList<IReadOnlyList<Punto3>> Lazos { get; }
    }

    //Solido cerrado de una pieza de la seccion barrida a lo largo del eje
    public sealed class SolidoBarrido
    {
        public List<Cara> Caras { get; } = new List<Cara>();
    }

    public sealed class OpcionesBarrido
    {
        public double EstacionInicial { get; set; }
        public double EstacionFinal { get; set; }

        //Separacion maxima entre secciones (m). Tambien se pone seccion en cada cambio de tramo y de curva vertical
        public double Paso { get; set; } = 1;

        //Desplazamiento de la seccion respecto al eje: + derecha, + arriba (m)
        public double DesplazamientoLateral { get; set; }
        public double DesplazamientoVertical { get; set; }
    }

    /// <summary>
    ///     Barre la seccion transversal a lo largo del eje. Las secciones quedan verticales y perpendiculares
    ///     al eje en planta (como "Siempre vertical" en Revit), siguiendo la cota de la rasante.
    /// </summary>
    public static class BarridoTablero
    {
        //Distancia minima entre secciones consecutivas: Revit une vertices mas cercanos que ~0.8 mm
        private const double SeparacionMinima = 0.01;

        public static List<double> Estaciones(Eje eje, OpcionesBarrido opciones)
        {
            double desde = Math.Max(opciones.EstacionInicial, eje.EstacionInicial);
            double hasta = Math.Min(opciones.EstacionFinal, eje.EstacionFinal);
            if (hasta - desde < SeparacionMinima)
                throw new ArgumentException("La estación final debe ser mayor que la inicial y quedar dentro del eje.");
            if (opciones.Paso <= 0) throw new ArgumentException("El paso entre secciones debe ser mayor que cero.");

            List<double> estaciones = new List<double> { desde };
            foreach (double e in eje.Estaciones(opciones.Paso))
            {
                if (e <= desde || e >= hasta) continue;
                if (e - estaciones[estaciones.Count - 1] < SeparacionMinima) continue;
                estaciones.Add(e);
            }

            //El final manda: si el ultimo intermedio quedo pegado al final, se quita
            if (hasta - estaciones[estaciones.Count - 1] < SeparacionMinima && estaciones.Count > 1) estaciones.RemoveAt(estaciones.Count - 1);
            estaciones.Add(hasta);

            //Entre estaciones del eje puede quedar un hueco mayor que el paso al recortar el rango
            List<double> resultado = new List<double> { estaciones[0] };
            for (int i = 1; i < estaciones.Count; i++)
            {
                double inicio = resultado[resultado.Count - 1];
                int partes = (int)Math.Ceiling((estaciones[i] - inicio) / opciones.Paso - 1e-9);
                for (int k = 1; k < partes; k++) resultado.Add(inicio + (estaciones[i] - inicio) * k / partes);
                resultado.Add(estaciones[i]);
            }

            return resultado;
        }

        public static List<SolidoBarrido> Generar(Eje eje, SeccionTransversal seccion, OpcionesBarrido opciones)
        {
            List<PuntoEje> secciones = Estaciones(eje, opciones).Select(eje.Evaluar).ToList();
            List<SolidoBarrido> solidos = new List<SolidoBarrido>();

            foreach (Pieza pieza in seccion.Piezas)
            {
                SolidoBarrido solido = new SolidoBarrido();

                //Cada contorno en cada estacion: anillos[contorno][estacion][vertice]
                List<List<Punto3[]>> anillos = pieza.Contornos
                    .Select(c => secciones.Select(p => Ubicar(c, p, opciones)).ToList())
                    .ToList();

                //01_Caras laterales: dos triangulos por borde y tramo, normal hacia afuera
                foreach (List<Punto3[]> anillo in anillos)
                {
                    for (int i = 0; i < anillo.Count - 1; i++)
                    {
                        Punto3[] a = anillo[i];
                        Punto3[] b = anillo[i + 1];
                        for (int j = 0; j < a.Length; j++)
                        {
                            int k = (j + 1) % a.Length;
                            solido.Caras.Add(Triangulo(a[j], b[j], b[k]));
                            solido.Caras.Add(Triangulo(a[j], b[k], a[k]));
                        }
                    }
                }

                //02_Tapas: al inicio la seccion mira hacia atras, al final hacia adelante
                solido.Caras.Add(new Cara(anillos.Select(c => (IReadOnlyList<Punto3>)c[0]).ToList()));
                solido.Caras.Add(new Cara(anillos.Select(c => (IReadOnlyList<Punto3>)c[c.Count - 1].Reverse().ToArray()).ToList()));

                solidos.Add(solido);
            }

            return solidos;
        }

        //Volumen por el teorema de la divergencia (para comprobar la malla)
        public static double Volumen(SolidoBarrido solido)
        {
            double volumen = 0;
            foreach (Cara cara in solido.Caras)
            {
                Punto3 origen = cara.Lazos[0][0];
                foreach (IReadOnlyList<Punto3> lazo in cara.Lazos)
                {
                    //Vector area del lazo
                    double ax = 0, ay = 0, az = 0;
                    for (int i = 0; i < lazo.Count; i++)
                    {
                        Punto3 p = lazo[i], q = lazo[(i + 1) % lazo.Count];
                        ax += p.Y * q.Z - p.Z * q.Y;
                        ay += p.Z * q.X - p.X * q.Z;
                        az += p.X * q.Y - p.Y * q.X;
                    }

                    volumen += (origen.X * ax + origen.Y * ay + origen.Z * az) / 2;
                }
            }

            return volumen / 3;
        }

        //Punto de la seccion (x derecha, y arriba) en coordenadas del eje
        private static Punto3[] Ubicar(Contorno contorno, PuntoEje p, OpcionesBarrido opciones)
        {
            double derechaX = Math.Sin(p.Rumbo);
            double derechaY = -Math.Cos(p.Rumbo);
            return contorno.Puntos.Select(q =>
            {
                double x = q.X + opciones.DesplazamientoLateral;
                double y = q.Y + opciones.DesplazamientoVertical;
                return new Punto3(p.Posicion.X + derechaX * x, p.Posicion.Y + derechaY * x, p.Posicion.Z + y);
            }).ToArray();
        }

        private static Cara Triangulo(Punto3 a, Punto3 b, Punto3 c) => new Cara(new[] { (IReadOnlyList<Punto3>)new[] { a, b, c } });
    }
}
