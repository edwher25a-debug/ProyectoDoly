using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    //Punto de inflexion del alineamiento: coordenadas (m, Este y Norte), radio de la curva y largo de las clotoides de entrada y salida
    public sealed class PuntoInflexion
    {
        public PuntoInflexion(double x, double y, double radio = 0, double clotoide = 0)
        {
            X = x;
            Y = y;
            Radio = radio;
            Clotoide = clotoide;
        }

        public double X { get; }
        public double Y { get; }

        //0 = vertice sin curva
        public double Radio { get; }
        public double Clotoide { get; }

        public Punto2 Punto => new Punto2(X, Y);
    }

    /// <summary>
    ///     Eje por tabla de PI, como el editor de ejes de SOFiSTiK: en cada vertice intermedio una curva circular de radio R
    ///     con clotoides simetricas de largo Ls (recta, clotoide, arco, clotoide, recta)
    /// </summary>
    public static class EjePorTabla
    {
        public static ResultadoEjeDesdeTramos Crear(IReadOnlyList<PuntoInflexion> pis, OpcionesEjeDesdeTramos opciones)
        {
            if (pis.Count < 2) throw new ArgumentException("La tabla necesita al menos dos PI: el inicio y el fin del eje.");
            for (int i = 1; i < pis.Count; i++)
                if (pis[i].Punto.Distancia(pis[i - 1].Punto) < 1e-6) throw new ArgumentException($"Los PI {i} y {i + 1} están en el mismo punto.");

            List<ElementoHorizontal> elementos = new List<ElementoHorizontal>();
            ResultadoEjeDesdeTramos resultado = new ResultadoEjeDesdeTramos();
            Punto2 actual = pis[0].Punto;
            double estacion = opciones.EstacionInicial;

            for (int i = 1; i < pis.Count - 1; i++)
            {
                PuntoInflexion pi = pis[i];
                double rumboEntrada = Rumbo(pis[i - 1].Punto, pi.Punto);
                double rumboSalida = Rumbo(pi.Punto, pis[i + 1].Punto);
                double delta = Normalizar(rumboSalida - rumboEntrada);
                if (pi.Radio < 0 || pi.Clotoide < 0) throw new ArgumentException($"PI {i + 1}: el radio y la clotoide no pueden ser negativos.");
                if (pi.Clotoide > 0 && pi.Radio <= 0) throw new ArgumentException($"PI {i + 1}: una clotoide necesita un radio.");

                //Sin radio o sin giro: vertice con quiebre (o recta seguida)
                if (pi.Radio <= 0 || Math.Abs(delta) < 1e-9)
                {
                    Recta(elementos, ref actual, ref estacion, pi.Punto, i + 1);
                    if (Math.Abs(delta) * 180 / Math.PI >= EjeDesdeTramos.QuiebreMinimo) resultado.Quiebres.Add((estacion, delta * 180 / Math.PI));
                    continue;
                }

                double signo = Math.Sign(delta);
                double r = pi.Radio;
                double ls = pi.Clotoide;
                double tetaS = ls / (2 * r);
                if (2 * tetaS > Math.Abs(delta) + 1e-9)
                    throw new ArgumentException($"PI {i + 1}: las clotoides de {ls:0.##} m giran más que el ángulo del vértice ({Math.Abs(delta) * 180 / Math.PI:0.##}°). Acórtelas o suba el radio.");

                //Retranqueo p y desplazamiento k de la clotoide: tangente T = (R + p) tan(delta/2) + k
                double xs = ls, ys = 0;
                if (ls > 0)
                {
                    Punto2 finClotoide = new ElementoHorizontal(TipoElemento.Clotoide, new Punto2(0, 0), 0, ls, 0, 1 / r).Fin;
                    xs = finClotoide.X;
                    ys = finClotoide.Y;
                }

                double p = ys - r * (1 - Math.Cos(tetaS));
                double k = xs - r * Math.Sin(tetaS);
                double tangente = (r + p) * Math.Tan(Math.Abs(delta) / 2) + k;
                Punto2 ts = pi.Punto - Punto2.Direccion(rumboEntrada) * tangente;

                if ((ts - actual).X * Math.Cos(rumboEntrada) + (ts - actual).Y * Math.Sin(rumboEntrada) < -1e-3)
                    throw new ArgumentException($"PI {i + 1}: la curva no cabe; se monta sobre la anterior o empieza antes del PI {i}. Baje el radio o las clotoides.");

                Recta(elementos, ref actual, ref estacion, ts, i + 1);
                double rumbo = rumboEntrada;
                double largoArco = r * (Math.Abs(delta) - 2 * tetaS);
                if (ls > 0) Agregar(elementos, ref actual, ref rumbo, ref estacion, TipoElemento.Clotoide, ls, 0, signo / r);
                if (largoArco > 1e-6) Agregar(elementos, ref actual, ref rumbo, ref estacion, TipoElemento.Arco, largoArco, signo / r, signo / r);
                if (ls > 0) Agregar(elementos, ref actual, ref rumbo, ref estacion, TipoElemento.Clotoide, ls, signo / r, 0);
                resultado.Arcos++;
            }

            Recta(elementos, ref actual, ref estacion, pis[pis.Count - 1].Punto, pis.Count);
            resultado.Rectas = elementos.Count(e => e.Tipo == TipoElemento.Recta);

            AlineamientoHorizontal horizontal = new AlineamientoHorizontal(elementos, opciones.EstacionInicial);
            resultado.Eje = new Eje(opciones.Nombre, horizontal, EjeDesdeTramos.Rasante(opciones, horizontal));
            return resultado;
        }

        /// <summary>
        ///     Tabla de PI: una linea por PI, "X; Y" o "X; Y; R" o "X; Y; R; Ls" (m)
        /// </summary>
        public static List<PuntoInflexion> Leer(string texto)
        {
            List<PuntoInflexion> pis = new List<PuntoInflexion>();
            int fila = 0;
            foreach (string linea in texto.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0))
            {
                fila++;
                string[] partes = linea.Split(new[] { ';', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
                double[] v = new double[4];
                bool ok = partes.Length >= 2 && partes.Length <= 4;
                for (int i = 0; ok && i < partes.Length; i++) ok = Superestructura.SeccionVariable.LeerNumero(partes[i], out v[i]);
                if (!ok) throw new ArgumentException($"PI {fila}: \"{linea}\" debe escribirse como X; Y; radio; clotoide (los dos últimos opcionales).");
                pis.Add(new PuntoInflexion(v[0], v[1], v[2], v[3]));
            }

            return pis;
        }

        private static void Recta(List<ElementoHorizontal> elementos, ref Punto2 actual, ref double estacion, Punto2 hasta, int pi)
        {
            Punto2 d = hasta - actual;
            if (d.Longitud < 1e-6) return;
            ElementoHorizontal recta = new ElementoHorizontal(TipoElemento.Recta, actual, Math.Atan2(d.Y, d.X), d.Longitud, 0, 0);
            elementos.Add(recta);
            actual = recta.Fin;
            estacion += recta.Longitud;
        }

        private static void Agregar(List<ElementoHorizontal> elementos, ref Punto2 actual, ref double rumbo, ref double estacion,
            TipoElemento tipo, double largo, double k0, double k1)
        {
            ElementoHorizontal e = new ElementoHorizontal(tipo, actual, rumbo, largo, k0, k1);
            elementos.Add(e);
            actual = e.Fin;
            rumbo = e.RumboFin;
            estacion += largo;
        }

        private static double Rumbo(Punto2 a, Punto2 b) => Math.Atan2(b.Y - a.Y, b.X - a.X);

        private static double Normalizar(double angulo)
        {
            while (angulo > Math.PI) angulo -= 2 * Math.PI;
            while (angulo < -Math.PI) angulo += 2 * Math.PI;
            return angulo;
        }
    }
}
