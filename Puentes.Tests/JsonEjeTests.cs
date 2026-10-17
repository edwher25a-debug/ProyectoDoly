using System;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class JsonEjeTests
    {
        [Fact]
        public void Ida_y_vuelta_conserva_el_eje()
        {
            ElementoHorizontal r = new ElementoHorizontal(TipoElemento.Recta, new Punto2(1000.5, 2000.25), 0.3, 50, 0, 0);
            ElementoHorizontal c = new ElementoHorizontal(TipoElemento.Clotoide, r.Fin, r.RumboFin, 40, 0, 1 / 300.0);
            Eje eje = new Eje("Eje \"A\" ñ", new AlineamientoHorizontal(new[] { r, c }, 1200),
                new PerfilVertical("Rasante", new[] { new PuntoVertical(1200, 50), new PuntoVertical(1240, 51, 20), new PuntoVertical(1290, 50.5) }))
            {
                MarcasCada = 25
            };
            eje.Placements.Add(new Placement { Id = "P1", Estacion = 1230, Rotacion = -7.5 });
            Variable v = new Variable { Nombre = "Canto" };
            v.Valores.Add((1200, 1.2));
            v.Valores.Add((1290, 1.8));
            eje.Variables.Add(v);

            Eje leido = JsonEje.Leer(JsonEje.Escribir(eje));

            Assert.Equal(eje.Nombre, leido.Nombre);
            Assert.Equal(25, leido.MarcasCada);
            Assert.Equal(eje.EstacionFinal, leido.EstacionFinal, 12);
            Assert.Equal(eje.Evaluar(1265).Posicion.X, leido.Evaluar(1265).Posicion.X, 9);
            Assert.Equal(eje.Evaluar(1245).Posicion.Z, leido.Evaluar(1245).Posicion.Z, 9);
            Assert.Equal(TipoElemento.Clotoide, leido.Horizontal.Elementos[1].Tipo);
            Assert.Equal(-7.5, leido.Placements.Single().Rotacion);
            Assert.Equal(1.8, leido.Variables.Single().ValorEn(1290), 12);
        }

        [Fact]
        public void Sin_rasante_ni_placements()
        {
            Eje eje = new Eje("Recto", new AlineamientoHorizontal(new[] { new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, 10, 0, 0) }, 0), null);
            string json = JsonEje.Escribir(eje);
            Assert.Contains("\"rasante\": null", json);
            Eje leido = JsonEje.Leer(json);
            Assert.False(leido.TieneRasante);
            Assert.Empty(leido.Placements);
        }

        [Fact]
        public void Json_escrito_a_mano()
        {
            Eje eje = JsonEje.Leer("{\"nombre\":\"M\",\"horizontal\":[{\"tipo\":\"arco\",\"x\":0,\"y\":0,\"rumbo\":0,\"longitud\":10,\"curvaturaInicio\":0.01,\"curvaturaFin\":0.01}]}");
            Assert.Equal(TipoElemento.Arco, eje.Horizontal.Elementos[0].Tipo);
            Assert.Throws<FormatException>(() => JsonEje.Leer("{\"nombre\":\"M\",\"horizontal\":[]}"));
            Assert.Throws<FormatException>(() => JsonEje.Leer("{\"nombre\":\"M\""));
        }
    }
}
