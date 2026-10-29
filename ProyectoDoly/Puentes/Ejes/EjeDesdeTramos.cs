using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    //Recta o arco dibujado en el modelo, en planta y en metros, en el sentido en que se dibujo
    public sealed class TramoDibujado
    {
        public Punto2 Inicio { get; set; }
        public Punto2 Fin { get; set; }

        //Solo arcos: centro y sentido de giro de Inicio a Fin
        public bool EsArco { get; set; }
        public Punto2 Centro { get; set; }
        public bool Antihorario { get; set; }

        public static TramoDibujado Recta(Punto2 inicio, Punto2 fin) => new TramoDibujado { Inicio = inicio, Fin = fin };

        public static TramoDibujado Arco(Punto2 inicio, Punto2 fin, Punto2 centro, bool antihorario) =>
            new TramoDibujado { Inicio = inicio, Fin = fin, EsArco = true, Centro = centro, Antihorario = antihorario };

        public TramoDibujado Invertido() => new TramoDibujado { Inicio = Fin, Fin = Inicio, EsArco = EsArco, Centro = Centro, Antihorario = !Antihorario };
    }

    public sealed class OpcionesEjeDesdeTramos
    {
        public string Nombre { get; set; } = "Eje";
        public double EstacionInicial { get; set; }

        //Distancia maxima entre extremos que se consideran unidos (m)
        public double Tolerancia { get; set; } = 0.01;

        //Rasante: tabla de PIV (estacion, cota, longitud de curva) o, si esta vacia, cota inicial y pendiente constante
        public List<PuntoVertical> Piv { get; } = new List<PuntoVertical>();
        public double CotaInicial { get; set; }
        public double PendientePorciento { get; set; }

        //Punto cerca del que debe empezar el eje (por ejemplo, el inicio de la primera linea elegida); null = cualquier extremo libre
        public Punto2? CercaDe { get; set; }
    }

    public sealed class ResultadoEjeDesdeTramos
    {
        public Eje Eje { get; set; } = null!;
        public int Rectas { get; set; }
        public int Arcos { get; set; }

        //Uniones donde el rumbo cambia de golpe (quiebres), en grados
        public List<(double estacion, double grados)> Quiebres { get; } = new List<(double, double)>();
    }

    /// <summary>
    ///     Crea un eje a partir de rectas y arcos dibujados en Revit (como el eje dibujado de SOFiSTiK, sin LandXML):
    ///     los encadena por sus extremos, en cualquier orden y sentido, y les pone rasante
    /// </summary>
    public static class EjeDesdeTramos
    {
        //Cambio de rumbo en una union a partir del cual se avisa (grados)
        public const double QuiebreMinimo = 0.5;

        public static ResultadoEjeDesdeTramos Crear(IReadOnlyList<TramoDibujado> tramos, OpcionesEjeDesdeTramos opciones)
        {
            List<TramoDibujado> cadena = Encadenar(tramos, opciones.Tolerancia, opciones.CercaDe);

            List<ElementoHorizontal> elementos = new List<ElementoHorizontal>();
            ResultadoEjeDesdeTramos resultado = new ResultadoEjeDesdeTramos();
            double estacion = opciones.EstacionInicial;
            foreach (TramoDibujado tramo in cadena)
            {
                ElementoHorizontal elemento = Elemento(tramo);
                if (elementos.Count > 0)
                {
                    double giro = Normalizar(elemento.RumboInicio - elementos[elementos.Count - 1].RumboFin) * 180 / Math.PI;
                    if (Math.Abs(giro) >= QuiebreMinimo) resultado.Quiebres.Add((estacion, giro));
                }

                elementos.Add(elemento);
                estacion += elemento.Longitud;
                if (tramo.EsArco) resultado.Arcos++;
                else resultado.Rectas++;
            }

            AlineamientoHorizontal horizontal = new AlineamientoHorizontal(elementos, opciones.EstacionInicial);
            resultado.Eje = new Eje(opciones.Nombre, horizontal, Rasante(opciones, horizontal));
            return resultado;
        }

        /// <summary>
        ///     Ordena los tramos en una sola cadena, dandoles la vuelta cuando hace falta. Falla si hay huecos,
        ///     ramificaciones o piezas sueltas, diciendo donde.
        /// </summary>
        public static List<TramoDibujado> Encadenar(IReadOnlyList<TramoDibujado> tramos, double tolerancia, Punto2? cercaDe = null)
        {
            if (tramos.Count == 0) throw new ArgumentException("Elija al menos una línea o un arco.");

            //Extremos agrupados por cercania: cada nudo cuenta cuantos tramos llegan a el
            List<Punto2> nudos = new List<Punto2>();
            int Nudo(Punto2 p)
            {
                for (int i = 0; i < nudos.Count; i++)
                    if (nudos[i].Distancia(p) <= tolerancia) return i;
                nudos.Add(p);
                return nudos.Count - 1;
            }

            List<(int a, int b)> uniones = tramos.Select(t => (Nudo(t.Inicio), Nudo(t.Fin))).ToList();
            int[] grado = new int[nudos.Count];
            foreach ((int a, int b) in uniones)
            {
                grado[a]++;
                grado[b]++;
            }

            int ramas = Array.FindIndex(grado, g => g > 2);
            if (ramas >= 0)
                throw new ArgumentException($"En el punto {Texto(nudos[ramas])} se juntan {grado[ramas]} líneas. El eje debe ser una sola cadena sin ramales.");

            List<int> libres = Enumerable.Range(0, nudos.Count).Where(i => grado[i] == 1).ToList();
            int inicio;
            if (libres.Count == 0)
                inicio = uniones[0].a; //Cadena cerrada: empieza en el inicio del primer tramo
            else if (libres.Count == 2)
                inicio = cercaDe.HasValue && nudos[libres[1]].Distancia(cercaDe.Value) < nudos[libres[0]].Distancia(cercaDe.Value) ? libres[1] : libres[0];
            else
                throw new ArgumentException(
                    $"Las líneas no forman una sola cadena: hay {libres.Count} extremos sueltos ({string.Join(", ", libres.Take(4).Select(i => Texto(nudos[i])))}). " +
                    $"Revise que se toquen (tolerancia {tolerancia * 1000:0.#} mm) o elija solo las del eje.");

            List<TramoDibujado> cadena = new List<TramoDibujado>();
            bool[] usado = new bool[tramos.Count];
            int actual = inicio;
            for (int paso = 0; paso < tramos.Count; paso++)
            {
                int siguiente = -1;
                for (int i = 0; i < tramos.Count && siguiente < 0; i++)
                    if (!usado[i] && (uniones[i].a == actual || uniones[i].b == actual)) siguiente = i;
                if (siguiente < 0) break;

                usado[siguiente] = true;
                bool alReves = uniones[siguiente].a != actual;
                cadena.Add(alReves ? tramos[siguiente].Invertido() : tramos[siguiente]);
                actual = alReves ? uniones[siguiente].a : uniones[siguiente].b;
            }

            if (cadena.Count < tramos.Count)
                throw new ArgumentException($"{tramos.Count - cadena.Count} de las {tramos.Count} líneas no se tocan con las demás. Elija solo las del eje.");
            return cadena;
        }

        private static ElementoHorizontal Elemento(TramoDibujado tramo)
        {
            Punto2 cuerda = tramo.Fin - tramo.Inicio;
            if (!tramo.EsArco)
            {
                if (cuerda.Longitud < 1e-6) throw new ArgumentException($"Hay una línea de longitud cero en {Texto(tramo.Inicio)}.");
                return new ElementoHorizontal(TipoElemento.Recta, tramo.Inicio, Math.Atan2(cuerda.Y, cuerda.X), cuerda.Longitud, 0, 0);
            }

            Punto2 radioInicio = tramo.Inicio - tramo.Centro;
            Punto2 radioFin = tramo.Fin - tramo.Centro;
            double radio = radioInicio.Longitud;
            double a0 = Math.Atan2(radioInicio.Y, radioInicio.X);
            double a1 = Math.Atan2(radioFin.Y, radioFin.X);

            //Angulo barrido en el sentido del arco (0 a 2 pi)
            double barrido = tramo.Antihorario ? a1 - a0 : a0 - a1;
            while (barrido <= 1e-12) barrido += 2 * Math.PI;
            while (barrido > 2 * Math.PI) barrido -= 2 * Math.PI;

            double signo = tramo.Antihorario ? 1 : -1;
            double rumbo = a0 + signo * Math.PI / 2;
            return new ElementoHorizontal(TipoElemento.Arco, tramo.Inicio, rumbo, radio * barrido, signo / radio, signo / radio);
        }

        public static PerfilVertical Rasante(OpcionesEjeDesdeTramos opciones, AlineamientoHorizontal horizontal)
        {
            if (opciones.Piv.Count >= 2) return new PerfilVertical("Rasante", opciones.Piv);
            if (opciones.Piv.Count == 1) throw new ArgumentException("La tabla de rasante necesita al menos dos PIV (o déjela vacía y use cota y pendiente).");

            double pendiente = opciones.PendientePorciento / 100;
            return new PerfilVertical("Rasante", new[]
            {
                new PuntoVertical(horizontal.EstacionInicial, opciones.CotaInicial),
                new PuntoVertical(horizontal.EstacionFinal, opciones.CotaInicial + pendiente * horizontal.Longitud)
            });
        }

        /// <summary>
        ///     Tabla de PIV: una linea o punto y coma por PIV, "estacion = cota" o "estacion = cota / longitud de curva"
        ///     (0+000 = 545.30; 0+120 = 548 / 80; 0+300 = 546.5)
        /// </summary>
        public static List<PuntoVertical> LeerPiv(string texto)
        {
            List<PuntoVertical> piv = new List<PuntoVertical>();
            foreach (string parte in texto.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                string[] lados = parte.Split('=');
                string[] derecha = lados.Length == 2 ? lados[1].Split('/') : Array.Empty<string>();
                double curva = 0;
                if (lados.Length != 2 || derecha.Length > 2 || !Superestructura.SeccionVariable.LeerNumero(lados[0], out double estacion)
                    || !Superestructura.SeccionVariable.LeerNumero(derecha[0], out double cota)
                    || (derecha.Length == 2 && (!Superestructura.SeccionVariable.LeerNumero(derecha[1], out curva) || curva < 0)))
                    throw new ArgumentException($"Rasante: \"{parte}\" debe escribirse como estación = cota, o estación = cota / longitud de curva (0+120 = 548 / 80).");
                if (piv.Any(p => Math.Abs(p.Estacion - estacion) < 1e-6))
                    throw new ArgumentException($"Rasante: la estación {Eje.FormatoEstacion(estacion)} está repetida.");
                piv.Add(new PuntoVertical(estacion, cota, curva));
            }

            return piv.OrderBy(p => p.Estacion).ToList();
        }

        private static double Normalizar(double angulo)
        {
            while (angulo > Math.PI) angulo -= 2 * Math.PI;
            while (angulo < -Math.PI) angulo += 2 * Math.PI;
            return angulo;
        }

        private static string Texto(Punto2 p) => $"({p.X:F2}; {p.Y:F2})";
    }
}
