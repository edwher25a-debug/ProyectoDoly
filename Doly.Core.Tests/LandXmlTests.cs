using System.Text;
using Doly.Core.Ejes;
using Doly.Core.LandXml;
using Xunit;

namespace Doly.Core.Tests
{
    public class LandXmlTests
    {
        private static ArchivoLandXml LeerPrueba() =>
            LectorLandXml.Leer(Path.Combine(AppContext.BaseDirectory, "Datos", "eje_prueba.xml"));

        [Fact]
        public void Lee_recta_clotoides_y_arco_sin_avisos()
        {
            ArchivoLandXml archivo = LeerPrueba();
            AlineamientoLandXml alineamiento = Assert.Single(archivo.Alineamientos);

            Assert.Equal("Eje Puente", alineamiento.Nombre);
            Assert.Empty(alineamiento.Avisos); //Cada tramo cierra en el punto final del archivo
            Assert.Equal(5, alineamiento.Horizontal.Elementos.Count);
            Assert.Equal(1000, alineamiento.Horizontal.EstacionInicial, 9);
            Assert.Equal(1400, alineamiento.Horizontal.EstacionFinal, 9);
        }

        [Fact]
        public void Puntos_conocidos_del_eje()
        {
            AlineamientoLandXml alineamiento = LeerPrueba().Alineamientos[0];
            Eje eje = alineamiento.CrearEje(alineamiento.Rasantes[0]);

            //Coordenadas calculadas de forma independiente al generar el archivo de prueba
            PuntoEje fin = eje.Evaluar(1400);
            Assert.Equal(1350.608389, fin.Posicion.X, 4);
            Assert.Equal(5139.778985, fin.Posicion.Y, 4);
            Assert.Equal(101, fin.Posicion.Z, 9);
            Assert.Equal(0.8, fin.Rumbo, 9);

            PuntoEje finArco = eje.Evaluar(1260);
            Assert.Equal(1251.014795, finArco.Posicion.X, 4);
            Assert.Equal(5041.532638, finArco.Posicion.Y, 4);
        }

        [Fact]
        public void Lee_rasante_e_ignora_terreno()
        {
            AlineamientoLandXml alineamiento = LeerPrueba().Alineamientos[0];

            PerfilVertical rasante = Assert.Single(alineamiento.Rasantes);
            Assert.Equal("Rasante Proyecto", rasante.Nombre);
            Assert.Equal(102.5, rasante.CotaEn(1150), 9);
        }

        [Fact]
        public void Muestreo_incluye_cambios_de_tramo_y_curvas()
        {
            AlineamientoLandXml alineamiento = LeerPrueba().Alineamientos[0];
            Eje eje = alineamiento.CrearEje(alineamiento.Rasantes[0]);
            eje.Placements.Add(new Placement { Id = "P1", Estacion = 1123.4 });

            List<double> estaciones = eje.Estaciones(25);

            Assert.Contains(1100.0, estaciones);  //Fin de recta
            Assert.Contains(1160.0, estaciones);  //Fin de clotoide
            Assert.Contains(1260.0, estaciones);  //Fin de arco
            Assert.Contains(1260.0, estaciones);  //Fin de curva vertical
            Assert.Contains(1123.4, estaciones);  //Placement
            Assert.Equal(1000.0, estaciones[0]);
            Assert.Equal(1400.0, estaciones[^1]);
        }

        [Fact]
        public void Convierte_pies_y_usa_pntRef()
        {
            const string xml = @"<LandXML xmlns='http://www.landxml.org/schema/LandXML-1.2'>
  <Units><Imperial linearUnit='foot'/></Units>
  <CgPoints><CgPoint name='A'>0 0</CgPoint><CgPoint name='B'>0 100</CgPoint></CgPoints>
  <Alignments><Alignment name='Pies' staStart='0'>
    <CoordGeom><Line><Start pntRef='A'/><End pntRef='B'/></Line></CoordGeom>
  </Alignment></Alignments>
</LandXML>";

            ArchivoLandXml archivo = LectorLandXml.Leer(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
            AlineamientoLandXml alineamiento = archivo.Alineamientos[0];

            Assert.Equal("foot", archivo.Unidad);
            Assert.Equal(30.48, alineamiento.Horizontal.Longitud, 9);
            Assert.Equal(30.48, alineamiento.Horizontal.PuntoEn(30.48).X, 9);
            Assert.Empty(alineamiento.Rasantes);
        }

        [Fact]
        public void Avisa_si_un_tramo_no_cierra()
        {
            const string xml = @"<LandXML>
  <Alignments><Alignment name='Mal'>
    <CoordGeom><Curve rot='cw' radius='100' length='50'><Start>0 0</Start><Center>-100 0</Center><End>-5 90</End></Curve></CoordGeom>
  </Alignment></Alignments>
</LandXML>";

            AlineamientoLandXml alineamiento = LectorLandXml.Leer(new MemoryStream(Encoding.UTF8.GetBytes(xml))).Alineamientos[0];
            Assert.Single(alineamiento.Avisos);
        }
    }
}
