using System;
using System.Collections.Generic;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Planos;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class PlantaTests
    {
        private static Eje EjeRecto(double inicio, double longitud) =>
            new Eje("Recto", new AlineamientoHorizontal(new[]
            {
                new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, longitud, 0, 0)
            }, inicio), null);

        [Fact]
        public void Estaciones_redondas_y_extremos()
        {
            Assert.Equal(new[] { 1005, 1020, 1040, 1060, 1065.0 }, MarcasPlanta.EstacionesCada(EjeRecto(1005, 60), 20, true));
            Assert.Equal(new[] { 1020, 1040, 1060.0 }, MarcasPlanta.EstacionesCada(EjeRecto(1005, 60), 20, false));
            Assert.Equal(new[] { 1000, 1020.0 }, MarcasPlanta.EstacionesCada(EjeRecto(1000, 20), 20, true));
        }

        [Fact]
        public void Travesano_va_de_izquierda_a_derecha_del_eje()
        {
            //Eje hacia +X: la izquierda es +Y
            (Punto2 izquierda, Punto2 derecha) = MarcasPlanta.Travesano(EjeRecto(0, 100), 40, 0, 3, 5);
            Assert.Equal(40, izquierda.X, 9);
            Assert.Equal(3, izquierda.Y, 9);
            Assert.Equal(-5, derecha.Y, 9);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(Math.PI / 2, Math.PI / 2)]
        [InlineData(Math.PI, 0)]
        [InlineData(-Math.PI / 2, Math.PI / 2)]
        [InlineData(3 * Math.PI / 4, -Math.PI / 4)]
        public void Texto_siempre_legible(double angulo, double esperado) =>
            Assert.Equal(esperado, MarcasPlanta.AnguloLegible(angulo), 9);
    }
}
