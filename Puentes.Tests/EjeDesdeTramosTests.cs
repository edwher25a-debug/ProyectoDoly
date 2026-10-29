using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Ejes;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class EjeDesdeTramosTests
    {
        //Recta de 100 m hacia el Este, arco de R = 50 girando 90° a la izquierda y recta de 60 m hacia el Norte
        private static List<TramoDibujado> Dibujo() => new List<TramoDibujado>
        {
            TramoDibujado.Recta(new Punto2(0, 0), new Punto2(100, 0)),
            TramoDibujado.Arco(new Punto2(100, 0), new Punto2(150, 50), new Punto2(100, 50), true),
            TramoDibujado.Recta(new Punto2(150, 50), new Punto2(150, 110))
        };

        [Fact]
        public void Encadena_recta_arco_recta_con_su_longitud()
        {
            ResultadoEjeDesdeTramos r = EjeDesdeTramos.Crear(Dibujo(), new OpcionesEjeDesdeTramos { EstacionInicial = 1000, CotaInicial = 50, PendientePorciento = 2 });

            Assert.Equal(2, r.Rectas);
            Assert.Equal(1, r.Arcos);
            Assert.Empty(r.Quiebres);
            Assert.Equal(100 + Math.PI * 25 + 60, r.Eje.Horizontal.Longitud, 6);
            Punto2 fin = r.Eje.Horizontal.PuntoEn(r.Eje.EstacionFinal);
            Assert.Equal(150, fin.X, 6);
            Assert.Equal(110, fin.Y, 6);

            //Mitad del arco: a 50 m del centro, en la diagonal
            Punto2 medio = r.Eje.Horizontal.PuntoEn(1100 + Math.PI * 12.5);
            Assert.Equal(50, medio.Distancia(new Punto2(100, 50)), 6);
            Assert.Equal(medio.X - 100, 50 - medio.Y, 6);

            Assert.Equal(50, r.Eje.Vertical.CotaEn(1000), 9);
            Assert.Equal(50 + 0.02 * r.Eje.Horizontal.Longitud, r.Eje.Vertical.CotaEn(r.Eje.EstacionFinal), 9);
        }

        [Fact]
        public void Admite_cualquier_orden_y_sentido()
        {
            List<TramoDibujado> d = Dibujo();
            List<TramoDibujado> revuelto = new List<TramoDibujado> { d[2].Invertido(), d[0], d[1].Invertido() };

            //Empieza en el extremo mas cercano al punto indicado
            ResultadoEjeDesdeTramos r = EjeDesdeTramos.Crear(revuelto, new OpcionesEjeDesdeTramos { CercaDe = new Punto2(1, 1) });
            Punto2 inicio = r.Eje.Horizontal.PuntoEn(0);
            Assert.Equal(0, inicio.Distancia(new Punto2(0, 0)), 6);
            Assert.Equal(new Punto2(150, 110).X, r.Eje.Horizontal.PuntoEn(r.Eje.EstacionFinal).X, 6);
            Assert.Empty(r.Quiebres);

            ResultadoEjeDesdeTramos alReves = EjeDesdeTramos.Crear(revuelto, new OpcionesEjeDesdeTramos { CercaDe = new Punto2(150, 109) });
            Assert.Equal(0, alReves.Eje.Horizontal.PuntoEn(0).Distancia(new Punto2(150, 110)), 6);
            Assert.Equal(r.Eje.Horizontal.Longitud, alReves.Eje.Horizontal.Longitud, 6);
        }

        [Fact]
        public void Arco_horario_y_quiebre()
        {
            List<TramoDibujado> d = new List<TramoDibujado>
            {
                TramoDibujado.Recta(new Punto2(0, 0), new Punto2(100, 0)),
                TramoDibujado.Arco(new Punto2(100, 0), new Punto2(150, -50), new Punto2(100, -50), false),
                TramoDibujado.Recta(new Punto2(150, -50), new Punto2(200, -100))
            };
            ResultadoEjeDesdeTramos r = EjeDesdeTramos.Crear(d, new OpcionesEjeDesdeTramos());

            Assert.Equal(100 + Math.PI * 25 + Math.Sqrt(2) * 50, r.Eje.Horizontal.Longitud, 6);
            Assert.Single(r.Quiebres);
            Assert.Equal(45, r.Quiebres[0].grados, 6);
        }

        [Fact]
        public void Huecos_y_ramales_se_explican()
        {
            List<TramoDibujado> hueco = new List<TramoDibujado>
            {
                TramoDibujado.Recta(new Punto2(0, 0), new Punto2(100, 0)),
                TramoDibujado.Recta(new Punto2(100.5, 0), new Punto2(200, 0))
            };
            Assert.Contains("extremos sueltos", Assert.Throws<ArgumentException>(() => EjeDesdeTramos.Crear(hueco, new OpcionesEjeDesdeTramos())).Message);

            List<TramoDibujado> ramal = Dibujo();
            ramal.Add(TramoDibujado.Recta(new Punto2(100, 0), new Punto2(100, -30)));
            Assert.Contains("ramales", Assert.Throws<ArgumentException>(() => EjeDesdeTramos.Crear(ramal, new OpcionesEjeDesdeTramos())).Message);
        }

        [Fact]
        public void Lee_tabla_de_piv()
        {
            List<PuntoVertical> piv = EjeDesdeTramos.LeerPiv("0+120 = 548 / 80\n0+000 = 545,30; 0+300 = 546.5");
            Assert.Equal(3, piv.Count);
            Assert.Equal(0, piv[0].Estacion);
            Assert.Equal(545.3, piv[0].Cota, 9);
            Assert.Equal(80, piv[1].LongitudCurva);

            OpcionesEjeDesdeTramos opciones = new OpcionesEjeDesdeTramos();
            opciones.Piv.AddRange(piv);
            Eje eje = EjeDesdeTramos.Crear(Dibujo(), opciones).Eje;
            Assert.True(eje.TieneRasante);
            Assert.Equal(548 - (548 - 545.3) / 120 * 40 , eje.Vertical.CotaEn(80), 6);

            Assert.Throws<ArgumentException>(() => EjeDesdeTramos.LeerPiv("0+000 545"));
        }
    }
}
