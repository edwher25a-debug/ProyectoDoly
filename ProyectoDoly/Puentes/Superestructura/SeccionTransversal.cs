using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes.Superestructura
{
    /// <summary>
    ///     Contorno cerrado de la seccion en metros. X = hacia la derecha del eje, Y = hacia arriba.
    ///     El origen (0, 0) es el punto del eje sobre la rasante.
    /// </summary>
    public sealed class Contorno
    {
        public Contorno(IEnumerable<Punto2> puntos)
        {
            Puntos = Limpiar(puntos);
            if (Puntos.Count < 3) throw new ArgumentException("Un contorno necesita al menos tres puntos distintos.", nameof(puntos));
        }

        public IReadOnlyList<Punto2> Puntos { get; }

        //Area con signo: positiva si el contorno va en sentido antihorario
        public double AreaConSigno
        {
            get
            {
                double area = 0;
                for (int i = 0; i < Puntos.Count; i++)
                {
                    Punto2 a = Puntos[i];
                    Punto2 b = Puntos[(i + 1) % Puntos.Count];
                    area += a.X * b.Y - b.X * a.Y;
                }

                return area / 2;
            }
        }

        public double Area => Math.Abs(AreaConSigno);

        public Contorno Antihorario() => AreaConSigno >= 0 ? this : new Contorno(Enumerable.Reverse(Puntos));
        public Contorno Horario() => AreaConSigno <= 0 ? this : new Contorno(Enumerable.Reverse(Puntos));

        //Punto dentro del poligono (regla par-impar)
        public bool Contiene(Punto2 p)
        {
            bool dentro = false;
            for (int i = 0, j = Puntos.Count - 1; i < Puntos.Count; j = i++)
            {
                Punto2 a = Puntos[i], b = Puntos[j];
                if (a.Y > p.Y != b.Y > p.Y && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) dentro = !dentro;
            }

            return dentro;
        }

        //Quita el punto de cierre repetido, puntos duplicados y puntos alineados
        private static List<Punto2> Limpiar(IEnumerable<Punto2> puntos)
        {
            const double tolerancia = 1e-6;
            List<Punto2> lista = new List<Punto2>();
            foreach (Punto2 p in puntos)
                if (lista.Count == 0 || p.Distancia(lista[lista.Count - 1]) > tolerancia) lista.Add(p);
            while (lista.Count > 1 && lista[0].Distancia(lista[lista.Count - 1]) <= tolerancia) lista.RemoveAt(lista.Count - 1);

            bool cambio = true;
            while (cambio && lista.Count > 3)
            {
                cambio = false;
                for (int i = 0; i < lista.Count; i++)
                {
                    Punto2 anterior = lista[(i + lista.Count - 1) % lista.Count];
                    Punto2 actual = lista[i];
                    Punto2 siguiente = lista[(i + 1) % lista.Count];
                    double cruz = (actual.X - anterior.X) * (siguiente.Y - actual.Y) - (actual.Y - anterior.Y) * (siguiente.X - actual.X);
                    if (Math.Abs(cruz) > tolerancia * Math.Max(anterior.Distancia(siguiente), 1e-9)) continue;

                    lista.RemoveAt(i);
                    cambio = true;
                    break;
                }
            }

            return lista;
        }
    }

    //Pieza maciza de la seccion: contorno exterior (antihorario) y huecos (horarios)
    public sealed class Pieza
    {
        public Pieza(Contorno exterior, IEnumerable<Contorno>? huecos = null)
        {
            Exterior = exterior.Antihorario();
            Huecos = (huecos ?? Enumerable.Empty<Contorno>()).Select(h => h.Horario()).ToList();
        }

        public Contorno Exterior { get; }
        public IReadOnlyList<Contorno> Huecos { get; }
        public double Area => Exterior.Area - Huecos.Sum(h => h.Area);

        public IEnumerable<Contorno> Contornos => new[] { Exterior }.Concat(Huecos);
    }

    /// <summary>
    ///     Seccion transversal del tablero (por ejemplo, leida de una familia de Revit)
    /// </summary>
    public sealed class SeccionTransversal
    {
        public SeccionTransversal(string nombre, IEnumerable<Pieza> piezas)
        {
            Nombre = nombre;
            Piezas = piezas.ToList();
            if (Piezas.Count == 0) throw new ArgumentException("La sección no tiene ninguna pieza.", nameof(piezas));
        }

        public string Nombre { get; }
        public IReadOnlyList<Pieza> Piezas { get; }
        public double Area => Piezas.Sum(p => p.Area);

        //Misma seccion reflejada respecto al eje (derecha <-> izquierda)
        public SeccionTransversal Espejo()
        {
            Contorno Reflejar(Contorno c) => new Contorno(c.Puntos.Select(p => new Punto2(-p.X, p.Y)));
            return new SeccionTransversal(Nombre, Piezas.Select(p => new Pieza(Reflejar(p.Exterior), p.Huecos.Select(Reflejar))));
        }

        public double MinX =>Piezas.Min(p => p.Exterior.Puntos.Min(q => q.X));
        public double MaxX => Piezas.Max(p => p.Exterior.Puntos.Max(q => q.X));
        public double MinY => Piezas.Min(p => p.Exterior.Puntos.Min(q => q.Y));
        public double MaxY => Piezas.Max(p => p.Exterior.Puntos.Max(q => q.Y));

        /// <summary>
        ///     Arma la seccion a partir de contornos sueltos: los que quedan dentro de otro (en numero impar) son huecos
        ///     de ese otro; los demas son piezas. Sirve para lineas de modelo o caras de solidos de una familia.
        /// </summary>
        public static SeccionTransversal DesdeContornos(string nombre, IEnumerable<Contorno> contornos)
        {
            List<Contorno> lista = contornos.Where(c => c.Area > 1e-8).OrderByDescending(c => c.Area).ToList();
            if (lista.Count == 0) throw new ArgumentException("No hay contornos cerrados con área.", nameof(contornos));

            //Contenedor inmediato de cada contorno: el menor que lo contiene
            int[] padre = new int[lista.Count];
            int[] nivel = new int[lista.Count];
            for (int i = 0; i < lista.Count; i++)
            {
                padre[i] = -1;
                Punto2 muestra = PuntoInterior(lista[i]);
                for (int j = i - 1; j >= 0; j--)
                {
                    if (!lista[j].Contiene(muestra)) continue;
                    padre[i] = j;
                    nivel[i] = nivel[j] + 1;
                    break;
                }
            }

            List<Pieza> piezas = new List<Pieza>();
            for (int i = 0; i < lista.Count; i++)
            {
                if (nivel[i] % 2 != 0) continue;
                IEnumerable<Contorno> huecos = Enumerable.Range(0, lista.Count).Where(k => padre[k] == i && nivel[k] % 2 == 1).Select(k => lista[k]);
                piezas.Add(new Pieza(lista[i], huecos));
            }

            return new SeccionTransversal(nombre, piezas);
        }

        //Punto cercano al primer vertice y dentro del contorno, para no confundir contornos que comparten bordes
        private static Punto2 PuntoInterior(Contorno contorno)
        {
            Contorno ccw = contorno.Antihorario();
            for (int i = 0; i < ccw.Puntos.Count; i++)
            {
                Punto2 a = ccw.Puntos[i];
                Punto2 b = ccw.Puntos[(i + 1) % ccw.Puntos.Count];
                Punto2 medio = (a + b) * 0.5;
                Punto2 borde = b - a;
                double largo = borde.Longitud;
                if (largo < 1e-9) continue;

                //Hacia la izquierda del borde esta el interior en un contorno antihorario
                Punto2 adentro = new Punto2(-borde.Y / largo, borde.X / largo);
                double paso = Math.Min(largo * 0.01, 1e-3);
                Punto2 candidato = medio + adentro * paso;
                if (ccw.Contiene(candidato)) return candidato;
            }

            return ccw.Puntos[0];
        }
    }
}
