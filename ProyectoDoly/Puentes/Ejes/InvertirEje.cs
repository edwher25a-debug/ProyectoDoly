using System;
using System.Linq;

namespace ProyectoDoly.Puentes.Ejes
{
    /// <summary>
    ///     Invierte el sentido del eje (como Invert Axis de SOFiSTiK): el fin pasa a ser el inicio, con la misma estacion
    ///     inicial. Rumbos girados 180°, curvaturas con signo contrario y en orden inverso; rasante, placements y
    ///     variables en la estacion equivalente. El plano de cada placement queda igual (mismo giro).
    /// </summary>
    public static class InvertirEje
    {
        public static Eje Invertir(Eje eje)
        {
            double inicio = eje.EstacionInicial, fin = eje.EstacionFinal;
            double Espejo(double s) => inicio + (fin - s);

            ElementoHorizontal[] elementos = Enumerable.Reverse(eje.Horizontal.Elementos)
                .Select(e => new ElementoHorizontal(e.Tipo, e.Fin, Normalizar(e.RumboFin + Math.PI), e.Longitud, -e.CurvaturaFin, -e.CurvaturaInicio))
                .ToArray();

            PerfilVertical? vertical = eje.TieneRasante
                ? new PerfilVertical(eje.Vertical.Nombre, eje.Vertical.Puntos.Select(p => new PuntoVertical(Espejo(p.Estacion), p.Cota, p.LongitudCurva)))
                : null;

            Eje invertido = new Eje(eje.Nombre, new AlineamientoHorizontal(elementos, inicio), vertical) { MarcasCada = eje.MarcasCada };
            foreach (Placement p in eje.Placements.OrderByDescending(p => p.Estacion))
                invertido.Placements.Add(new Placement { Id = p.Id, Estacion = Espejo(p.Estacion), Rotacion = p.Rotacion });
            foreach (Variable v in eje.Variables)
            {
                Variable copia = new Variable { Nombre = v.Nombre };
                copia.Valores.AddRange(Enumerable.Reverse(v.Valores).Select(x => (Espejo(x.estacion), x.valor)));
                invertido.Variables.Add(copia);
            }

            return invertido;
        }

        private static double Normalizar(double angulo) => Math.Atan2(Math.Sin(angulo), Math.Cos(angulo));
    }
}
