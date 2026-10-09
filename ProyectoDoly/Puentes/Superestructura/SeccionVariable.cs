using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes.Superestructura
{
    /// <summary>
    ///     Parametros de la familia de seccion que cambian a lo largo del tablero (como las variables de SOFiSTiK):
    ///     cada parametro tiene valores en algunas estaciones y entre ellas se interpola linealmente.
    /// </summary>
    public static class SeccionVariable
    {
        /// <summary>
        ///     Lee "estacion = valor" separados por punto y coma o saltos de linea, por ejemplo
        ///     "0+000 = 1.20; 0+040 = 1.80; 0+080 = 1.20". Texto vacio = el parametro no cambia (null).
        /// </summary>
        public static Variable? Leer(string nombre, string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            Variable variable = new Variable { Nombre = nombre };
            foreach (string parte in texto.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(parte)) continue;

                string[] lados = parte.Split('=');
                if (lados.Length != 2 || !LeerNumero(lados[0], out double estacion) || !LeerNumero(lados[1], out double valor))
                    throw new ArgumentException($"{nombre}: \"{parte.Trim()}\" debe escribirse como estación = valor (por ejemplo 0+040 = 1.80).");
                if (variable.Valores.Any(v => Math.Abs(v.estacion - estacion) < 1e-6))
                    throw new ArgumentException($"{nombre}: la estación {Eje.FormatoEstacion(estacion)} está repetida.");

                variable.Valores.Add((estacion, valor));
            }

            if (variable.Valores.Count == 0) return null;
            variable.Valores.Sort((a, b) => a.estacion.CompareTo(b.estacion));
            return variable;
        }

        public static string Texto(Variable variable) =>
            string.Join("; ", variable.Valores.Select(v => $"{Eje.FormatoEstacion(v.estacion)} = {v.valor.ToString("0.####", CultureInfo.InvariantCulture)}"));

        /// <summary>
        ///     Valores de todos los parametros variables en una estacion
        /// </summary>
        public static Dictionary<string, double> ValoresEn(IEnumerable<Variable> variables, double estacion) =>
            variables.ToDictionary(v => v.Nombre, v => v.ValorEn(estacion));

        /// <summary>
        ///     Ordena piezas, huecos y vertices de "seccion" para que se correspondan con "referencia" (la seccion
        ///     de la estacion anterior). Revit puede devolver los contornos en otro orden o empezando en otro vertice
        ///     cuando cambian los parametros; la solevacion necesita el mismo vertice en la misma posicion.
        /// </summary>
        public static SeccionTransversal Alinear(SeccionTransversal seccion, SeccionTransversal referencia)
        {
            if (seccion.Piezas.Count != referencia.Piezas.Count)
                throw new InvalidOperationException(
                    $"La sección cambia de forma: tiene {seccion.Piezas.Count} piezas donde antes tenía {referencia.Piezas.Count}.");

            List<Pieza> libres = seccion.Piezas.ToList();
            List<Pieza> ordenadas = new List<Pieza>();
            foreach (Pieza patron in referencia.Piezas)
            {
                Pieza pieza = libres.OrderBy(p => Centro(p.Exterior).Distancia(Centro(patron.Exterior))).First();
                libres.Remove(pieza);

                if (pieza.Huecos.Count != patron.Huecos.Count)
                    throw new InvalidOperationException("La sección cambia de forma: una pieza gana o pierde huecos.");

                List<Contorno> huecosLibres = pieza.Huecos.ToList();
                List<Contorno> huecos = new List<Contorno>();
                foreach (Contorno hueco in patron.Huecos)
                {
                    Contorno elegido = huecosLibres.OrderBy(h => Centro(h).Distancia(Centro(hueco))).First();
                    huecosLibres.Remove(elegido);
                    huecos.Add(Rotar(elegido, hueco));
                }

                ordenadas.Add(new Pieza(Rotar(pieza.Exterior, patron.Exterior), huecos));
            }

            return new SeccionTransversal(seccion.Nombre, ordenadas);
        }

        //Mismo numero de vertices y el primer vertice donde mejor coincide con el contorno de referencia
        private static Contorno Rotar(Contorno contorno, Contorno referencia)
        {
            int n = contorno.Puntos.Count;
            if (n != referencia.Puntos.Count)
                throw new InvalidOperationException(
                    $"La sección cambia de forma: un contorno pasa de {referencia.Puntos.Count} a {n} vértices. " +
                    "Revise que los valores no hagan desaparecer un borde (por ejemplo, una cartela de 0 m).");

            int mejor = 0;
            double menor = double.MaxValue;
            for (int k = 0; k < n; k++)
            {
                double suma = 0;
                for (int i = 0; i < n; i++)
                {
                    Punto2 d = contorno.Puntos[(i + k) % n] - referencia.Puntos[i];
                    suma += d.X * d.X + d.Y * d.Y;
                }

                if (suma >= menor) continue;
                menor = suma;
                mejor = k;
            }

            return mejor == 0 ? contorno : new Contorno(Enumerable.Range(0, n).Select(i => contorno.Puntos[(i + mejor) % n]));
        }

        private static Punto2 Centro(Contorno contorno) =>
            new Punto2(contorno.Puntos.Average(p => p.X), contorno.Puntos.Average(p => p.Y));

        //Acepta coma o punto decimal y el formato vial 1+234.5
        public static bool LeerNumero(string texto, out double valor)
        {
            valor = 0;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            string limpio = texto.Trim().Replace(',', '.');
            int mas = limpio.IndexOf('+');
            if (mas > 0 && double.TryParse(limpio.Substring(0, mas), NumberStyles.Float, CultureInfo.InvariantCulture, out double km)
                        && double.TryParse(limpio.Substring(mas + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double metros))
            {
                valor = km * 1000 + metros;
                return true;
            }

            return double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }
    }
}
