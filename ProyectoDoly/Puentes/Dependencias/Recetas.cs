using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Superestructura;

namespace ProyectoDoly.Puentes.Dependencias
{
    /// <summary>
    ///     Como se creo un elemento atado a un eje (tablero, pila, eje secundario), para volver a crearlo o moverlo
    ///     cuando el eje cambia. Se guarda en texto dentro del elemento: una linea "clave|valor" por dato.
    /// </summary>
    public sealed class Receta
    {
        private const string Version = "DOLY-DEP 1";
        private readonly Dictionary<string, string> valores = new Dictionary<string, string>();

        public Receta(string tipo)
        {
            Tipo = tipo;
        }

        public string Tipo { get; }

        public string Texto(string clave, string defecto = "") => valores.TryGetValue(clave, out string? v) ? v : defecto;
        public double Numero(string clave, double defecto = 0) => valores.TryGetValue(clave, out string? v) ? double.Parse(v, CultureInfo.InvariantCulture) : defecto;
        public bool SiNo(string clave, bool defecto = false) => valores.TryGetValue(clave, out string? v) ? v == "1" : defecto;
        public bool Tiene(string clave) => valores.ContainsKey(clave);

        public Receta Con(string clave, string? valor)
        {
            if (valor != null) valores[clave] = valor;
            return this;
        }

        public Receta Con(string clave, double valor) => Con(clave, Num(valor));
        public Receta Con(string clave, bool valor) => Con(clave, valor ? "1" : "0");

        public string Escribir()
        {
            StringBuilder texto = new StringBuilder();
            texto.AppendLine(Version);
            texto.AppendLine($"TIPO|{Uri.EscapeDataString(Tipo)}");
            foreach (KeyValuePair<string, string> v in valores.OrderBy(v => v.Key, StringComparer.Ordinal))
                texto.AppendLine($"{Uri.EscapeDataString(v.Key)}|{Uri.EscapeDataString(v.Value)}");
            return texto.ToString();
        }

        public static Receta Leer(string texto)
        {
            string[] lineas = texto.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lineas.Length < 2 || lineas[0] != Version || !lineas[1].StartsWith("TIPO|", StringComparison.Ordinal))
                throw new FormatException("La receta del elemento no tiene un formato conocido.");

            Receta receta = new Receta(Uri.UnescapeDataString(lineas[1].Substring(5)));
            foreach (string linea in lineas.Skip(2))
            {
                int barra = linea.IndexOf('|');
                if (barra > 0) receta.valores[Uri.UnescapeDataString(linea.Substring(0, barra))] = Uri.UnescapeDataString(linea.Substring(barra + 1));
            }

            return receta;
        }

        public static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        //Lista de numeros separados por espacios
        public static string Lista(IEnumerable<double> valores) => string.Join(" ", valores.Select(Num));

        public static List<double> Lista(string texto) =>
            texto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToList();

        //Tabla estacion=valor separada por espacios
        public static string Tabla(IEnumerable<(double estacion, double valor)> tabla) => string.Join(" ", tabla.Select(t => $"{Num(t.estacion)}={Num(t.valor)}"));

        public static List<(double estacion, double valor)> Tabla(string texto) =>
            texto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('='))
                .Select(p => (double.Parse(p[0], CultureInfo.InvariantCulture), double.Parse(p[1], CultureInfo.InvariantCulture)))
                .ToList();

        //Seccion: piezas separadas por "/", contornos de cada pieza por ";" (el primero es el exterior), puntos "x,y" por espacios
        public static string Seccion(SeccionTransversal seccion) =>
            string.Join("/", seccion.Piezas.Select(p => string.Join(";", p.Contornos.Select(c => string.Join(" ", c.Puntos.Select(q => $"{Num(q.X)},{Num(q.Y)}"))))));

        public static SeccionTransversal Seccion(string nombre, string texto)
        {
            Contorno Contorno(string c) => new Contorno(c.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split(','))
                .Select(p => new Punto2(double.Parse(p[0], CultureInfo.InvariantCulture), double.Parse(p[1], CultureInfo.InvariantCulture))));

            return new SeccionTransversal(nombre, texto.Split('/').Select(p =>
            {
                List<Contorno> contornos = p.Split(';').Select(Contorno).ToList();
                return new Pieza(contornos[0], contornos.Skip(1));
            }));
        }

        //Variables: "nombre:e=v e=v" separadas por "/"
        public static string Variables(IEnumerable<Variable> variables) =>
            string.Join("/", variables.Select(v => $"{Uri.EscapeDataString(v.Nombre)}:{Tabla(v.Valores)}"));

        public static List<Variable> Variables(string texto)
        {
            List<Variable> lista = new List<Variable>();
            foreach (string parte in texto.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int dos = parte.IndexOf(':');
                Variable v = new Variable { Nombre = Uri.UnescapeDataString(parte.Substring(0, dos)) };
                v.Valores.AddRange(Tabla(parte.Substring(dos + 1)));
                lista.Add(v);
            }

            return lista;
        }
    }

    /// <summary>
    ///     Tipos de receta y como se resuelven las estaciones que dependen de placements
    /// </summary>
    public static class Recetas
    {
        public const string Superestructura = "Superestructura";
        public const string Subestructura = "Subestructura";
        public const string EjeSecundario = "EjeSecundario";

        //Estacion de un placement del eje si sigue existiendo; si no, la estacion guardada, recortada al eje
        public static double Estacion(Eje eje, string placement, double guardada)
        {
            Placement? p = placement.Length > 0 ? eje.BuscarPlacement(placement) : null;
            double estacion = p?.Estacion ?? guardada;
            return Math.Max(eje.EstacionInicial, Math.Min(eje.EstacionFinal, estacion));
        }

        public static Receta DeEjeSecundario(OpcionesEjeSecundario o) =>
            new Receta(EjeSecundario).Con("nombre", o.Nombre).Con("derecha", Receta.Tabla(o.Derecha)).Con("subir", o.Subir).Con("largo", o.LargoMaximo);

        public static OpcionesEjeSecundario OpcionesEjeSecundario(Receta r)
        {
            OpcionesEjeSecundario o = new OpcionesEjeSecundario { Nombre = r.Texto("nombre"), Subir = r.Numero("subir"), LargoMaximo = r.Numero("largo", 10) };
            o.Derecha.AddRange(Receta.Tabla(r.Texto("derecha")));
            return o;
        }
    }
}
