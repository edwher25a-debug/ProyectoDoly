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

        //Estaciones donde tambien debe haber seccion (por ejemplo, los puntos de los parametros variables)
        public List<double> EstacionesExtra { get; set; } = new List<double>();
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
            foreach (double e in eje.Estaciones(opciones.Paso).Concat(opciones.EstacionesExtra).OrderBy(e => e))
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

        public static List<SolidoBarrido> Generar(Eje eje, SeccionTransversal seccion, OpcionesBarrido opciones) =>
            Generar(eje, Estaciones(eje, opciones).Select(_ => seccion).ToList(), opciones);

        /// <summary>
        ///     Barrido con una seccion por estacion (parametros variables). Las secciones deben tener la misma forma
        ///     (piezas, huecos y vertices en el mismo orden, ver SeccionVariable.Alinear).
        /// </summary>
        public static List<SolidoBarrido> Generar(Eje eje, IReadOnlyList<SeccionTransversal> porEstacion, OpcionesBarrido opciones)
        {
            List<PuntoEje> secciones = Estaciones(eje, opciones).Select(eje.Evaluar).ToList();
            ComprobarCantidad(secciones.Count, porEstacion);
            List<SolidoBarrido> solidos = new List<SolidoBarrido>();

            for (int indice = 0; indice < porEstacion[0].Piezas.Count; indice++)
            {
                SolidoBarrido solido = new SolidoBarrido();
                int contornos = porEstacion[0].Piezas[indice].Contornos.Count();

                //Cada contorno en cada estacion: anillos[contorno][estacion][vertice]
                List<List<Punto3[]>> anillos = Enumerable.Range(0, contornos)
                    .Select(c => secciones.Select((p, e) => Ubicar(porEstacion[e].Piezas[indice].Contornos.ElementAt(c), p, opciones)).ToList())
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

                //02_Tapas en triangulos: al inicio la seccion mira hacia atras, al final hacia adelante
                int ultima = secciones.Count - 1;
                foreach ((RefVertice a, RefVertice b, RefVertice c) in Triangulacion.Triangular(porEstacion[0].Piezas[indice]))
                    solido.Caras.Add(Triangulo(Punto(anillos, a, 0), Punto(anillos, b, 0), Punto(anillos, c, 0)));
                foreach ((RefVertice a, RefVertice b, RefVertice c) in Triangulacion.Triangular(porEstacion[ultima].Piezas[indice]))
                    solido.Caras.Add(Triangulo(Punto(anillos, a, ultima), Punto(anillos, c, ultima), Punto(anillos, b, ultima)));

                solidos.Add(solido);
            }

            return solidos;
        }

        /// <summary>
        ///     Puntos de la seccion en cada estacion, para colocar un ejemplar adaptativo por tramo:
        ///     resultado[estacion] = vertices de todas las piezas y contornos, siempre en el mismo orden
        ///     (pieza 1 exterior, pieza 1 huecos, pieza 2 exterior...). Un tramo usa las estaciones k y k + 1.
        /// </summary>
        public static List<Punto3[]> PuntosPorEstacion(Eje eje, SeccionTransversal seccion, OpcionesBarrido opciones) =>
            PuntosPorEstacion(eje, Estaciones(eje, opciones).Select(_ => seccion).ToList(), opciones);

        public static List<Punto3[]> PuntosPorEstacion(Eje eje, IReadOnlyList<SeccionTransversal> porEstacion, OpcionesBarrido opciones)
        {
            List<PuntoEje> secciones = Estaciones(eje, opciones).Select(eje.Evaluar).ToList();
            ComprobarCantidad(secciones.Count, porEstacion);
            return secciones
                .Select((p, e) => porEstacion[e].Piezas.SelectMany(pieza => pieza.Contornos).SelectMany(c => Ubicar(c, p, opciones)).ToArray())
                .ToList();
        }

        private static void ComprobarCantidad(int estaciones, IReadOnlyList<SeccionTransversal> porEstacion)
        {
            if (porEstacion.Count != estaciones)
                throw new ArgumentException($"Hay {porEstacion.Count} secciones para {estaciones} estaciones.", nameof(porEstacion));
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

        private static Punto3 Punto(List<List<Punto3[]>> anillos, RefVertice r, int estacion) => anillos[r.Contorno][estacion][r.Vertice];

        private static Cara Triangulo(Punto3 a, Punto3 b, Punto3 c) => new Cara(new[] { (IReadOnlyList<Punto3>)new[] { a, b, c } });
    }
}
