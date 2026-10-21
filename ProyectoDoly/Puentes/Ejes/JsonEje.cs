using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ProyectoDoly.Puentes.Ejes
{
    /// <summary>
    ///     Eje en JSON abierto para intercambiar con otros programas o proyectos (como el JSON de ejes de SOFiSTiK, con
    ///     formato propio y documentado). Metros; rumbos en radianes desde +X (Este) antihorario; curvatura + = izquierda.
    ///     {
    ///       "formato": "ProyectoDoly eje 1", "nombre": "...", "estacionInicial": 0, "marcasCada": 20,
    ///       "horizontal": [{ "tipo": "Recta|Arco|Clotoide", "x":, "y":, "rumbo":, "longitud":, "curvaturaInicio":, "curvaturaFin": }],
    ///       "rasante": { "nombre": "...", "piv": [{ "estacion":, "cota":, "curva": }] } o null,
    ///       "placements": [{ "id": "P1", "estacion":, "giro": }],
    ///       "variables": [{ "nombre": "...", "valores": [[estacion, valor], ...] }]
    ///     }
    /// </summary>
    public static class JsonEje
    {
        public const string Formato = "ProyectoDoly eje 1";

        public static string Escribir(Eje eje)
        {
            StringBuilder j = new StringBuilder();
            j.AppendLine("{");
            j.AppendLine($"  \"formato\": {Cadena(Formato)},");
            j.AppendLine($"  \"nombre\": {Cadena(eje.Nombre)},");
            j.AppendLine($"  \"estacionInicial\": {Num(eje.EstacionInicial)},");
            j.AppendLine($"  \"marcasCada\": {Num(eje.MarcasCada)},");
            j.AppendLine("  \"horizontal\": [");
            j.AppendLine(string.Join(",\n", eje.Horizontal.Elementos.Select(e =>
                $"    {{ \"tipo\": {Cadena(e.Tipo.ToString())}, \"x\": {Num(e.Inicio.X)}, \"y\": {Num(e.Inicio.Y)}, \"rumbo\": {Num(e.RumboInicio)}, " +
                $"\"longitud\": {Num(e.Longitud)}, \"curvaturaInicio\": {Num(e.CurvaturaInicio)}, \"curvaturaFin\": {Num(e.CurvaturaFin)} }}")));
            j.AppendLine("  ],");

            if (eje.TieneRasante)
            {
                j.AppendLine($"  \"rasante\": {{ \"nombre\": {Cadena(eje.Vertical.Nombre)}, \"piv\": [");
                j.AppendLine(string.Join(",\n", eje.Vertical.Puntos.Select(p =>
                    $"    {{ \"estacion\": {Num(p.Estacion)}, \"cota\": {Num(p.Cota)}, \"curva\": {Num(p.LongitudCurva)} }}")));
                j.AppendLine("  ] },");
            }
            else j.AppendLine("  \"rasante\": null,");

            j.AppendLine("  \"placements\": [");
            j.AppendLine(string.Join(",\n", eje.Placements.Select(p => $"    {{ \"id\": {Cadena(p.Id)}, \"estacion\": {Num(p.Estacion)}, \"giro\": {Num(p.Rotacion)} }}")));
            j.AppendLine("  ],");
            j.AppendLine("  \"variables\": [");
            j.AppendLine(string.Join(",\n", eje.Variables.Select(v =>
                $"    {{ \"nombre\": {Cadena(v.Nombre)}, \"valores\": [{string.Join(", ", v.Valores.Select(x => $"[{Num(x.estacion)}, {Num(x.valor)}]"))}] }}")));
            j.AppendLine("  ]");
            j.Append("}");
            return j.ToString().Replace("\r\n", "\n").Replace("[\n\n", "[\n");
        }

        public static Eje Leer(string texto)
        {
            object? raiz = new Lector(texto).Documento();
            if (!(raiz is Dictionary<string, object?> o)) throw new FormatException("El JSON debe ser un objeto.");
            if (Texto(o, "formato", false) is string f && f.Length > 0 && f != Formato)
                throw new FormatException($"Formato \"{f}\" no reconocido (se esperaba \"{Formato}\").");

            List<ElementoHorizontal> elementos = new List<ElementoHorizontal>();
            foreach (Dictionary<string, object?> e in Lista(o, "horizontal").Cast<Dictionary<string, object?>>())
            {
                if (!Enum.TryParse(Texto(e, "tipo"), true, out TipoElemento tipo)) throw new FormatException($"Tipo de tramo \"{Texto(e, "tipo")}\" desconocido.");
                elementos.Add(new ElementoHorizontal(tipo, new Punto2(Numero(e, "x"), Numero(e, "y")), Numero(e, "rumbo"), Numero(e, "longitud"),
                    Numero(e, "curvaturaInicio", 0), Numero(e, "curvaturaFin", 0)));
            }

            if (elementos.Count == 0) throw new FormatException("El eje no tiene tramos horizontales.");

            PerfilVertical? rasante = null;
            if (o.TryGetValue("rasante", out object? r) && r is Dictionary<string, object?> ro)
                rasante = new PerfilVertical(Texto(ro, "nombre", false) ?? "Rasante",
                    Lista(ro, "piv").Cast<Dictionary<string, object?>>().Select(p => new PuntoVertical(Numero(p, "estacion"), Numero(p, "cota"), Numero(p, "curva", 0))));

            Eje eje = new Eje(Texto(o, "nombre") ?? "", new AlineamientoHorizontal(elementos, Numero(o, "estacionInicial", 0)), rasante)
            {
                MarcasCada = Numero(o, "marcasCada", 20)
            };
            if (o.ContainsKey("placements"))
                foreach (Dictionary<string, object?> p in Lista(o, "placements").Cast<Dictionary<string, object?>>())
                    eje.Placements.Add(new Placement { Id = Texto(p, "id") ?? "", Estacion = Numero(p, "estacion"), Rotacion = Numero(p, "giro", 0) });
            if (o.ContainsKey("variables"))
                foreach (Dictionary<string, object?> v in Lista(o, "variables").Cast<Dictionary<string, object?>>())
                {
                    Variable variable = new Variable { Nombre = Texto(v, "nombre") ?? "" };
                    foreach (List<object?> par in Lista(v, "valores").Cast<List<object?>>())
                        variable.Valores.Add((Convert.ToDouble(par[0], CultureInfo.InvariantCulture), Convert.ToDouble(par[1], CultureInfo.InvariantCulture)));
                    eje.Variables.Add(variable);
                }

            return eje;
        }

        private static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static string Cadena(string s)
        {
            StringBuilder b = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < ' ') b.Append($"\\u{(int)c:x4}");
                        else b.Append(c);
                        break;
                }
            }

            return b.Append('"').ToString();
        }

        private static string? Texto(Dictionary<string, object?> o, string clave, bool obligatorio = true)
        {
            if (o.TryGetValue(clave, out object? v) && v is string s) return s;
            if (obligatorio) throw new FormatException($"Falta el texto \"{clave}\".");
            return null;
        }

        private static double Numero(Dictionary<string, object?> o, string clave, double? omision = null)
        {
            if (o.TryGetValue(clave, out object? v) && v is double d) return d;
            if (omision != null) return omision.Value;
            throw new FormatException($"Falta el número \"{clave}\".");
        }

        private static List<object?> Lista(Dictionary<string, object?> o, string clave) =>
            o.TryGetValue(clave, out object? v) && v is List<object?> l ? l : throw new FormatException($"Falta la lista \"{clave}\".");

        //Lector JSON minimo: objetos, listas, textos, numeros, true/false/null
        private sealed class Lector
        {
            private readonly string t;
            private int i;

            public Lector(string texto)
            {
                t = texto;
            }

            public object? Documento()
            {
                object? v = Valor();
                Blancos();
                if (i < t.Length) Fallo("texto sobrante");
                return v;
            }

            private object? Valor()
            {
                Blancos();
                if (i >= t.Length) Fallo("fin inesperado");
                char c = t[i];
                if (c == '{') return Objeto();
                if (c == '[') return Arreglo();
                if (c == '"') return Cadena();
                if (Palabra("true")) return true;
                if (Palabra("false")) return false;
                if (Palabra("null")) return null;
                return Numero();
            }

            private Dictionary<string, object?> Objeto()
            {
                Dictionary<string, object?> o = new Dictionary<string, object?>(StringComparer.Ordinal);
                i++;
                Blancos();
                if (Siguiente('}')) return o;
                do
                {
                    Blancos();
                    if (i >= t.Length || t[i] != '"') Fallo("se esperaba una clave");
                    string clave = Cadena();
                    Blancos();
                    if (!Siguiente(':')) Fallo("se esperaba ':'");
                    o[clave] = Valor();
                    Blancos();
                } while (Siguiente(','));

                if (!Siguiente('}')) Fallo("se esperaba '}'");
                return o;
            }

            private List<object?> Arreglo()
            {
                List<object?> l = new List<object?>();
                i++;
                Blancos();
                if (Siguiente(']')) return l;
                do
                {
                    l.Add(Valor());
                    Blancos();
                } while (Siguiente(','));

                if (!Siguiente(']')) Fallo("se esperaba ']'");
                return l;
            }

            private string Cadena()
            {
                StringBuilder b = new StringBuilder();
                i++;
                while (i < t.Length && t[i] != '"')
                {
                    char c = t[i++];
                    if (c != '\\')
                    {
                        b.Append(c);
                        continue;
                    }

                    if (i >= t.Length) Fallo("escape incompleto");
                    char e = t[i++];
                    switch (e)
                    {
                        case 'n': b.Append('\n'); break;
                        case 'r': b.Append('\r'); break;
                        case 't': b.Append('\t'); break;
                        case 'b': b.Append('\b'); break;
                        case 'f': b.Append('\f'); break;
                        case 'u':
                            if (i + 4 > t.Length) Fallo("escape \\u incompleto");
                            b.Append((char)int.Parse(t.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: b.Append(e); break;
                    }
                }

                if (!Siguiente('"')) Fallo("texto sin cerrar");
                return b.ToString();
            }

            private double Numero()
            {
                int inicio = i;
                while (i < t.Length && "+-0123456789.eE".IndexOf(t[i]) >= 0) i++;
                if (!double.TryParse(t.Substring(inicio, i - inicio), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) Fallo("número no válido");
                return v;
            }

            private bool Palabra(string p)
            {
                if (string.CompareOrdinal(t, i, p, 0, p.Length) != 0) return false;
                i += p.Length;
                return true;
            }

            private bool Siguiente(char c)
            {
                if (i < t.Length && t[i] == c)
                {
                    i++;
                    return true;
                }

                return false;
            }

            private void Blancos()
            {
                while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
            }

            private void Fallo(string que) => throw new FormatException($"JSON no válido en la posición {i}: {que}.");
        }
    }
}
