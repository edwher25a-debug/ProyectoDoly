using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using ProyectoDoly.Puentes.Dependencias;

namespace ProyectoDoly.Puentes
{
    /// <summary>
    ///     Ata un elemento (tablero, pila, eje secundario) al eje del que depende y guarda su receta (Extensible Storage),
    ///     para moverlo o volver a crearlo cuando el eje cambia
    /// </summary>
    public static class DatosDependencia
    {
        private static readonly Guid IdEsquema = new Guid("8d0f6a55-2a2b-4c57-9a71-3c1b9e4f2d61");
        private const string CampoEje = "Eje";
        private const string CampoReceta = "Receta";

        //El eje se identifica por su UniqueId (no cambia al guardar, copiar el archivo ni editar el eje)
        public static void Guardar(Element elemento, Element eje, Receta receta)
        {
            Entity entidad = new Entity(Esquema());
            entidad.Set(CampoEje, eje.UniqueId);
            entidad.Set(CampoReceta, receta.Escribir());
            elemento.SetEntity(entidad);
        }

        public static (string eje, Receta receta)? Leer(Element elemento)
        {
            Schema? esquema = Schema.Lookup(IdEsquema);
            if (esquema == null) return null;

            Entity entidad = elemento.GetEntity(esquema);
            if (entidad == null || !entidad.IsValid()) return null;
            try
            {
                return (entidad.Get<string>(CampoEje), Receta.Leer(entidad.Get<string>(CampoReceta)));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        //Elementos que dependen del eje, con su receta
        public static List<(Element elemento, Receta receta)> Dependientes(Document doc, Element eje)
        {
            List<(Element, Receta)> lista = new List<(Element, Receta)>();
            Schema? esquema = Schema.Lookup(IdEsquema);
            if (esquema == null) return lista;

            foreach (Element e in new FilteredElementCollector(doc).WherePasses(new ExtensibleStorageFilter(IdEsquema)))
            {
                (string eje, Receta receta)? datos = Leer(e);
                if (datos != null && datos.Value.eje == eje.UniqueId) lista.Add((e, datos.Value.receta));
            }

            return lista;
        }

        private static Schema Esquema()
        {
            Schema? existente = Schema.Lookup(IdEsquema);
            if (existente != null) return existente;

            SchemaBuilder constructor = new SchemaBuilder(IdEsquema);
            constructor.SetSchemaName("ProyectoDoly_Dependencia");
            constructor.SetDocumentation("Elemento de ProyectoDoly atado a un eje: cómo volver a crearlo si el eje cambia.");
            constructor.SetReadAccessLevel(AccessLevel.Public);
            constructor.SetWriteAccessLevel(AccessLevel.Public);
            constructor.AddSimpleField(CampoEje, typeof(string));
            constructor.AddSimpleField(CampoReceta, typeof(string));
            return constructor.Finish();
        }
    }
}
