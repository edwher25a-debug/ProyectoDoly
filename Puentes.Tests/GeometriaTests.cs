using ProyectoNice.Puentes.Ejes;
using Xunit;

namespace ProyectoNice.Puentes.Tests
{
    public class GeometriaTests
    {
        //Clotoide por serie de Fresnel (independiente de la integracion numerica del motor)
        private static (double x, double y) ClotoideSerie(double l, double a)
        {
            double x = 0, y = 0, factorial = 1;
            double u = l * l / (2 * a * a);
            for (int n = 0; n < 12; n++)
            {
                if (n > 0) factorial *= 2 * n * (2 * n - 1);
                double signo = n % 2 == 0 ? 1 : -1;
                x += signo * l * Math.Pow(u, 2 * n) / ((4 * n + 1) * factorial);
                y += signo * l * Math.Pow(u, 2 * n + 1) / ((4 * n + 3) * factorial * (2 * n + 1));
            }

            return (x, y);
        }

        [Theory]
        [InlineData(60, 200)]
        [InlineData(120, 300)]
        [InlineData(40, 50)]
        public void Clotoide_coincide_con_serie(double longitud, double radio)
        {
            ElementoHorizontal clotoide = new ElementoHorizontal(TipoElemento.Clotoide, new Punto2(0, 0), 0, longitud, 0, 1 / radio);
            (double x, double y) = ClotoideSerie(longitud, Math.Sqrt(longitud * radio));

            Assert.Equal(x, clotoide.Fin.X, 6);
            Assert.Equal(y, clotoide.Fin.Y, 6);
            Assert.Equal(longitud / (2 * radio), clotoide.RumboFin, 12);
        }

        [Fact]
        public void Clotoide_a_la_derecha_es_simetrica()
        {
            ElementoHorizontal izquierda = new ElementoHorizontal(TipoElemento.Clotoide, new Punto2(0, 0), 0, 60, 0, 1 / 200.0);
            ElementoHorizontal derecha = new ElementoHorizontal(TipoElemento.Clotoide, new Punto2(0, 0), 0, 60, 0, -1 / 200.0);

            Assert.Equal(izquierda.Fin.X, derecha.Fin.X, 9);
            Assert.Equal(-izquierda.Fin.Y, derecha.Fin.Y, 9);
        }

        [Fact]
        public void Arco_de_cuarto_de_circulo()
        {
            double radio = 100;
            ElementoHorizontal arco = new ElementoHorizontal(TipoElemento.Arco, new Punto2(0, 0), 0, Math.PI * radio / 2, 1 / radio, 1 / radio);

            Assert.Equal(100, arco.Fin.X, 9);
            Assert.Equal(100, arco.Fin.Y, 9);
            Assert.Equal(Math.PI / 2, arco.RumboFin, 12);
        }

        [Fact]
        public void Recta_y_estacionamiento()
        {
            ElementoHorizontal recta = new ElementoHorizontal(TipoElemento.Recta, new Punto2(10, 20), Math.PI / 2, 50, 0, 0);
            AlineamientoHorizontal alineamiento = new AlineamientoHorizontal(new[] { recta }, 500);

            Punto2 p = alineamiento.PuntoEn(530);
            Assert.Equal(10, p.X, 9);
            Assert.Equal(50, p.Y, 9);
            Assert.Equal(550, alineamiento.EstacionFinal, 9);
        }

        [Fact]
        public void Rasante_con_curvas_parabolicas()
        {
            PerfilVertical rasante = new PerfilVertical("R", new[]
            {
                new PuntoVertical(1000, 100),
                new PuntoVertical(1150, 103, 100),
                new PuntoVertical(1300, 100, 80),
                new PuntoVertical(1400, 101)
            });

            Assert.Equal(100, rasante.CotaEn(1000), 9);
            Assert.Equal(102, rasante.CotaEn(1100), 9);   //Inicio de curva sobre la tangente
            Assert.Equal(102.5, rasante.CotaEn(1150), 9); //Centro: PIV + (g2 - g1) L / 8
            Assert.Equal(101, rasante.CotaEn(1250), 9);
            Assert.Equal(100.3, rasante.CotaEn(1300), 9);
            Assert.Equal(101, rasante.CotaEn(1400), 9);
            Assert.Equal(0, rasante.PendienteEn(1150), 9);
            Assert.Equal(-0.02, rasante.PendienteEn(1200), 9);
        }

        [Fact]
        public void Variable_interpola_linealmente()
        {
            Variable canto = new Variable { Nombre = "Canto" };
            canto.Valores.Add((0, 2));
            canto.Valores.Add((100, 4));

            Assert.Equal(2, canto.ValorEn(-10), 9);
            Assert.Equal(3, canto.ValorEn(50), 9);
            Assert.Equal(4, canto.ValorEn(200), 9);
        }

        [Theory]
        [InlineData(1234.5, "1+234.500")]
        [InlineData(0, "0+000.000")]
        [InlineData(999.9996, "1+000.000")]
        [InlineData(-12.25, "-0+012.250")]
        public void Formato_de_estacion(double estacion, string esperado)
        {
            Assert.Equal(esperado, Eje.FormatoEstacion(estacion));
        }
    }
}
