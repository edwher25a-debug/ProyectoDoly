using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ProyectoDoly.Puentes.Ejes
{
    /// <summary>
    ///     Guarda el eje en texto para que viaje dentro del modelo de Revit (el tablero lo lee sin volver al LandXML).
    ///     Una linea por dato, campos separados por "|" y numeros en formato invariante:
    ///     EJE|nombre / H|estacionInicial / E|tipo|x|y|rumbo|longitud|k0|k1 / V|nombre / P|estacion|cota|curva /
    ///     L|id|estacion|giro (placements) / M|marcasCada. Las lineas desconocidas se ignoran (versiones anteriores las leen igual)
    /// </summary>
    public static class SerializadorEje
    {
        private const string Version = "DOLY-EJE 1";

        public static string Escribir(Eje eje)
        {
            StringBuilder texto = new StringBuilder();
            texto.AppendLine(Version);
            texto.AppendLine($"EJE|{Uri.EscapeDataString(eje.Nombre)}");
            texto.AppendLine($"H|{Num(eje.Horizontal.EstacionInicial)}");

            foreach (ElementoHorizontal e in eje.Horizontal.Elementos)
                texto.AppendLine($"E|{e.Tipo}|{Num(e.Inicio.X)}|{Num(e.Inicio.Y)}|{Num(e.RumboInicio)}|{Num(e.Longitud)}|" +
                                 $"{Num(e.CurvaturaInicio)}|{Num(e.CurvaturaFin)}");

            if (eje.TieneRasante)
            {
                texto.AppendLine($"V|{Uri.EscapeDataString(eje.Vertical.Nombre)}");
                foreach (PuntoVertical p in eje.Vertical.Puntos)
                    texto.AppendLine($"P|{Num(p.Estacion)}|{Num(p.Cota)}|{Num(p.LongitudCurva)}");
            }

            foreach (Placement p in eje.Placements)
                texto.AppendLine($"L|{Uri.EscapeDataString(p.Id)}|{Num(p.Estacion)}|{Num(p.Rotacion)}");
            texto.AppendLine($"M|{Num(eje.MarcasCada)}");

            return texto.ToString();
        }

        public static Eje Leer(string texto)
        {
            string[] lineas = texto.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lineas.Length == 0 || lineas[0] != Version)
                throw new FormatException("Los datos guardados del eje no tienen un formato conocido.");

            string nombre = "";
            double estacionInicial = 0;
            string? nombreRasante = null;
            List<ElementoHorizontal> elementos = new List<ElementoHorizontal>();
            List<PuntoVertical> piv = new List<PuntoVertical>();
            List<Placement> placements = new List<Placement>();
            double marcas = 20;

            foreach (string linea in lineas.Skip(1))
            {
                string[] c = linea.Split('|');
                switch (c[0])
                {
                    case "EJE":
                        nombre = Uri.UnescapeDataString(c[1]);
                        break;
                    case "H":
                        estacionInicial = Leer(c, 1);
                        break;
                    case "E":
                        TipoElemento tipo = (TipoElemento)Enum.Parse(typeof(TipoElemento), c[1]);
                        elementos.Add(new ElementoHorizontal(tipo, new Punto2(Leer(c, 2), Leer(c, 3)), Leer(c, 4), Leer(c, 5),
                            Leer(c, 6), Leer(c, 7)));
                        break;
                    case "V":
                        nombreRasante = Uri.UnescapeDataString(c[1]);
                        break;
                    case "P":
                        piv.Add(new PuntoVertical(Leer(c, 1), Leer(c, 2), Leer(c, 3)));
                        break;
                    case "L":
                        placements.Add(new Placement { Id = Uri.UnescapeDataString(c[1]), Estacion = Leer(c, 2), Rotacion = c.Length > 3 ? Leer(c, 3) : 0 });
                        break;
                    case "M":
                        marcas = Leer(c, 1);
                        break;
                }
            }

            PerfilVertical? rasante = nombreRasante != null ? new PerfilVertical(nombreRasante, piv) : null;
            Eje eje = new Eje(nombre, new AlineamientoHorizontal(elementos, estacionInicial), rasante) { MarcasCada = marcas };
            eje.Placements.AddRange(placements);
            return eje;
        }

        //"R" conserva todos los decimales: el eje leido es identico al guardado
        private static string Num(double valor) => valor.ToString("R", CultureInfo.InvariantCulture);

        private static double Leer(string[] campos, int indice)
        {
            if (indice >= campos.Length) throw new FormatException("Faltan datos en el eje guardado.");
            return double.Parse(campos[indice], NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
