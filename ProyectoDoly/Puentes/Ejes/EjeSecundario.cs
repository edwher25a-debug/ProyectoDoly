using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    public sealed class OpcionesEjeSecundario
    {
        public string Nombre { get; set; } = "";

        //Desplazamiento a la derecha del eje principal (m): un valor = constante; varios = lineal entre estaciones del principal
        public List<(double estacion, double valor)> Derecha { get; set; } = new List<(double, double)>();

        //Desplazamiento vertical respecto a la rasante del principal (m)
        public double Subir { get; set; }

        //Largo maximo de cada tramo aproximado en clotoides o con desplazamiento variable (m)
        public double LargoMaximo { get; set; } = 10;
    }

    /// <summary>
    ///     Eje secundario desplazado del principal (como los ejes secundarios de SOFiSTiK): bordes de tablero, ejes de
    ///     vigas, ramales paralelos. Rectas y arcos con desplazamiento constante son exactos (recta y arco paralelos);
    ///     clotoides y desplazamientos variables se aproximan por clotoides cortas continuas en posicion y rumbo.
    ///     El eje secundario tiene su propio estacionamiento (su longitud), empezando en la estacion inicial del principal;
    ///     placements y variables se trasladan a la estacion equivalente.
    /// </summary>
    public static class EjeSecundario
    {
        public static double DerechaEn(IReadOnlyList<(double estacion, double valor)> tabla, double s)
        {
            if (tabla.Count == 0) return 0;
            if (s <= tabla[0].estacion) return tabla[0].valor;
            for (int i = 0; i < tabla.Count - 1; i++)
                if (s <= tabla[i + 1].estacion)
                {
                    double t = (s - tabla[i].estacion) / (tabla[i + 1].estacion - tabla[i].estacion);
                    return tabla[i].valor + (tabla[i + 1].valor - tabla[i].valor) * t;
                }

            return tabla[tabla.Count - 1].valor;
        }

        public static Eje Crear(Eje principal, OpcionesEjeSecundario opciones)
        {
            if (string.IsNullOrWhiteSpace(opciones.Nombre)) throw new ArgumentException("Falta el nombre del eje secundario.");
            List<(double estacion, double valor)> tabla = opciones.Derecha.OrderBy(p => p.estacion).ToList();
            if (tabla.Count == 0) throw new ArgumentException("Indique el desplazamiento del eje secundario.");
            for (int i = 0; i < tabla.Count - 1; i++)
                if (tabla[i + 1].estacion - tabla[i].estacion < 1e-6) throw new ArgumentException($"La estación {Eje.FormatoEstacion(tabla[i].estacion)} está repetida.");

            AlineamientoHorizontal h = principal.Horizontal;
            double inicio = principal.EstacionInicial;
            bool constante = tabla.All(p => Math.Abs(p.valor - tabla[0].valor) < 1e-12);

            //Quiebres: cambios de tramo del principal y puntos de la tabla de desplazamientos
            List<double> quiebres = Enumerable.Range(0, h.Elementos.Count).Select(h.EstacionInicioDe)
                .Concat(tabla.Select(p => p.estacion)).Concat(new[] { principal.EstacionFinal })
                .Where(e => e >= inicio - 1e-9 && e <= principal.EstacionFinal + 1e-9).Distinct().OrderBy(e => e).ToList();

            List<ElementoHorizontal> elementos = new List<ElementoHorizontal>();
            List<(double principal, double secundario)> mapa = new List<(double, double)> { (inicio, inicio) };
            Punto2? anterior = null;
            double estacionSecundaria = inicio;

            for (int q = 0; q < quiebres.Count - 1; q++)
            {
                double desde = quiebres[q], hasta = quiebres[q + 1];
                if (hasta - desde < 1e-6) continue;

                (ElementoHorizontal elemento, _) = h.Localizar((desde + hasta) / 2);
                bool exacto = constante && elemento.Tipo != TipoElemento.Clotoide;
                int partes = exacto ? 1 : Math.Max(1, (int)Math.Ceiling((hasta - desde) / opciones.LargoMaximo - 1e-9));

                for (int k = 0; k < partes; k++)
                {
                    double s0 = desde + (hasta - desde) * k / partes, s1 = desde + (hasta - desde) * (k + 1) / partes;
                    double g = (DerechaEn(tabla, s1) - DerechaEn(tabla, s0)) / (s1 - s0);

                    (Punto2 p0, double rumbo0, double kappa0, double w0) = Desplazado(principal, tabla, s0 + 1e-9, g);
                    (_, _, double kappa1, double w1) = Desplazado(principal, tabla, s1 - 1e-9, g);
                    double largo = Largo(principal, tabla, s0, s1, g);
                    if (w0 <= 1e-6 || w1 <= 1e-6) throw new ArgumentException("El desplazamiento es mayor que el radio de una curva del eje principal.");

                    //Posicion continua: cada tramo empieza donde termino el anterior; rumbo exacto (respeta los quiebres reales)
                    Punto2 comienzo = anterior ?? p0;
                    TipoElemento tipo = Math.Abs(kappa0) < 1e-12 && Math.Abs(kappa1) < 1e-12 ? TipoElemento.Recta
                        : Math.Abs(kappa0 - kappa1) < 1e-12 ? TipoElemento.Arco : TipoElemento.Clotoide;
                    if (tipo == TipoElemento.Recta) kappa0 = kappa1 = 0;
                    if (tipo == TipoElemento.Arco) kappa1 = kappa0;

                    ElementoHorizontal nuevo = new ElementoHorizontal(tipo, comienzo, rumbo0, largo, kappa0, kappa1);
                    elementos.Add(nuevo);
                    anterior = nuevo.Fin;
                    estacionSecundaria += largo;
                    mapa.Add((s1, estacionSecundaria));
                }
            }

            AlineamientoHorizontal horizontal = new AlineamientoHorizontal(elementos, inicio);
            double Mapear(double s) => Interpolar(mapa, s);

            //Rasante: los PIV del principal en su estacion equivalente, desplazados en altura
            PerfilVertical? vertical = null;
            if (principal.TieneRasante)
                vertical = new PerfilVertical($"{principal.Vertical.Nombre} ({opciones.Nombre})", principal.Vertical.Puntos.Select(p =>
                {
                    double curva = p.LongitudCurva > 0 ? Mapear(p.Estacion + p.LongitudCurva / 2) - Mapear(p.Estacion - p.LongitudCurva / 2) : 0;
                    return new PuntoVertical(Mapear(p.Estacion), p.Cota + opciones.Subir, curva);
                }));
            else if (Math.Abs(opciones.Subir) > 1e-12)
                vertical = PerfilVertical.Horizontal(horizontal.EstacionInicial, horizontal.EstacionFinal, opciones.Subir);

            Eje secundario = new Eje(opciones.Nombre.Trim(), horizontal, vertical) { MarcasCada = principal.MarcasCada };
            foreach (Placement p in principal.Placements)
                secundario.Placements.Add(new Placement { Id = p.Id, Estacion = Mapear(p.Estacion), Rotacion = p.Rotacion });
            foreach (Variable v in principal.Variables)
            {
                Variable copia = new Variable { Nombre = v.Nombre };
                copia.Valores.AddRange(v.Valores.Select(x => (Mapear(x.estacion), x.valor)));
                secundario.Variables.Add(copia);
            }

            return secundario;
        }

        //Estacion del eje secundario que corresponde a una del principal
        public static double Interpolar(IReadOnlyList<(double principal, double secundario)> mapa, double s)
        {
            if (s <= mapa[0].principal) return mapa[0].secundario + (s - mapa[0].principal);
            for (int i = 0; i < mapa.Count - 1; i++)
                if (s <= mapa[i + 1].principal)
                {
                    double t = (s - mapa[i].principal) / (mapa[i + 1].principal - mapa[i].principal);
                    return mapa[i].secundario + (mapa[i + 1].secundario - mapa[i].secundario) * t;
                }

            return mapa[mapa.Count - 1].secundario + (s - mapa[mapa.Count - 1].principal);
        }

        //Punto, rumbo, curvatura y velocidad (d sigma / d s) de la curva desplazada, con pendiente de desplazamiento g
        private static (Punto2 punto, double rumbo, double curvatura, double velocidad) Desplazado(Eje principal,
            IReadOnlyList<(double estacion, double valor)> tabla, double s, double g)
        {
            (ElementoHorizontal elemento, double local) = principal.Horizontal.Localizar(s);
            double theta = elemento.RumboEn(local);
            double k = elemento.CurvaturaEn(local);
            double dk = (elemento.CurvaturaFin - elemento.CurvaturaInicio) / elemento.Longitud;

            //Desplazamiento a la izquierda (l) y su derivada
            double l = -DerechaEn(tabla, s), dl = -g;
            double b = 1 - k * l;
            double w = Math.Sqrt(b * b + dl * dl);
            double rumbo = theta + Math.Atan2(dl, b);
            double curvatura = (k + dl * (dk * l + k * dl) / (w * w)) / w;

            Punto2 c = elemento.PuntoEn(local);
            Punto2 izquierda = new Punto2(-Math.Sin(theta), Math.Cos(theta));
            return (c + izquierda * l, rumbo, curvatura, b > 0 ? w : -1);
        }

        //Longitud de la curva desplazada entre s0 y s1 (Gauss-Legendre de 5 puntos)
        private static double Largo(Eje principal, IReadOnlyList<(double estacion, double valor)> tabla, double s0, double s1, double g)
        {
            double[] nodos = { -0.9061798459386640, -0.5384693101056831, 0, 0.5384693101056831, 0.9061798459386640 };
            double[] pesos = { 0.2369268850561891, 0.4786286704993665, 0.5688888888888889, 0.4786286704993665, 0.2369268850561891 };
            double medio = (s0 + s1) / 2, mitad = (s1 - s0) / 2, suma = 0;
            for (int i = 0; i < nodos.Length; i++) suma += pesos[i] * Math.Abs(Desplazado(principal, tabla, medio + nodos[i] * mitad, g).velocidad);
            return suma * mitad;
        }
    }
}
