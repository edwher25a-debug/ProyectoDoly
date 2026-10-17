using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes.Planos
{
    //Donde cortar: estacion, nombre de la vista y giro del plano (grados respecto a la perpendicular al eje)
    public sealed class CorteSeccion
    {
        public string Nombre { get; set; } = "";
        public double Estacion { get; set; }
        public double Giro { get; set; }
    }

    public sealed class OpcionesCortes
    {
        //En los placements elegidos
        public bool EnPlacements { get; set; } = true;

        //Entre placements consecutivos elegidos, a no mas de "Cada" metros (dividiendo cada intervalo en partes iguales)
        public bool EntrePlacements { get; set; }
        public double Cada { get; set; } = 10;

        //Girar el corte con el placement (apoyos esviados)
        public bool ConsiderarGiro { get; set; } = true;

        //Estaciones sueltas escritas por el usuario
        public List<double> Estaciones { get; set; } = new List<double>();
    }

    /// <summary>
    ///     Estaciones de las secciones del plano (como Sections de SOFiSTiK): en placements, entre placements con una
    ///     regla de distribucion o en estaciones dadas. Sin repetir estaciones.
    /// </summary>
    public static class EstacionesSeccion
    {
        public static List<CorteSeccion> Calcular(Eje eje, IReadOnlyList<Placement> elegidos, OpcionesCortes opciones)
        {
            List<Placement> orden = elegidos.OrderBy(p => p.Estacion).ToList();
            List<CorteSeccion> cortes = new List<CorteSeccion>();

            if (opciones.EnPlacements)
                foreach (Placement p in orden)
                    cortes.Add(new CorteSeccion { Nombre = p.Id, Estacion = p.Estacion, Giro = opciones.ConsiderarGiro ? p.Rotacion : 0 });

            if (opciones.EntrePlacements)
            {
                if (opciones.Cada <= 0) throw new ArgumentException("La separación entre secciones debe ser mayor que cero.");
                for (int i = 0; i < orden.Count - 1; i++)
                {
                    double a = orden[i].Estacion, b = orden[i + 1].Estacion;
                    int partes = (int)Math.Ceiling((b - a) / opciones.Cada - 1e-9);
                    for (int k = 1; k < partes; k++)
                    {
                        double e = a + (b - a) * k / partes;
                        cortes.Add(new CorteSeccion { Nombre = $"{orden[i].Id}-{orden[i + 1].Id} {k}", Estacion = e });
                    }
                }
            }

            foreach (double e in opciones.Estaciones)
            {
                if (e < eje.EstacionInicial - 1e-6 || e > eje.EstacionFinal + 1e-6)
                    throw new ArgumentException($"La estación {Eje.FormatoEstacion(e)} está fuera del eje.");
                cortes.Add(new CorteSeccion { Nombre = Eje.FormatoEstacion(e), Estacion = e });
            }

            //Ordenadas por estacion y sin duplicados (gana la primera: placement antes que intermedia)
            List<CorteSeccion> resultado = new List<CorteSeccion>();
            foreach (CorteSeccion c in cortes.OrderBy(c => c.Estacion))
                if (!resultado.Any(r => Math.Abs(r.Estacion - c.Estacion) < 1e-3)) resultado.Add(c);
            return resultado;
        }

        /// <summary>
        ///     Direccion en la que mira la seccion (sentido del eje girado por el corte), horizontal
        /// </summary>
        public static Punto2 Mirada(Eje eje, CorteSeccion corte)
        {
            double rumbo = eje.Horizontal.RumboEn(corte.Estacion) + corte.Giro * Math.PI / 180;
            return new Punto2(Math.Cos(rumbo), Math.Sin(rumbo));
        }
    }
}
