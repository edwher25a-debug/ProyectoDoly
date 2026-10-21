using System;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Subestructura;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class SubestructuraTests
    {
        //Eje recto hacia el este (rumbo 0) con rasante al 5 %
        private static Eje Eje() => new Eje("Recto", new AlineamientoHorizontal(new[]
        {
            new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, 200, 0, 0)
        }, 0), new PerfilVertical("R", new[] { new PuntoVertical(0, 100), new PuntoVertical(200, 110) }));

        [Fact]
        public void Pila_va_al_punto_del_eje_orientada_con_el_eje()
        {
            Ubicacion3 u = UbicacionSubestructura.Calcular(Eje(), new Placement { Id = "P1", Estacion = 40 }, new OpcionesUbicacion());

            Assert.Equal(40, u.Origen.X, 9);
            Assert.Equal(0, u.Origen.Y, 9);
            Assert.Equal(102, u.Origen.Z, 9);
            Assert.Equal(1, u.EjeX.X, 9);
            Assert.Equal(1, u.EjeY.Y, 9);
            Assert.Equal(1, u.EjeZ.Z, 9);
        }

        [Fact]
        public void Desplazamientos_van_en_el_plano_del_placement_esviado()
        {
            OpcionesUbicacion opciones = new OpcionesUbicacion { DesplazamientoLateral = 4, DesplazamientoVertical = -2.5 };
            Ubicacion3 u = UbicacionSubestructura.Calcular(Eje(), new Placement { Estacion = 100, Rotacion = 30 }, opciones);

            //4 m a la derecha en un plano girado 30°: se mueve tambien a lo largo del eje
            Assert.Equal(100 + 4 * Math.Sin(Math.PI / 6), u.Origen.X, 9);
            Assert.Equal(-4 * Math.Cos(Math.PI / 6), u.Origen.Y, 9);
            Assert.Equal(105 - 2.5, u.Origen.Z, 9);
            Assert.Equal(Math.Cos(Math.PI / 6), u.EjeX.X, 9);
            Assert.Equal(25.5 - 0, UbicacionSubestructura.Altura(u, 77), 9);
            Assert.Equal(0, UbicacionSubestructura.Altura(u, 200), 9);
        }

        [Fact]
        public void Sin_siempre_vertical_la_pila_se_inclina_con_la_rasante()
        {
            Ubicacion3 u = UbicacionSubestructura.Calcular(Eje(), new Placement { Estacion = 50 }, new OpcionesUbicacion { SiempreVertical = false });
            double l = Math.Sqrt(1 + 0.05 * 0.05);
            Assert.Equal(0.05 / l, u.EjeX.Z, 9);
            Assert.Equal(-0.05 / l, u.EjeZ.X, 9);

            //Ejes perpendiculares entre si
            Assert.Equal(0, u.EjeX.X * u.EjeZ.X + u.EjeX.Y * u.EjeZ.Y + u.EjeX.Z * u.EjeZ.Z, 9);
        }
    }
}
