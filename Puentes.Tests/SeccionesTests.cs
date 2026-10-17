using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Planos;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class SeccionesTests
    {
        private static Eje EjeRecto(double longitud) =>
            new Eje("Recto", new AlineamientoHorizontal(new[]
            {
                new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, longitud, 0, 0)
            }, 1000), null);

        private static readonly List<Placement> Apoyos = new List<Placement>
        {
            new Placement { Id = "P2", Estacion = 1030, Rotacion = 15 },
            new Placement { Id = "E1", Estacion = 1000 },
            new Placement { Id = "E3", Estacion = 1055 }
        };

        [Fact]
        public void Cortes_en_placements_ordenados_y_con_giro()
        {
            List<CorteSeccion> cortes = EstacionesSeccion.Calcular(EjeRecto(100), Apoyos, new OpcionesCortes());
            Assert.Equal(new[] { "E1", "P2", "E3" }, cortes.Select(c => c.Nombre).ToArray());
            Assert.Equal(15, cortes[1].Giro);

            List<CorteSeccion> rectos = EstacionesSeccion.Calcular(EjeRecto(100), Apoyos, new OpcionesCortes { ConsiderarGiro = false });
            Assert.All(rectos, c => Assert.Equal(0, c.Giro));
        }

        [Fact]
        public void Entre_placements_se_divide_en_partes_iguales_sin_pasar_la_separacion()
        {
            List<CorteSeccion> cortes = EstacionesSeccion.Calcular(EjeRecto(100), Apoyos,
                new OpcionesCortes { EnPlacements = false, EntrePlacements = true, Cada = 10 });

            //30 m en 3 partes (2 cortes) y 25 m en 3 partes de 8.33 (2 cortes)
            Assert.Equal(new[] { 1010, 1020, 1030 + 25.0 / 3, 1030 + 50.0 / 3 }, cortes.Select(c => c.Estacion).ToArray(), new Cerca());
            Assert.Equal("E1-P2 1", cortes[0].Nombre);
            Assert.Equal("P2-E3 2", cortes[3].Nombre);
        }

        [Fact]
        public void Estaciones_sueltas_sin_duplicar_y_dentro_del_eje()
        {
            List<CorteSeccion> cortes = EstacionesSeccion.Calcular(EjeRecto(100), Apoyos,
                new OpcionesCortes { Estaciones = new List<double> { 1030, 1042.5 } });
            Assert.Equal(4, cortes.Count);
            Assert.Equal("P2", cortes.Single(c => Math.Abs(c.Estacion - 1030) < 1e-9).Nombre);

            Assert.Throws<ArgumentException>(() => EstacionesSeccion.Calcular(EjeRecto(100), Apoyos,
                new OpcionesCortes { Estaciones = new List<double> { 1200 } }));
        }

        [Fact]
        public void La_seccion_mira_en_el_sentido_del_eje_girada()
        {
            Punto2 m = EstacionesSeccion.Mirada(EjeRecto(100), new CorteSeccion { Estacion = 1050, Giro = 90 });
            Assert.Equal(0, m.X, 9);
            Assert.Equal(1, m.Y, 9);
        }

        private sealed class Cerca : IEqualityComparer<double>
        {
            public bool Equals(double a, double b) => Math.Abs(a - b) < 1e-9;
            public int GetHashCode(double v) => 0;
        }
    }
}
