using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class EjePorTablaTests
    {
        [Fact]
        public void Curva_circular_en_un_vertice_de_90_grados()
        {
            List<PuntoInflexion> pis = new List<PuntoInflexion> { new(0, 0), new(200, 0, 100), new(200, 200) };
            ResultadoEjeDesdeTramos r = EjePorTabla.Crear(pis, new OpcionesEjeDesdeTramos { EstacionInicial = 500 });

            Assert.Equal(new[] { TipoElemento.Recta, TipoElemento.Arco, TipoElemento.Recta }, r.Eje.Horizontal.Elementos.Select(e => e.Tipo));
            Assert.Equal(100 + Math.PI * 50 + 100, r.Eje.Horizontal.Longitud, 6);
            Punto2 fin = r.Eje.Horizontal.PuntoEn(r.Eje.EstacionFinal);
            Assert.Equal(200, fin.X, 6);
            Assert.Equal(200, fin.Y, 6);
            Assert.Empty(r.Quiebres);
        }

        [Fact]
        public void Clotoides_simetricas_quedan_tangentes_y_llegan_al_ultimo_pi()
        {
            List<PuntoInflexion> pis = new List<PuntoInflexion> { new(0, 0), new(300, 0, 150, 60), new(300 + 300 * Math.Cos(1), 300 * Math.Sin(1)) };
            Eje eje = EjePorTabla.Crear(pis, new OpcionesEjeDesdeTramos()).Eje;

            Assert.Equal(new[] { TipoElemento.Recta, TipoElemento.Clotoide, TipoElemento.Arco, TipoElemento.Clotoide, TipoElemento.Recta },
                eje.Horizontal.Elementos.Select(e => e.Tipo));
            IReadOnlyList<ElementoHorizontal> el = eje.Horizontal.Elementos;
            for (int i = 1; i < el.Count; i++)
            {
                Assert.True(el[i].Inicio.Distancia(el[i - 1].Fin) < 1e-9);
                Assert.Equal(el[i - 1].RumboFin, el[i].RumboInicio, 9);
            }

            Punto2 fin = eje.Horizontal.PuntoEn(eje.EstacionFinal);
            Assert.True(fin.Distancia(pis[2].Punto) < 1e-3, fin.ToString());
            Assert.Equal(1, eje.Horizontal.RumboEn(eje.EstacionFinal), 6);

            //Simetria: las dos rectas miden lo mismo
            Assert.Equal(el[0].Longitud, el[4].Longitud, 3);
        }

        [Fact]
        public void Curvas_que_no_caben_se_explican()
        {
            List<PuntoInflexion> pis = new List<PuntoInflexion> { new(0, 0), new(100, 0, 400), new(100, 100) };
            Assert.Contains("no cabe", Assert.Throws<ArgumentException>(() => EjePorTabla.Crear(pis, new OpcionesEjeDesdeTramos())).Message);

            List<PuntoInflexion> giro = new List<PuntoInflexion> { new(0, 0), new(500, 0, 100, 300), new(1000, 50) };
            Assert.Contains("giran más", Assert.Throws<ArgumentException>(() => EjePorTabla.Crear(giro, new OpcionesEjeDesdeTramos())).Message);
        }

        [Fact]
        public void Vertice_sin_radio_es_un_quiebre()
        {
            List<PuntoInflexion> pis = new List<PuntoInflexion> { new(0, 0), new(100, 0), new(200, 100) };
            ResultadoEjeDesdeTramos r = EjePorTabla.Crear(pis, new OpcionesEjeDesdeTramos());
            Assert.Single(r.Quiebres);
            Assert.Equal(45, r.Quiebres[0].grados, 6);
        }

        [Fact]
        public void Lee_la_tabla()
        {
            List<PuntoInflexion> pis = EjePorTabla.Leer("1000; 2000\n1300,5; 2000; 150; 60\r\n1500\t2200");
            Assert.Equal(3, pis.Count);
            Assert.Equal(1300.5, pis[1].X, 9);
            Assert.Equal(150, pis[1].Radio);
            Assert.Equal(60, pis[1].Clotoide);
            Assert.Throws<ArgumentException>(() => EjePorTabla.Leer("1000"));
        }
    }
}
