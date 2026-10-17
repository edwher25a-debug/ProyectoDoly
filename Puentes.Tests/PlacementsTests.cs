using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class PlacementsTests
    {
        private static Eje EjeRecto(double longitud) =>
            new Eje("Recto", new AlineamientoHorizontal(new[]
            {
                new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, longitud, 0, 0)
            }, 1000), null);

        [Fact]
        public void Placements_viajan_en_el_eje_guardado()
        {
            Eje eje = EjeRecto(120);
            eje.MarcasCada = 25;
            eje.Placements.Add(new Placement { Id = "E1", Estacion = 1000 });
            eje.Placements.Add(new Placement { Id = "P|1 ñ", Estacion = 1040.5, Rotacion = -12.5 });

            Eje leido = SerializadorEje.Leer(SerializadorEje.Escribir(eje));

            Assert.Equal(25, leido.MarcasCada);
            Assert.Equal(new[] { "E1", "P|1 ñ" }, leido.Placements.Select(p => p.Id).ToArray());
            Assert.Equal(1040.5, leido.Placements[1].Estacion);
            Assert.Equal(-12.5, leido.Placements[1].Rotacion);
            Assert.NotNull(leido.BuscarPlacement("e1"));
        }

        [Fact]
        public void Eje_guardado_sin_placements_se_sigue_leyendo()
        {
            string antiguo = string.Join("\n", SerializadorEje.Escribir(EjeRecto(50)).Split('\n').Where(l => !l.StartsWith("M|")));
            Eje leido = SerializadorEje.Leer(antiguo);
            Assert.Empty(leido.Placements);
            Assert.Equal(20, leido.MarcasCada);
        }

        [Fact]
        public void Validar_detecta_ids_repetidos_estaciones_fuera_y_giros_excesivos()
        {
            Eje eje = EjeRecto(100);
            List<string> errores = PlacementsEje.Validar(eje, new List<Placement>
            {
                new Placement { Id = "P1", Estacion = 1010 },
                new Placement { Id = "p1", Estacion = 1020 },
                new Placement { Id = "", Estacion = 1030 },
                new Placement { Id = "P4", Estacion = 1200 },
                new Placement { Id = "P5", Estacion = 1050, Rotacion = 85 }
            });

            Assert.Equal(4, errores.Count);
            Assert.Empty(PlacementsEje.Validar(eje, new List<Placement> { new Placement { Id = "P1", Estacion = 1100, Rotacion = 30 } }));
        }

        [Fact]
        public void Distribuir_pone_placements_cada_separacion_y_siempre_en_el_final()
        {
            List<Placement> lista = PlacementsEje.Distribuir(1000, 1100, 30, "P", 3);
            Assert.Equal(new[] { 1000.0, 1030, 1060, 1090, 1100 }, lista.Select(p => p.Estacion).ToArray());
            Assert.Equal(new[] { "P3", "P4", "P5", "P6", "P7" }, lista.Select(p => p.Id).ToArray());
            Assert.Equal("P8", PlacementsEje.SiguienteId(lista, "P"));
        }

        [Fact]
        public void Plano_del_placement_es_perpendicular_al_eje_y_gira()
        {
            Eje eje = EjeRecto(100);
            Punto2 recto = PlacementsEje.DireccionPlano(eje, new Placement { Estacion = 1050 });
            Assert.Equal(0, recto.X, 9);
            Assert.Equal(1, recto.Y, 9);

            Punto2 esviado = PlacementsEje.DireccionPlano(eje, new Placement { Estacion = 1050, Rotacion = 30 });
            Assert.Equal(-Math.Sin(Math.PI / 6), esviado.X, 9);

            (Punto3 izquierda, Punto3 derecha) = PlacementsEje.Marca(eje, new Placement { Estacion = 1050 }, 12);
            Assert.Equal(6, izquierda.Y, 9);
            Assert.Equal(-6, derecha.Y, 9);
            Assert.Equal(50, izquierda.X, 9);
        }
    }
}
