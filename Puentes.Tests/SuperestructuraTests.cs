using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.LandXml;
using ProyectoDoly.Puentes.Superestructura;
using Xunit;

namespace ProyectoDoly.Puentes.Tests
{
    public class SuperestructuraTests
    {
        private static Contorno Rectangulo(double x0, double y0, double x1, double y1) =>
            new Contorno(new[] { new Punto2(x0, y0), new Punto2(x1, y0), new Punto2(x1, y1), new Punto2(x0, y1) });

        private static Eje EjeRecto(double longitud, PerfilVertical? rasante = null) =>
            new Eje("Recto", new AlineamientoHorizontal(new[]
            {
                new ElementoHorizontal(TipoElemento.Recta, new Punto2(100, 200), 0.3, longitud, 0, 0)
            }, 0), rasante);

        //Cajon: 10 x 2 m con hueco de 6 x 1.2 m
        private static SeccionTransversal Cajon() => SeccionTransversal.DesdeContornos("Cajón", new[]
        {
            Rectangulo(-3, -1.6, 3, -0.4),
            Rectangulo(-5, -2, 5, 0)
        });

        [Fact]
        public void Contornos_anidados_forman_pieza_con_hueco()
        {
            SeccionTransversal seccion = Cajon();

            Pieza pieza = Assert.Single(seccion.Piezas);
            Assert.Single(pieza.Huecos);
            Assert.Equal(20 - 7.2, seccion.Area, 9);
            Assert.True(pieza.Exterior.AreaConSigno > 0);
            Assert.True(pieza.Huecos[0].AreaConSigno < 0);
        }

        [Fact]
        public void Contornos_separados_son_piezas_distintas_y_una_isla_en_un_hueco_vuelve_a_ser_pieza()
        {
            SeccionTransversal vigas = SeccionTransversal.DesdeContornos("Vigas", new[]
            {
                Rectangulo(-5, -0.25, 5, 0),
                Rectangulo(-3, -1.5, -2.6, -0.25),
                Rectangulo(2.6, -1.5, 3, -0.25)
            });
            Assert.Equal(3, vigas.Piezas.Count);
            Assert.All(vigas.Piezas, p => Assert.Empty(p.Huecos));

            SeccionTransversal isla = SeccionTransversal.DesdeContornos("Isla", new[]
            {
                Rectangulo(-5, -5, 5, 5), Rectangulo(-3, -3, 3, 3), Rectangulo(-1, -1, 1, 1)
            });
            Assert.Equal(2, isla.Piezas.Count);
            Assert.Equal(100 - 36 + 4, isla.Area, 9);
        }

        [Fact]
        public void Contorno_quita_cierre_duplicado_y_puntos_alineados()
        {
            Contorno contorno = new Contorno(new[]
            {
                new Punto2(0, 0), new Punto2(1, 0), new Punto2(2, 0), new Punto2(2, 1), new Punto2(0, 1), new Punto2(0, 0)
            });

            Assert.Equal(4, contorno.Puntos.Count);
            Assert.Equal(2, contorno.Area, 12);
        }

        [Fact]
        public void Contorno_une_puntos_a_menos_de_2_mm()
        {
            //Un arco teselado muy fino deja puntos casi iguales que Revit no acepta como bordes
            Contorno contorno = new Contorno(new[]
            {
                new Punto2(0, 0), new Punto2(1, 0), new Punto2(1.0005, 0.0005), new Punto2(1, 1), new Punto2(0, 1), new Punto2(0.0001, 0)
            });

            Assert.Equal(4, contorno.Puntos.Count);
            for (int i = 0; i < contorno.Puntos.Count; i++)
                Assert.True(contorno.Puntos[i].Distancia(contorno.Puntos[(i + 1) % contorno.Puntos.Count]) > 0.002);
        }

        [Fact]
        public void Triangulacion_cubre_el_area_de_piezas_con_huecos_y_concavas()
        {
            SeccionTransversal[] secciones =
            {
                Cajon(),
                SeccionTransversal.DesdeContornos("Bicelular", new[]
                {
                    Rectangulo(-6, -2, 6, 0), Rectangulo(-5, -1.7, -0.3, -0.3), Rectangulo(0.3, -1.7, 5, -0.3)
                }),
                SeccionTransversal.DesdeContornos("Losa con voladizos", new[]
                {
                    new Contorno(new[]
                    {
                        new Punto2(-6, 0), new Punto2(0, 0.12), new Punto2(6, 0), new Punto2(6, -0.2), new Punto2(3, -0.35),
                        new Punto2(2.5, -1.8), new Punto2(-2.5, -1.8), new Punto2(-3, -0.35), new Punto2(-6, -0.2)
                    })
                })
            };

            foreach (SeccionTransversal seccion in secciones)
            foreach (Pieza pieza in seccion.Piezas)
            {
                List<Contorno> contornos = pieza.Contornos.ToList();
                Punto2 P(RefVertice r) => contornos[r.Contorno].Puntos[r.Vertice];

                var triangulos = Triangulacion.Triangular(pieza);
                double area = triangulos.Sum(t => ((P(t.b).X - P(t.a).X) * (P(t.c).Y - P(t.a).Y) - (P(t.b).Y - P(t.a).Y) * (P(t.c).X - P(t.a).X)) / 2);

                Assert.All(triangulos, t => Assert.True((P(t.b).X - P(t.a).X) * (P(t.c).Y - P(t.a).Y) - (P(t.b).Y - P(t.a).Y) * (P(t.c).X - P(t.a).X) > 0));
                Assert.Equal(pieza.Area, area, 9);
            }
        }

        [Fact]
        public void Malla_solo_tiene_triangulos()
        {
            List<SolidoBarrido> solidos = BarridoTablero.Generar(EjeRecto(20), Cajon(), new OpcionesBarrido { EstacionInicial = 0, EstacionFinal = 20, Paso = 5 });

            Assert.All(solidos[0].Caras, c => Assert.Equal(3, Assert.Single(c.Lazos).Count));
        }

        [Fact]
        public void Puntos_adaptativos_mismo_orden_en_cada_estacion()
        {
            SeccionTransversal cajon = Cajon();
            OpcionesBarrido opciones = new OpcionesBarrido { EstacionInicial = 0, EstacionFinal = 20, Paso = 5 };

            List<Punto3[]> estaciones = BarridoTablero.PuntosPorEstacion(EjeRecto(20), cajon, opciones);

            Assert.Equal(BarridoTablero.Estaciones(EjeRecto(20), opciones).Count, estaciones.Count);
            int vertices = cajon.Piezas.Sum(p => p.Contornos.Sum(c => c.Puntos.Count));
            Assert.All(estaciones, e => Assert.Equal(vertices, e.Length));

            //En recta, cada vertice avanza exactamente el paso entre una estacion y la siguiente
            for (int j = 0; j < vertices; j++)
            {
                Punto3 a = estaciones[0][j], b = estaciones[1][j];
                Assert.Equal(5, Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2)), 9);
            }
        }

        [Fact]
        public void Volumen_en_recta_es_area_por_longitud_y_la_malla_mira_hacia_afuera()
        {
            Eje eje = EjeRecto(50);
            List<SolidoBarrido> solidos = BarridoTablero.Generar(eje, Cajon(), new OpcionesBarrido { EstacionInicial = 0, EstacionFinal = 50, Paso = 5 });

            //Volumen positivo = normales hacia afuera
            Assert.Equal((20 - 7.2) * 50, BarridoTablero.Volumen(Assert.Single(solidos)), 6);
        }

        [Fact]
        public void Espejo_conserva_area_y_orientacion()
        {
            SeccionTransversal asimetrica = SeccionTransversal.DesdeContornos("L", new[]
            {
                new Contorno(new[] { new Punto2(0, 0), new Punto2(4, 0), new Punto2(4, -0.3), new Punto2(1, -0.3), new Punto2(1, -2), new Punto2(0, -2) })
            });

            SeccionTransversal espejo = asimetrica.Espejo();

            Assert.Equal(asimetrica.Area, espejo.Area, 12);
            Assert.Equal(-4, espejo.MinX, 12);
            Assert.True(espejo.Piezas[0].Exterior.AreaConSigno > 0);
        }

        [Fact]
        public void Volumen_con_pendiente_es_area_por_longitud_en_planta()
        {
            //Secciones verticales: con rasante inclinada el volumen sigue siendo area x longitud horizontal
            PerfilVertical rasante = new PerfilVertical("6%", new[] { new PuntoVertical(0, 2600), new PuntoVertical(80, 2604.8) });
            Eje eje = EjeRecto(80, rasante);
            SeccionTransversal losa = SeccionTransversal.DesdeContornos("Losa", new[] { Rectangulo(-6, -0.3, 6, 0) });

            List<SolidoBarrido> solidos = BarridoTablero.Generar(eje, losa, new OpcionesBarrido { EstacionInicial = 10, EstacionFinal = 70, Paso = 2 });

            Assert.Equal(3.6 * 60, BarridoTablero.Volumen(solidos[0]), 6);
            Assert.Equal(2600.6 - 0.3, solidos[0].Caras.SelectMany(c => c.Lazos).SelectMany(l => l).Min(p => p.Z), 9);
        }

        [Fact]
        public void Volumen_en_curva_cumple_pappus()
        {
            //Arco a la izquierda R = 100 m, seccion desplazada 2 m a la derecha: el centroide recorre R + 2
            double radio = 100, largo = 60;
            Eje eje = new Eje("Curva", new AlineamientoHorizontal(new[]
            {
                new ElementoHorizontal(TipoElemento.Arco, new Punto2(0, 0), 0, largo, 1 / radio, 1 / radio)
            }, 0), null);
            SeccionTransversal losa = SeccionTransversal.DesdeContornos("Losa", new[] { Rectangulo(-4, -1, 4, 0) });

            List<SolidoBarrido> solidos = BarridoTablero.Generar(eje, losa,
                new OpcionesBarrido { EstacionInicial = 0, EstacionFinal = largo, Paso = 0.5, DesplazamientoLateral = 2 });

            double esperado = 8 * (radio + 2) * largo / radio;
            Assert.Equal(esperado, BarridoTablero.Volumen(solidos[0]), 1);
        }

        [Fact]
        public void Estaciones_respetan_rango_paso_y_cambios_de_tramo()
        {
            ArchivoLandXml archivo = LectorLandXml.Leer(Path.Combine(AppContext.BaseDirectory, "Datos", "eje_prueba.xml"));
            AlineamientoLandXml alineamiento = archivo.Alineamientos[0];
            Eje eje = alineamiento.CrearEje(alineamiento.Rasantes[0]);

            List<double> estaciones = BarridoTablero.Estaciones(eje, new OpcionesBarrido { EstacionInicial = 1050, EstacionFinal = 1203.7, Paso = 10 });

            Assert.Equal(1050, estaciones.First(), 9);
            Assert.Equal(1203.7, estaciones.Last(), 9);
            Assert.All(estaciones.Zip(estaciones.Skip(1)), par => Assert.InRange(par.Second - par.First, 0.01, 10 + 1e-9));
            for (int i = 0; i < eje.Horizontal.Elementos.Count; i++)
            {
                double cambio = eje.Horizontal.EstacionInicioDe(i);
                if (cambio > 1050 && cambio < 1203.7) Assert.Contains(estaciones, e => Math.Abs(e - cambio) < 1e-6);
            }
        }

        [Fact]
        public void Eje_guardado_se_lee_identico()
        {
            ArchivoLandXml archivo = LectorLandXml.Leer(Path.Combine(AppContext.BaseDirectory, "Datos", "eje_prueba.xml"));
            AlineamientoLandXml alineamiento = archivo.Alineamientos[0];
            Eje original = alineamiento.CrearEje(alineamiento.Rasantes[0]);

            Eje leido = SerializadorEje.Leer(SerializadorEje.Escribir(original));

            Assert.Equal(original.Nombre, leido.Nombre);
            Assert.Equal(original.Vertical.Nombre, leido.Vertical.Nombre);
            Assert.True(leido.TieneRasante);
            for (double e = original.EstacionInicial; e <= original.EstacionFinal; e += 7.3)
            {
                Assert.Equal(original.Evaluar(e).Posicion.X, leido.Evaluar(e).Posicion.X, 12);
                Assert.Equal(original.Evaluar(e).Posicion.Y, leido.Evaluar(e).Posicion.Y, 12);
                Assert.Equal(original.Evaluar(e).Posicion.Z, leido.Evaluar(e).Posicion.Z, 12);
            }

            Eje sinRasante = SerializadorEje.Leer(SerializadorEje.Escribir(alineamiento.CrearEje(null)));
            Assert.False(sinRasante.TieneRasante);
        }

        [Fact]
        public void Variable_de_seccion_se_lee_por_estacion_e_interpola()
        {
            Variable? canto = SeccionVariable.Leer("Height", "0+000 = 1,20; 40 = 1.80\n0+080=1.2");
            Assert.NotNull(canto);
            Assert.Equal(3, canto!.Valores.Count);
            Assert.Equal(1.5, canto.ValorEn(20), 9);
            Assert.Equal(1.2, canto.ValorEn(200), 9);
            Assert.Null(SeccionVariable.Leer("Height", "  "));
            Assert.Throws<ArgumentException>(() => SeccionVariable.Leer("Height", "0+000 1.2"));
            Assert.Throws<ArgumentException>(() => SeccionVariable.Leer("Height", "10=1; 10=2"));
        }

        [Fact]
        public void Alinear_reordena_piezas_y_vertices_como_la_seccion_anterior()
        {
            SeccionTransversal referencia = SeccionTransversal.DesdeContornos("A", new[] { Rectangulo(-5, -0.3, 5, 0), Rectangulo(-1, -2, 1, -0.3) });

            //Misma forma algo mas alta, con las piezas en otro orden y cada contorno empezando en otro vertice
            Contorno losa = new Contorno(new[] { new Punto2(5, 0), new Punto2(-5, 0), new Punto2(-5, -0.3), new Punto2(5, -0.3) });
            Contorno alma = new Contorno(new[] { new Punto2(1, -2.4), new Punto2(1, -0.3), new Punto2(-1, -0.3), new Punto2(-1, -2.4) });
            SeccionTransversal nueva = new SeccionTransversal("B", new[] { new Pieza(alma), new Pieza(losa) });

            SeccionTransversal alineada = SeccionVariable.Alinear(nueva, referencia);

            for (int p = 0; p < 2; p++)
            for (int v = 0; v < 4; v++)
            {
                Punto2 a = referencia.Piezas[p].Exterior.Puntos[v], b = alineada.Piezas[p].Exterior.Puntos[v];
                Assert.True(a.Distancia(b) < 0.5, $"pieza {p} vertice {v}");
            }

            SeccionTransversal otra = SeccionTransversal.DesdeContornos("C", new[] { Rectangulo(-5, -0.3, 5, 0) });
            Assert.Throws<InvalidOperationException>(() => SeccionVariable.Alinear(otra, referencia));
        }

        [Fact]
        public void Barrido_con_canto_variable_da_el_volumen_del_trapecio_y_secciones_en_las_estaciones_de_la_variable()
        {
            Eje eje = EjeRecto(100);
            OpcionesBarrido opciones = new OpcionesBarrido { EstacionInicial = 0, EstacionFinal = 60, Paso = 25, EstacionesExtra = { 33 } };
            List<double> estaciones = BarridoTablero.Estaciones(eje, opciones);
            Assert.Contains(estaciones, e => Math.Abs(e - 33) < 1e-9);

            //Canto de 1 m a 2 m: rectangulo de 4 m de ancho
            List<SeccionTransversal> secciones = estaciones
                .Select(e => SeccionTransversal.DesdeContornos("V", new[] { Rectangulo(-2, -(1 + e / 60), 2, 0) }))
                .ToList();

            SolidoBarrido solido = Assert.Single(BarridoTablero.Generar(eje, secciones, opciones));
            Assert.Equal(4 * 1.5 * 60, BarridoTablero.Volumen(solido), 6);
            Assert.Equal(estaciones.Count, BarridoTablero.PuntosPorEstacion(eje, secciones, opciones).Count);
            Assert.Throws<ArgumentException>(() => BarridoTablero.Generar(eje, secciones.Skip(1).ToList(), opciones));
        }
    }
}
