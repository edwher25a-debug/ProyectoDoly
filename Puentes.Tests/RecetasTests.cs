using System;
using System.Collections.Generic;
using System.Linq;
using ProyectoDoly.Puentes.Dependencias;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Superestructura;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class RecetasTests
    {
        [Fact]
        public void Receta_ida_y_vuelta_con_textos_raros()
        {
            Receta r = new Receta(Recetas.Subestructura).Con("placement", "P|1 = pila\nnorte").Con("lateral", -4.5).Con("vertical", true);
            Receta leida = Receta.Leer(r.Escribir());
            Assert.Equal(Recetas.Subestructura, leida.Tipo);
            Assert.Equal("P|1 = pila\nnorte", leida.Texto("placement"));
            Assert.Equal(-4.5, leida.Numero("lateral"));
            Assert.True(leida.SiNo("vertical"));
            Assert.Equal(7, leida.Numero("falta", 7));
            Assert.Throws<FormatException>(() => Receta.Leer("otra cosa"));
        }

        [Fact]
        public void Seccion_con_hueco_conserva_el_orden_de_vertices()
        {
            Pieza cajon = new Pieza(new Contorno(new[] { new Punto2(-3, 0), new Punto2(3, 0), new Punto2(3, -2), new Punto2(-3, -2) }),
                new[] { new Contorno(new[] { new Punto2(-1, -0.5), new Punto2(1, -0.5), new Punto2(1, -1.5), new Punto2(-1, -1.5) }) });
            Pieza losa = new Pieza(new Contorno(new[] { new Punto2(4, 0), new Punto2(6, 0), new Punto2(6, -0.3) }));
            SeccionTransversal s = new SeccionTransversal("Cajón", new[] { cajon, losa });

            SeccionTransversal leida = Receta.Seccion("Cajón", Receta.Seccion(s));
            Assert.Equal(2, leida.Piezas.Count);
            Assert.Single(leida.Piezas[0].Huecos);
            Assert.Equal(s.Area, leida.Area, 9);
            for (int p = 0; p < s.Piezas.Count; p++)
                Assert.Equal(s.Piezas[p].Contornos.SelectMany(c => c.Puntos).Select(q => (q.X, q.Y)),
                    leida.Piezas[p].Contornos.SelectMany(c => c.Puntos).Select(q => (q.X, q.Y)));
        }

        [Fact]
        public void Variables_y_eje_secundario()
        {
            Variable canto = new Variable { Nombre = "Canto / h" };
            canto.Valores.Add((0, 1.8));
            canto.Valores.Add((40, 2.5));
            List<Variable> leidas = Receta.Variables(Receta.Variables(new[] { canto }));
            Assert.Equal("Canto / h", leidas[0].Nombre);
            Assert.Equal(2.5, leidas[0].ValorEn(40));

            OpcionesEjeSecundario o = new OpcionesEjeSecundario { Nombre = "Borde", Subir = -0.3, Derecha = { (0, 6), (80, 8) } };
            OpcionesEjeSecundario b = Recetas.OpcionesEjeSecundario(Receta.Leer(Recetas.DeEjeSecundario(o).Escribir()));
            Assert.Equal("Borde", b.Nombre);
            Assert.Equal(-0.3, b.Subir);
            Assert.Equal(o.Derecha, b.Derecha);
        }

        [Fact]
        public void Estacion_sigue_al_placement()
        {
            Eje eje = new Eje("E", new AlineamientoHorizontal(new[] { new ElementoHorizontal(TipoElemento.Recta, new Punto2(0, 0), 0, 100, 0, 0) }, 0), null);
            eje.Placements.Add(new Placement { Id = "P2", Estacion = 60 });
            Assert.Equal(60, Recetas.Estacion(eje, "P2", 30));
            Assert.Equal(30, Recetas.Estacion(eje, "P9", 30));
            Assert.Equal(100, Recetas.Estacion(eje, "", 150));
        }
    }
}
