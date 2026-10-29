using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class EjePorElementosTests
    {
        //El ejemplo del cuadro Axis Data: recta 30, clotoide 60 de 0 a R 450 y arco 60 de R 450
        private static List<DatoElemento> Ejemplo(double signo = 1) => new List<DatoElemento>
        {
            new(TipoElemento.Recta, 30),
            new(TipoElemento.Clotoide, 60, 0, 450 * signo),
            new(TipoElemento.Arco, 60, 450 * signo)
        };

        [Fact]
        public void Estaciones_y_continuidad()
        {
            AlineamientoHorizontal h = EjePorElementos.Crear(new Punto2(0, 0), new Punto2(1, 0), 0, Ejemplo());

            Assert.Equal(new[] { 0.0, 30, 90 }, Enumerable.Range(0, 3).Select(h.EstacionInicioDe));
            Assert.Equal(150, h.EstacionFinal, 9);
            for (int i = 1; i < h.Elementos.Count; i++)
            {
                Assert.True(h.Elementos[i].Inicio.Distancia(h.Elementos[i - 1].Fin) < 1e-9);
                Assert.Equal(h.Elementos[i - 1].RumboFin, h.Elementos[i].RumboInicio, 12);
                Assert.Equal(h.Elementos[i - 1].CurvaturaFin, h.Elementos[i].CurvaturaInicio, 12);
            }

            //Radio positivo = curva a la derecha: el rumbo baja y el eje queda al sur
            Assert.True(h.RumboEn(150) < 0);
            Assert.True(h.PuntoEn(150).Y < 0);
            Assert.Equal(-(60 / (2 * 450.0) + 60 / 450.0), h.RumboEn(150), 9);
        }

        [Fact]
        public void Radio_negativo_gira_a_la_izquierda_y_simetrico()
        {
            AlineamientoHorizontal der = EjePorElementos.Crear(new Punto2(0, 0), new Punto2(1, 0), 0, Ejemplo());
            AlineamientoHorizontal izq = EjePorElementos.Crear(new Punto2(0, 0), new Punto2(1, 0), 0, Ejemplo(-1));
            Punto2 a = der.PuntoEn(150), b = izq.PuntoEn(150);
            Assert.Equal(a.X, b.X, 9);
            Assert.Equal(-a.Y, b.Y, 9);
        }

        [Fact]
        public void Origen_direccion_y_vuelta_a_filas()
        {
            AlineamientoHorizontal h = EjePorElementos.Crear(new Punto2(1000, 2000), new Punto2(0, 2), 400000, Ejemplo());
            Assert.Equal(Math.PI / 2, h.RumboEn(400000), 12);
            Assert.Equal(1000, h.PuntoEn(400010).X, 9);
            Assert.Equal(2010, h.PuntoEn(400010).Y, 9);

            List<DatoElemento> filas = EjePorElementos.Filas(h);
            Assert.Equal(3, filas.Count);
            Assert.Null(filas[0].RadioInicio);
            Assert.Equal(0, filas[1].RadioInicio!.Value, 9);
            Assert.Equal(450, filas[1].RadioFin!.Value, 6);
            Assert.Equal(450, filas[2].RadioInicio!.Value, 6);
        }

        [Fact]
        public void Errores_por_fila()
        {
            Assert.Contains("Elemento 2", Assert.Throws<ArgumentException>(() =>
                EjePorElementos.Crear(new Punto2(0, 0), new Punto2(1, 0), 0, new[] { new DatoElemento(TipoElemento.Recta, 10), new DatoElemento(TipoElemento.Arco, 10) })).Message);
            Assert.Throws<ArgumentException>(() => EjePorElementos.Crear(new Punto2(0, 0), new Punto2(1, 0), 0, new[] { new DatoElemento(TipoElemento.Clotoide, 10, 300, 300) }));
            Assert.Throws<ArgumentException>(() => EjePorElementos.Crear(new Punto2(0, 0), new Punto2(0, 0), 0, Ejemplo()));
        }
    }
}
