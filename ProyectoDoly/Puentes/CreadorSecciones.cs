using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Planos;

namespace ProyectoDoly.Puentes
{
    public sealed class OpcionesSecciones
    {
        public EjeGuardado Eje { get; set; } = null!;
        public List<CorteSeccion> Cortes { get; set; } = new List<CorteSeccion>();

        //Tamano del recorte respecto al punto del eje (m): ancho total, por encima y por debajo de la rasante, profundidad vista
        public double Ancho { get; set; } = 20;
        public double Arriba { get; set; } = 5;
        public double Abajo { get; set; } = 25;
        public double Profundidad { get; set; } = 1;

        public ViewFamilyType? TipoVista { get; set; }
        public int Escala { get; set; } = 100;
    }

    /// <summary>
    ///     Crea vistas de seccion verticales en estaciones del eje (como Sections de SOFiSTiK), mirando en el sentido
    ///     del eje con la derecha a la derecha, como la vista previa de la seccion transversal
    /// </summary>
    public static class CreadorSecciones
    {
        public static List<ViewFamilyType> TiposVista(Document doc) =>
            new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .Where(t => t.ViewFamily == ViewFamily.Section).OrderBy(t => t.Name).ToList();

        public static List<ElementId> Crear(Document doc, OpcionesSecciones opciones)
        {
            Eje eje = opciones.Eje.Eje;
            Transform transformacion = opciones.Eje.Transformacion;
            ViewFamilyType tipo = opciones.TipoVista ?? TiposVista(doc).FirstOrDefault()
                                  ?? throw new InvalidOperationException("El proyecto no tiene ningún tipo de vista de sección.");

            double Pies(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
            HashSet<string> nombres = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Select(v => v.Name));
            List<ElementId> vistas = new List<ElementId>();

            using (Transaction transaccion = new Transaction(doc, $"Secciones {eje.Nombre}"))
            {
                transaccion.Start();
                foreach (CorteSeccion corte in opciones.Cortes)
                {
                    Punto3 punto = eje.Evaluar(corte.Estacion).Posicion;
                    Punto2 mirada = EstacionesSeccion.Mirada(eje, corte);

                    //Revit mira hacia -Z de la caja: Z = contra la mirada, Y = arriba, X = Y x Z (la derecha mirando hacia adelante)
                    XYZ z = transformacion.OfVector(new XYZ(-mirada.X, -mirada.Y, 0)).Normalize();
                    XYZ y = XYZ.BasisZ;
                    Transform marco = Transform.Identity;
                    marco.Origin = transformacion.OfPoint(CreadorEje.APies(punto));
                    marco.BasisX = y.CrossProduct(z).Normalize();
                    marco.BasisY = y;
                    marco.BasisZ = z;

                    BoundingBoxXYZ caja = new BoundingBoxXYZ
                    {
                        Transform = marco,
                        Min = new XYZ(-Pies(opciones.Ancho / 2), -Pies(opciones.Abajo), -Pies(opciones.Profundidad)),
                        Max = new XYZ(Pies(opciones.Ancho / 2), Pies(opciones.Arriba), 0)
                    };

                    ViewSection vista = ViewSection.CreateSection(doc, tipo.Id, caja);
                    vista.Scale = opciones.Escala;
                    vista.Name = Libre(nombres, $"Sección {eje.Nombre} {corte.Nombre} ({Eje.FormatoEstacion(corte.Estacion)})");
                    vistas.Add(vista.Id);
                }

                transaccion.Commit();
            }

            return vistas;
        }

        //Revit no admite nombres de vista repetidos ni algunos caracteres
        private static string Libre(HashSet<string> usados, string nombre)
        {
            string limpio = new string(nombre.Select(c => "\\:{}[]|;<>?`~".IndexOf(c) >= 0 ? '-' : c).ToArray());
            string libre = limpio;
            for (int i = 2; usados.Contains(libre); i++) libre = $"{limpio} ({i})";
            usados.Add(libre);
            return libre;
        }
    }
}
