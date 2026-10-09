using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes
{
    //Eje leido del modelo: el elemento dibujado, la geometria del eje y como se ubico en Revit
    public sealed class EjeGuardado
    {
        public ElementId Id { get; set; } = ElementId.InvalidElementId;
        public Eje Eje { get; set; } = null!;

        //Coordenadas del eje (en pies) a coordenadas internas de Revit
        public Transform Transformacion { get; set; } = Transform.Identity;

        public override string ToString() =>
            $"{Eje.Nombre}  ({Eje.FormatoEstacion(Eje.EstacionInicial)} a {Eje.FormatoEstacion(Eje.EstacionFinal)})";
    }

    /// <summary>
    ///     Guarda el eje dentro del elemento que lo dibuja (Extensible Storage), para que las demas herramientas
    ///     (tablero, pilas...) lo usen sin volver a leer el LandXML.
    /// </summary>
    public static class DatosEje
    {
        private static readonly Guid IdEsquema = new Guid("4b9eef55-0263-42d4-ac34-d4b466cdf0c7");
        private const string CampoEje = "Eje";
        private const string CampoTransformacion = "Transformacion";

        public static void Guardar(Element elemento, Eje eje, Transform transformacion)
        {
            Entity entidad = new Entity(Esquema());
            entidad.Set(CampoEje, SerializadorEje.Escribir(eje));
            entidad.Set(CampoTransformacion, EscribirTransformacion(transformacion));
            elemento.SetEntity(entidad);
        }

        public static EjeGuardado? Leer(Element elemento)
        {
            Schema? esquema = Schema.Lookup(IdEsquema);
            if (esquema == null) return null;

            Entity entidad = elemento.GetEntity(esquema);
            if (entidad == null || !entidad.IsValid()) return null;

            return new EjeGuardado
            {
                Id = elemento.Id,
                Eje = SerializadorEje.Leer(entidad.Get<string>(CampoEje)),
                Transformacion = LeerTransformacion(entidad.Get<string>(CampoTransformacion))
            };
        }

        //Ejes importados en el modelo (los importados con versiones anteriores no tienen datos y no aparecen)
        public static List<EjeGuardado> Ejes(Document doc)
        {
            List<EjeGuardado> ejes = new List<EjeGuardado>();
            if (Schema.Lookup(IdEsquema) == null) return ejes;

            foreach (DirectShape forma in new FilteredElementCollector(doc).OfClass(typeof(DirectShape)).Cast<DirectShape>())
            {
                try
                {
                    EjeGuardado? eje = Leer(forma);
                    if (eje != null) ejes.Add(eje);
                }
                catch (FormatException)
                {
                    //Datos danados: se ignora ese eje
                }
            }

            return ejes.OrderBy(e => e.Eje.Nombre).ToList();
        }

        private static Schema Esquema()
        {
            Schema? existente = Schema.Lookup(IdEsquema);
            if (existente != null) return existente;

            SchemaBuilder constructor = new SchemaBuilder(IdEsquema);
            constructor.SetSchemaName("ProyectoDoly_Eje");
            constructor.SetDocumentation("Eje de puente importado con ProyectoDoly (alineamiento, rasante y ubicación).");
            constructor.SetReadAccessLevel(AccessLevel.Public);
            constructor.SetWriteAccessLevel(AccessLevel.Public);
            constructor.AddSimpleField(CampoEje, typeof(string));
            constructor.AddSimpleField(CampoTransformacion, typeof(string));
            return constructor.Finish();
        }

        private static string EscribirTransformacion(Transform t) =>
            string.Join(";", new[] { t.Origin, t.BasisX, t.BasisY, t.BasisZ }
                .SelectMany(v => new[] { v.X, v.Y, v.Z })
                .Select(n => n.ToString("R", CultureInfo.InvariantCulture)));

        private static Transform LeerTransformacion(string texto)
        {
            double[] n = texto.Split(';').Select(v => double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
            if (n.Length != 12) throw new FormatException("La ubicación guardada del eje no es válida.");

            Transform t = Transform.Identity;
            t.Origin = new XYZ(n[0], n[1], n[2]);
            t.BasisX = new XYZ(n[3], n[4], n[5]);
            t.BasisY = new XYZ(n[6], n[7], n[8]);
            t.BasisZ = new XYZ(n[9], n[10], n[11]);
            return t;
        }
    }
}
