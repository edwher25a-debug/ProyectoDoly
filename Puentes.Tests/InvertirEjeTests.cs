using System.Linq;
using ProyectoDoly.Puentes.Ejes;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class InvertirEjeTests
    {
        [Fact]
        public void Invertir_recorre_los_mismos_puntos_al_reves()
        {
            ElementoHorizontal r = new ElementoHorizontal(TipoElemento.Recta, new Punto2(10, 20), 0.2, 50, 0, 0);
            ElementoHorizontal c = new ElementoHorizontal(TipoElemento.Clotoide, r.Fin, r.RumboFin, 40, 0, 1 / 150.0);
            ElementoHorizontal a = new ElementoHorizontal(TipoElemento.Arco, c.Fin, c.RumboFin, 30, 1 / 150.0, 1 / 150.0);
            Eje eje = new Eje("E", new AlineamientoHorizontal(new[] { r, c, a }, 500),
                new PerfilVertical("R", new[] { new PuntoVertical(500, 10), new PuntoVertical(560, 13, 30), new PuntoVertical(620, 12) }));
            eje.Placements.Add(new Placement { Id = "E1", Estacion = 500 });
            eje.Placements.Add(new Placement { Id = "P1", Estacion = 545, Rotacion = 12 });
            Variable v = new Variable { Nombre = "H" };
            v.Valores.Add((500, 1));
            v.Valores.Add((620, 2));
            eje.Variables.Add(v);

            Eje inv = InvertirEje.Invertir(eje);

            Assert.Equal(500, inv.EstacionInicial, 9);
            Assert.Equal(620, inv.EstacionFinal, 9);
            foreach (double s in new[] { 500.0, 530, 560, 575, 600, 620 })
            {
                Punto3 a1 = eje.Evaluar(s).Posicion, b1 = inv.Evaluar(500 + 620 - s).Posicion;
                Assert.Equal(a1.X, b1.X, 6);
                Assert.Equal(a1.Y, b1.Y, 6);
                Assert.Equal(a1.Z, b1.Z, 6);
            }

            Assert.Equal(new[] { "P1", "E1" }, inv.Placements.Select(p => p.Id).ToArray());
            Assert.Equal(575, inv.Placements[0].Estacion, 9);
            Assert.Equal(1, inv.Variables[0].ValorEn(620), 9);

            //Invertir dos veces devuelve el original
            Eje doble = InvertirEje.Invertir(inv);
            Assert.Equal(eje.Evaluar(555).Posicion.X, doble.Evaluar(555).Posicion.X, 6);
        }
    }
}
