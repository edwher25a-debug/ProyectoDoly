using System;
using System.Collections.Generic;
using ProyectoDoly.Puentes.Cuantificacion;
using ProyectoDoly.Puentes.Ejes;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class CuantificacionTests
    {
        private static Eje EjeRecto(double longitud) =>
            new Eje("Recto", new AlineamientoHorizontal(new[]
            {
                new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, longitud, 0, 0)
            }, 1000), null);

        [Fact]
        public void Proyecta_en_recta_con_desfase_a_la_derecha()
        {
            PuntoProyectado p = new ProyectorEje(EjeRecto(100)).Proyectar(new Punto2(37.25, -4));
            Assert.Equal(1037.25, p.Estacion, 5);
            Assert.Equal(4, p.Desfase, 5);
        }

        [Fact]
        public void Proyecta_en_curva()
        {
            //Cuarto de circulo de radio 50 girando a la izquierda: centro en (0, 50)
            const double radio = 50;
            Eje eje = new Eje("Curva", new AlineamientoHorizontal(new[]
            {
                new ElementoHorizontal(TipoElemento.Arco, new Punto2(0, 0), 0, Math.PI * radio / 2, 1 / radio, 1 / radio)
            }, 0), null);

            //Punto a 45° a 5 m por fuera de la curva (a la derecha)
            double a = Math.PI / 4;
            Punto2 fuera = new Punto2(Math.Sin(a) * (radio + 5), radio - Math.Cos(a) * (radio + 5));
            PuntoProyectado p = new ProyectorEje(eje).Proyectar(fuera);
            Assert.Equal(radio * a, p.Estacion, 4);
            Assert.Equal(5, p.Desfase, 4);
        }

        [Fact]
        public void Tramo_y_placement_de_un_tablero_y_de_una_pila()
        {
            Eje eje = EjeRecto(100);
            eje.Placements.Add(new Placement { Id = "E1", Estacion = 1000 });
            eje.Placements.Add(new Placement { Id = "P1", Estacion = 1040 });
            eje.Placements.Add(new Placement { Id = "E2", Estacion = 1100 });
            ProyectorEje proyector = new ProyectorEje(eje);

            List<Punto2> tablero = new List<Punto2> { new Punto2(0, 6), new Punto2(40, 6), new Punto2(0, -6), new Punto2(40, -6) };
            DatosCuantificacion t = CuantificacionEje.Calcular(proyector, eje, tablero, new Punto2(20, 0));
            Assert.Equal("E1-P1", t.Placement);
            Assert.Equal(40, t.Longitud, 5);

            List<Punto2> pila = new List<Punto2> { new Punto2(39, 2), new Punto2(41, -2) };
            DatosCuantificacion p = CuantificacionEje.Calcular(proyector, eje, pila, new Punto2(40, -1));
            Assert.Equal("P1", p.Placement);
            Assert.Equal(1, p.Desfase, 5);

            List<Punto2> suelto = new List<Punto2> { new Punto2(60, 0), new Punto2(62, 0) };
            Assert.Equal("P1-E2", CuantificacionEje.Calcular(proyector, eje, suelto, new Punto2(61, 0)).Placement);
        }
    }
}
