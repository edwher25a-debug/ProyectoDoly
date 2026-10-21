using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    /// <summary>
    ///     Puntos con nombre a lo largo del eje (placements): ejes de apoyo, juntas, inicio y fin de tablero.
    ///     Son la referencia para colocar pilas y estribos y para cortar el tablero en vanos.
    /// </summary>
    public static class PlacementsEje
    {
        //Giro maximo del plano respecto a la perpendicular al eje: a 90° el plano quedaria paralelo al eje
        public const double GiroMaximo = 80;

        /// <summary>
        ///     Errores de una lista de placements para el eje (vacia si todo esta bien)
        /// </summary>
        public static List<string> Validar(Eje eje, IReadOnlyList<Placement> placements)
        {
            List<string> errores = new List<string>();
            HashSet<string> usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < placements.Count; i++)
            {
                Placement p = placements[i];
                string nombre = string.IsNullOrWhiteSpace(p.Id) ? $"Fila {i + 1}" : p.Id;

                if (string.IsNullOrWhiteSpace(p.Id)) errores.Add($"{nombre}: falta el ID.");
                else if (!usados.Add(p.Id.Trim())) errores.Add($"{nombre}: el ID está repetido.");

                if (p.Estacion < eje.EstacionInicial - 1e-6 || p.Estacion > eje.EstacionFinal + 1e-6)
                    errores.Add($"{nombre}: la estación {Eje.FormatoEstacion(p.Estacion)} está fuera del eje " +
                                $"({Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)}).");

                if (Math.Abs(p.Rotacion) > GiroMaximo)
                    errores.Add($"{nombre}: el giro debe estar entre -{GiroMaximo:0} y {GiroMaximo:0} grados.");
            }

            return errores;
        }

        /// <summary>
        ///     Placements cada "separacion" metros desde una estacion hasta otra (incluidas), con IDs prefijo + numero
        /// </summary>
        public static List<Placement> Distribuir(double desde, double hasta, double separacion, string prefijo, int primerNumero = 1)
        {
            if (separacion <= 0) throw new ArgumentException("La separación debe ser mayor que cero.", nameof(separacion));
            if (hasta < desde) throw new ArgumentException("La estación final debe ser mayor que la inicial.", nameof(hasta));

            List<Placement> resultado = new List<Placement>();
            int cantidad = (int)Math.Floor((hasta - desde) / separacion + 1e-9);
            for (int k = 0; k <= cantidad; k++)
                resultado.Add(new Placement { Id = $"{prefijo}{primerNumero + k}", Estacion = desde + k * separacion });

            //El final siempre lleva placement aunque no caiga en la separacion exacta
            if (hasta - resultado[resultado.Count - 1].Estacion > 1e-6)
                resultado.Add(new Placement { Id = $"{prefijo}{primerNumero + resultado.Count}", Estacion = hasta });

            return resultado;
        }

        /// <summary>
        ///     Direccion horizontal del plano del placement (hacia la izquierda del eje), ya girada
        /// </summary>
        public static Punto2 DireccionPlano(Eje eje, Placement placement)
        {
            double rumbo = eje.Horizontal.RumboEn(placement.Estacion);
            double angulo = rumbo + Math.PI / 2 + placement.Rotacion * Math.PI / 180;
            return new Punto2(Math.Cos(angulo), Math.Sin(angulo));
        }

        /// <summary>
        ///     Extremos de la marca del placement: linea de "ancho" metros en su plano, centrada en el eje y a la cota de la rasante
        /// </summary>
        public static (Punto3 izquierda, Punto3 derecha) Marca(Eje eje, Placement placement, double ancho)
        {
            Punto3 centro = eje.Evaluar(placement.Estacion).Posicion;
            Punto2 d = DireccionPlano(eje, placement);
            double mitad = ancho / 2;
            return (new Punto3(centro.X + d.X * mitad, centro.Y + d.Y * mitad, centro.Z),
                new Punto3(centro.X - d.X * mitad, centro.Y - d.Y * mitad, centro.Z));
        }

        /// <summary>
        ///     Tramos de tablero entre dos placements (como From/To y el Repeater de SOFiSTiK): uno solo de "desde" a "hasta"
        ///     o uno por vano entre placements consecutivos. "junta" separa vanos contiguos (m, se reparte a cada lado).
        /// </summary>
        public static List<(string nombre, double desde, double hasta)> Vanos(IEnumerable<Placement> placements, string desdeId, string hastaId,
            bool porVano, double junta = 0)
        {
            List<Placement> orden = placements.OrderBy(p => p.Estacion).ToList();
            int i = orden.FindIndex(p => string.Equals(p.Id, desdeId, StringComparison.OrdinalIgnoreCase));
            int j = orden.FindIndex(p => string.Equals(p.Id, hastaId, StringComparison.OrdinalIgnoreCase));
            if (i < 0 || j < 0) throw new ArgumentException("Elija los placements de inicio y fin.");
            if (j <= i) throw new ArgumentException("El placement final debe estar después del inicial.");
            if (junta < 0) throw new ArgumentException("La junta no puede ser negativa.");

            if (!porVano) return new List<(string, double, double)> { ($"{orden[i].Id}-{orden[j].Id}", orden[i].Estacion, orden[j].Estacion) };

            List<(string, double, double)> vanos = new List<(string, double, double)>();
            for (int k = i; k < j; k++)
            {
                double desde = orden[k].Estacion + (k > i ? junta / 2 : 0);
                double hasta = orden[k + 1].Estacion - (k + 1 < j ? junta / 2 : 0);
                if (hasta - desde < 0.01) throw new ArgumentException($"El vano {orden[k].Id}-{orden[k + 1].Id} es más corto que la junta.");
                vanos.Add(($"{orden[k].Id}-{orden[k + 1].Id}", desde, hasta));
            }

            return vanos;
        }

        //Siguiente ID libre con el prefijo dado (P1, P2...)
        public static string SiguienteId(IEnumerable<Placement> placements, string prefijo)
        {
            int mayor = 0;
            foreach (Placement p in placements)
            {
                if (!p.Id.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)) continue;
                if (int.TryParse(p.Id.Substring(prefijo.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) mayor = Math.Max(mayor, n);
            }

            return $"{prefijo}{mayor + 1}";
        }
    }
}
