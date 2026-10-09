using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes.Superestructura
{
    //Vertice de una pieza: contorno (0 = exterior, 1.. = huecos) y posicion dentro de el
    public readonly struct RefVertice
    {
        public RefVertice(int contorno, int vertice)
        {
            Contorno = contorno;
            Vertice = vertice;
        }

        public int Contorno { get; }
        public int Vertice { get; }
    }

    /// <summary>
    ///     Divide una pieza (exterior con huecos) en triangulos antihorarios. Revit falla con caras de muchos
    ///     vertices, concavas o con huecos; con triangulos la tapa siempre es valida.
    ///     Metodo: los huecos se unen al exterior con un puente y el poligono resultante se recorta por orejas.
    /// </summary>
    public static class Triangulacion
    {
        public static List<(RefVertice a, RefVertice b, RefVertice c)> Triangular(Pieza pieza)
        {
            List<Contorno> contornos = pieza.Contornos.ToList();
            Punto2 P(RefVertice r) => contornos[r.Contorno].Puntos[r.Vertice];

            List<RefVertice> poligono = Enumerable.Range(0, pieza.Exterior.Puntos.Count).Select(v => new RefVertice(0, v)).ToList();

            //01_Huecos unidos al exterior, empezando por el que llega mas a la derecha
            List<int> pendientes = Enumerable.Range(1, contornos.Count - 1)
                .OrderByDescending(h => contornos[h].Puntos.Max(p => p.X)).ToList();
            while (pendientes.Count > 0)
            {
                int hueco = pendientes[0];
                pendientes.RemoveAt(0);

                Contorno contorno = contornos[hueco];
                int m = Enumerable.Range(0, contorno.Puntos.Count).OrderByDescending(v => contorno.Puntos[v].X).First();
                Punto2 pm = contorno.Puntos[m];

                //Vertice del poligono visible desde M: el puente no puede cruzar ningun borde
                List<(Punto2, Punto2)> bordes = Bordes(poligono.Select(P).ToList());
                foreach (int otro in pendientes.Append(hueco)) bordes.AddRange(Bordes(contornos[otro].Puntos.ToList()));

                int puente = Enumerable.Range(0, poligono.Count)
                    .OrderBy(i => P(poligono[i]).Distancia(pm))
                    .FirstOrDefault(i => Visible(pm, P(poligono[i]), bordes));

                //poligono: ... P, M, (vuelta al hueco), M, P, ...
                List<RefVertice> vuelta = Enumerable.Range(0, contorno.Puntos.Count + 1)
                    .Select(k => new RefVertice(hueco, (m + k) % contorno.Puntos.Count)).ToList();
                vuelta.Add(poligono[puente]);
                poligono.InsertRange(puente + 1, vuelta);
            }

            //02_Recorte de orejas
            List<(RefVertice, RefVertice, RefVertice)> triangulos = new List<(RefVertice, RefVertice, RefVertice)>();
            List<RefVertice> restantes = new List<RefVertice>(poligono);
            int sinOreja = 0;
            int i0 = 0;
            while (restantes.Count > 3)
            {
                int n = restantes.Count;
                int ia = (i0 + n - 1) % n, ib = i0 % n, ic = (i0 + 1) % n;
                Punto2 a = P(restantes[ia]), b = P(restantes[ib]), c = P(restantes[ic]);

                bool oreja = Cruz(a, b, c) > 1e-12 && !restantes.Where((r, k) => k != ia && k != ib && k != ic)
                    .Select(P).Any(p => !Igual(p, a) && !Igual(p, b) && !Igual(p, c) && Dentro(p, a, b, c));

                //Si tras una vuelta completa no hay oreja (poligono degenerado), se corta igual para no quedar en bucle
                if (oreja || sinOreja > n)
                {
                    if (Math.Abs(Cruz(a, b, c)) > 1e-12) triangulos.Add((restantes[ia], restantes[ib], restantes[ic]));
                    restantes.RemoveAt(ib);
                    sinOreja = 0;
                    i0 = ib % restantes.Count;
                }
                else
                {
                    sinOreja++;
                    i0 = (i0 + 1) % n;
                }
            }

            if (Math.Abs(Cruz(P(restantes[0]), P(restantes[1]), P(restantes[2]))) > 1e-12)
                triangulos.Add((restantes[0], restantes[1], restantes[2]));

            return triangulos;
        }

        private static List<(Punto2, Punto2)> Bordes(List<Punto2> puntos) =>
            puntos.Select((p, k) => (p, puntos[(k + 1) % puntos.Count])).ToList();

        private static bool Visible(Punto2 desde, Punto2 hasta, List<(Punto2 a, Punto2 b)> bordes) =>
            !bordes.Any(e => !Igual(e.a, desde) && !Igual(e.b, desde) && !Igual(e.a, hasta) && !Igual(e.b, hasta)
                             && SeCortan(desde, hasta, e.a, e.b));

        private static bool SeCortan(Punto2 p1, Punto2 p2, Punto2 q1, Punto2 q2)
        {
            double d1 = Cruz(q1, q2, p1), d2 = Cruz(q1, q2, p2), d3 = Cruz(p1, p2, q1), d4 = Cruz(p1, p2, q2);
            return d1 * d2 < 0 && d3 * d4 < 0;
        }

        private static double Cruz(Punto2 a, Punto2 b, Punto2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        private static bool Dentro(Punto2 p, Punto2 a, Punto2 b, Punto2 c) =>
            Cruz(a, b, p) >= 0 && Cruz(b, c, p) >= 0 && Cruz(c, a, p) >= 0;

        private static bool Igual(Punto2 a, Punto2 b) => a.Distancia(b) < 1e-9;
    }
}
