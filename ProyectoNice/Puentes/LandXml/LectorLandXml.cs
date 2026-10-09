using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ProyectoNice.Puentes.Ejes;

namespace ProyectoNice.Puentes.LandXml
{
    //Alineamiento leido de un LandXML con sus rasantes disponibles
    public sealed class AlineamientoLandXml
    {
        public string Nombre { get; set; } = "";
        public AlineamientoHorizontal Horizontal { get; set; } = null!;
        public List<PerfilVertical> Rasantes { get; } = new List<PerfilVertical>();
        public List<string> Avisos { get; } = new List<string>();

        public Eje CrearEje(PerfilVertical? rasante) => new Eje(Nombre, Horizontal, rasante);

        public override string ToString() => Nombre;
    }

    public sealed class ArchivoLandXml
    {
        public string Unidad { get; set; } = "meter";
        public List<AlineamientoLandXml> Alineamientos { get; } = new List<AlineamientoLandXml>();
    }

    /// <summary>
    ///     Lee alineamientos y rasantes de LandXML 1.x (exportacion de Civil 3D).
    ///     Todo se devuelve en metros, con X = Este e Y = Norte.
    /// </summary>
    public static class LectorLandXml
    {
        //Diferencia admitida entre el fin calculado de un tramo y el fin escrito en el archivo
        public const double ToleranciaCierre = 0.005;

        public static ArchivoLandXml Leer(string ruta)
        {
            using (FileStream archivo = File.OpenRead(ruta)) return Leer(archivo);
        }

        public static ArchivoLandXml Leer(Stream contenido)
        {
            XDocument documento = XDocument.Load(contenido);
            XElement raiz = documento.Root ?? throw new FormatException("El archivo LandXML esta vacio.");

            ArchivoLandXml resultado = new ArchivoLandXml();
            double factor = LeerUnidad(raiz, resultado);
            Dictionary<string, Punto2> puntosNombrados = LeerPuntosNombrados(raiz, factor);

            foreach (XElement alineamiento in Descendientes(raiz, "Alignment"))
                resultado.Alineamientos.Add(LeerAlineamiento(alineamiento, factor, puntosNombrados));

            if (resultado.Alineamientos.Count == 0)
                throw new FormatException("El archivo no contiene alineamientos (Alignment).");

            return resultado;
        }

        private static double LeerUnidad(XElement raiz, ArchivoLandXml resultado)
        {
            XElement? unidades = Hijos(raiz, "Units").FirstOrDefault();
            XElement? sistema = unidades?.Elements().FirstOrDefault();
            string unidad = sistema?.Attribute("linearUnit")?.Value ?? "meter";
            resultado.Unidad = unidad;

            switch (unidad)
            {
                case "meter": return 1;
                case "kilometer": return 1000;
                case "centimeter": return 0.01;
                case "millimeter": return 0.001;
                case "foot": return 0.3048;
                case "USSurveyFoot": return 1200.0 / 3937.0;
                case "inch": return 0.0254;
                default: throw new FormatException($"Unidad lineal no soportada: {unidad}.");
            }
        }

        //CgPoints con nombre, para tramos que usan pntRef en lugar de coordenadas
        private static Dictionary<string, Punto2> LeerPuntosNombrados(XElement raiz, double factor)
        {
            Dictionary<string, Punto2> puntos = new Dictionary<string, Punto2>();
            foreach (XElement punto in Descendientes(raiz, "CgPoint"))
            {
                string? nombre = punto.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(nombre) || string.IsNullOrWhiteSpace(punto.Value)) continue;
                puntos[nombre!] = LeerCoordenadas(punto.Value, factor);
            }

            return puntos;
        }

        private static AlineamientoLandXml LeerAlineamiento(XElement alineamiento, double factor, Dictionary<string, Punto2> puntosNombrados)
        {
            AlineamientoLandXml resultado = new AlineamientoLandXml
            {
                Nombre = alineamiento.Attribute("name")?.Value ?? "Alineamiento"
            };

            double estacionInicial = Numero(alineamiento.Attribute("staStart")?.Value, 0) * factor;
            XElement geometria = Hijos(alineamiento, "CoordGeom").FirstOrDefault()
                                 ?? throw new FormatException($"El alineamiento {resultado.Nombre} no tiene CoordGeom.");

            List<ElementoHorizontal> elementos = new List<ElementoHorizontal>();
            foreach (XElement tramo in geometria.Elements())
            {
                ElementoHorizontal? elemento = LeerTramo(tramo, factor, puntosNombrados, resultado.Avisos);
                if (elemento == null) continue;

                Punto2? finDeclarado = PuntoOpcional(tramo, "End", factor, puntosNombrados);
                if (finDeclarado.HasValue && elemento.Fin.Distancia(finDeclarado.Value) > ToleranciaCierre)
                    resultado.Avisos.Add($"{resultado.Nombre}: el tramo {elementos.Count + 1} ({tramo.Name.LocalName}) termina a " +
                                         $"{elemento.Fin.Distancia(finDeclarado.Value):F3} m del punto final del archivo.");

                elementos.Add(elemento);
            }

            resultado.Horizontal = new AlineamientoHorizontal(elementos, estacionInicial);

            foreach (XElement perfil in Hijos(alineamiento, "Profile"))
            foreach (XElement rasante in Hijos(perfil, "ProfAlign"))
            {
                PerfilVertical? leida = LeerRasante(rasante, factor, resultado.Avisos);
                if (leida != null) resultado.Rasantes.Add(leida);
            }

            return resultado;
        }

        private static ElementoHorizontal? LeerTramo(XElement tramo, double factor, Dictionary<string, Punto2> puntosNombrados, List<string> avisos)
        {
            switch (tramo.Name.LocalName)
            {
                case "Line":
                {
                    Punto2 inicio = Punto(tramo, "Start", factor, puntosNombrados);
                    Punto2 fin = Punto(tramo, "End", factor, puntosNombrados);
                    Punto2 delta = fin - inicio;
                    double longitud = delta.Longitud;
                    return new ElementoHorizontal(TipoElemento.Recta, inicio, Math.Atan2(delta.Y, delta.X), longitud, 0, 0);
                }

                case "Curve":
                {
                    Punto2 inicio = Punto(tramo, "Start", factor, puntosNombrados);
                    Punto2 centro = Punto(tramo, "Center", factor, puntosNombrados);
                    Punto2 fin = Punto(tramo, "End", factor, puntosNombrados);
                    double signo = Sentido(tramo);
                    Punto2 radial = inicio - centro;
                    double radio = radial.Longitud;

                    //Tangente: radial girado 90 grados en el sentido de avance
                    double rumbo = Math.Atan2(radial.Y, radial.X) + signo * Math.PI / 2;

                    double longitud = Numero(tramo.Attribute("length")?.Value, double.NaN) * factor;
                    if (double.IsNaN(longitud))
                    {
                        Punto2 radialFin = fin - centro;
                        double angulo = signo * (Math.Atan2(radialFin.Y, radialFin.X) - Math.Atan2(radial.Y, radial.X));
                        while (angulo <= 0) angulo += 2 * Math.PI;
                        longitud = angulo * radio;
                    }

                    return new ElementoHorizontal(TipoElemento.Arco, inicio, rumbo, longitud, signo / radio, signo / radio);
                }

                case "Spiral":
                {
                    Punto2 inicio = Punto(tramo, "Start", factor, puntosNombrados);
                    Punto2 pi = Punto(tramo, "PI", factor, puntosNombrados);
                    double signo = Sentido(tramo);
                    double longitud = Numero(tramo.Attribute("length")?.Value, double.NaN) * factor;
                    if (double.IsNaN(longitud)) throw new FormatException("Una clotoide (Spiral) no tiene el atributo length.");

                    string tipo = tramo.Attribute("spiType")?.Value ?? "clothoid";
                    if (tipo != "clothoid")
                        avisos.Add($"Espiral de tipo {tipo}: se calcula como clotoide.");

                    Punto2 haciaPi = pi - inicio;
                    return new ElementoHorizontal(TipoElemento.Clotoide, inicio, Math.Atan2(haciaPi.Y, haciaPi.X), longitud,
                        signo * Curvatura(tramo.Attribute("radiusStart")?.Value, factor),
                        signo * Curvatura(tramo.Attribute("radiusEnd")?.Value, factor));
                }

                default:
                    avisos.Add($"Tramo {tramo.Name.LocalName} no soportado: se omite.");
                    return null;
            }
        }

        private static PerfilVertical? LeerRasante(XElement rasante, double factor, List<string> avisos)
        {
            string nombre = rasante.Attribute("name")?.Value ?? "Rasante";
            List<PuntoVertical> puntos = new List<PuntoVertical>();

            foreach (XElement punto in rasante.Elements())
            {
                double longitud;
                switch (punto.Name.LocalName)
                {
                    case "PVI":
                        longitud = 0;
                        break;
                    case "ParaCurve":
                    case "CircCurve":
                        longitud = Numero(punto.Attribute("length")?.Value, 0) * factor;
                        if (punto.Name.LocalName == "CircCurve")
                            avisos.Add($"{nombre}: curva vertical circular calculada como parabola de igual longitud.");
                        break;
                    case "UnsymParaCurve":
                        longitud = (Numero(punto.Attribute("lengthIn")?.Value, 0) + Numero(punto.Attribute("lengthOut")?.Value, 0)) * factor;
                        avisos.Add($"{nombre}: curva vertical asimetrica calculada como simetrica.");
                        break;
                    default:
                        continue;
                }

                double[] valores = Numeros(punto.Value);
                if (valores.Length < 2) throw new FormatException($"{nombre}: PIV sin estacion y cota.");
                puntos.Add(new PuntoVertical(valores[0] * factor, valores[1] * factor, longitud));
            }

            if (puntos.Count < 2)
            {
                avisos.Add($"{nombre}: rasante con menos de dos PIV, se omite.");
                return null;
            }

            return new PerfilVertical(nombre, puntos);
        }

        //cw = horario (derecha, curvatura negativa), ccw = antihorario
        private static double Sentido(XElement tramo) => tramo.Attribute("rot")?.Value == "cw" ? -1 : 1;

        private static double Curvatura(string? radio, double factor)
        {
            if (string.IsNullOrWhiteSpace(radio) || radio!.Trim().Equals("INF", StringComparison.OrdinalIgnoreCase)) return 0;
            double valor = Numero(radio, double.PositiveInfinity) * factor;
            return double.IsInfinity(valor) || valor == 0 ? 0 : 1 / valor;
        }

        private static Punto2 Punto(XElement tramo, string hijo, double factor, Dictionary<string, Punto2> puntosNombrados) =>
            PuntoOpcional(tramo, hijo, factor, puntosNombrados)
            ?? throw new FormatException($"Al tramo {tramo.Name.LocalName} le falta el punto {hijo}.");

        private static Punto2? PuntoOpcional(XElement tramo, string hijo, double factor, Dictionary<string, Punto2> puntosNombrados)
        {
            XElement? punto = Hijos(tramo, hijo).FirstOrDefault();
            if (punto == null) return null;

            string? referencia = punto.Attribute("pntRef")?.Value;
            if (string.IsNullOrWhiteSpace(punto.Value) && referencia != null)
            {
                if (puntosNombrados.TryGetValue(referencia, out Punto2 nombrado)) return nombrado;
                throw new FormatException($"No se encontro el punto {referencia} en CgPoints.");
            }

            return LeerCoordenadas(punto.Value, factor);
        }

        //LandXML escribe "Norte Este [Cota]"
        private static Punto2 LeerCoordenadas(string texto, double factor)
        {
            double[] valores = Numeros(texto);
            if (valores.Length < 2) throw new FormatException($"Coordenadas no validas: \"{texto}\".");
            return new Punto2(valores[1] * factor, valores[0] * factor);
        }

        private static double[] Numeros(string texto) =>
            texto.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToArray();

        private static double Numero(string? texto, double porDefecto) =>
            double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out double valor) ? valor : porDefecto;

        //Busqueda por nombre local: ignora el espacio de nombres (LandXML 1.0, 1.1, 1.2)
        private static IEnumerable<XElement> Hijos(XElement padre, string nombre) =>
            padre.Elements().Where(e => e.Name.LocalName == nombre);

        private static IEnumerable<XElement> Descendientes(XElement padre, string nombre) =>
            padre.Descendants().Where(e => e.Name.LocalName == nombre);
    }
}
