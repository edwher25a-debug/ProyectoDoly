using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class EjeSecundarioTests
    {
        //Recta de 50 m, clotoide de 40 m hasta R = 200 y arco de 60 m, girando a la izquierda
        private static Eje Principal()
        {
            ElementoHorizontal r = new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, 50, 0, 0);
            ElementoHorizontal c = new ElementoHorizontal(TipoElemento.Clotoide, r.Fin, r.RumboFin, 40, 0, 1 / 200.0);
            ElementoHorizontal a = new ElementoHorizontal(TipoElemento.Arco, c.Fin, c.RumboFin, 60, 1 / 200.0, 1 / 200.0);
            Eje eje = new Eje("Principal", new AlineamientoHorizontal(new[] { r, c, a }, 1000),
                new PerfilVertical("Rasante", new[] { new PuntoVertical(1000, 100), new PuntoVertical(1080, 102, 40), new PuntoVertical(1150, 101) }));
            eje.Placements.Add(new Placement { Id = "P1", Estacion = 1120, Rotacion = 10 });
            return eje;
        }

        [Fact]
        public void Desplazamiento_constante_queda_paralelo()
        {
            Eje principal = Principal();
            Eje borde = EjeSecundario.Crear(principal, new OpcionesEjeSecundario { Nombre = "Borde", Derecha = { (0, 6) }, Subir = -0.3 });

            //Cada punto del secundario esta a 6 m a la derecha del principal
            foreach (double e in new[] { 1010.0, 1060, 1090, 1130, 1150 })
            {
                Punto2 p = principal.Horizontal.PuntoEn(e);
                double r = principal.Horizontal.RumboEn(e);
                Punto2 esperado = p + new Punto2(Math.Sin(r), -Math.Cos(r)) * 6;
                double cercana = Enumerable.Range(0, 3001).Select(i => borde.EstacionInicial + borde.Horizontal.Longitud * i / 3000.0)
                    .Min(s => borde.Horizontal.PuntoEn(s).Distancia(esperado));
                Assert.True(cercana < 0.03, $"{e}: {cercana}");
            }

            //Por fuera de la curva (a la derecha, girando a la izquierda) es mas largo; el arco exterior tiene radio 206
            Assert.True(borde.Horizontal.Longitud > principal.Horizontal.Longitud);
            ElementoHorizontal arco = borde.Horizontal.Elementos.Last();
            Assert.Equal(TipoElemento.Arco, arco.Tipo);
            Assert.Equal(1 / 206.0, arco.CurvaturaInicio, 9);
            Assert.Equal(TipoElemento.Recta, borde.Horizontal.Elementos[0].Tipo);

            //Continuo en posicion y la rasante baja 0.3 m
            for (int i = 0; i < borde.Horizontal.Elementos.Count - 1; i++)
                Assert.True(borde.Horizontal.Elementos[i].Fin.Distancia(borde.Horizontal.Elementos[i + 1].Inicio) < 1e-9);
            Assert.Equal(99.7, borde.Evaluar(borde.EstacionInicial).Posicion.Z, 9);
            Assert.Equal("P1", borde.Placements.Single().Id);
            Assert.True(borde.Placements[0].Estacion > 1120);
        }

        [Fact]
        public void Final_del_secundario_cae_sobre_el_desplazado_exacto()
        {
            Eje principal = Principal();
            Eje borde = EjeSecundario.Crear(principal, new OpcionesEjeSecundario { Nombre = "Borde", Derecha = { (0, -4) } });
            double rf = principal.Horizontal.RumboEn(principal.EstacionFinal);
            Punto2 esperado = principal.Horizontal.PuntoEn(principal.EstacionFinal) + new Punto2(-Math.Sin(rf), Math.Cos(rf)) * 4;
            Assert.True(borde.Horizontal.PuntoEn(borde.EstacionFinal).Distancia(esperado) < 1e-3);
        }

        [Fact]
        public void Desplazamiento_variable_sigue_la_tabla()
        {
            ElementoHorizontal r = new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, 100, 0, 0);
            Eje principal = new Eje("Recto", new AlineamientoHorizontal(new[] { r }, 0), null);
            Eje ensanche = EjeSecundario.Crear(principal, new OpcionesEjeSecundario { Nombre = "E", Derecha = { (20, 5), (80, 8) } });

            Assert.Equal(-5, ensanche.Horizontal.PuntoEn(10).Y, 6);
            Assert.Equal(-8, ensanche.Horizontal.PuntoEn(ensanche.EstacionFinal).Y, 6);
            Assert.Equal(Math.Sqrt(60 * 60 + 9) + 40, ensanche.Horizontal.Longitud, 6);
        }

        [Fact]
        public void Desplazamiento_mayor_que_el_radio_se_rechaza()
        {
            Assert.Throws<ArgumentException>(() => EjeSecundario.Crear(Principal(), new OpcionesEjeSecundario { Nombre = "X", Derecha = { (0, -250) } }));
        }
    }
}
