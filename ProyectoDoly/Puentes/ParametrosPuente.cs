using System.IO;
using Autodesk.Revit.DB;

namespace ProyectoDoly.Puentes
{
    /// <summary>
    ///     Parametros compartidos de puente (eje, estaciones, placement, volumen), con GUID fijos para que sirvan en
    ///     tablas de planificacion y entre proyectos. Se crean en un archivo propio y se enlazan como parametros de ejemplar.
    /// </summary>
    public static class ParametrosPuente
    {
        public sealed class Definicion
        {
            public Definicion(string nombre, Guid guid, ForgeTypeId tipo)
            {
                Nombre = nombre;
                Guid = guid;
                Tipo = tipo;
            }

            public string Nombre { get; }
            public Guid Guid { get; }
            public ForgeTypeId Tipo { get; }
        }

        public static readonly Definicion Eje = new Definicion("DOLY Eje", new Guid("9129937f-8b5d-4df9-a769-4ec508984cc0"), SpecTypeId.String.Text);
        public static readonly Definicion Placement = new Definicion("DOLY Placement", new Guid("8f018002-1236-4d73-8d79-bd2449e47317"), SpecTypeId.String.Text);
        public static readonly Definicion Estaciones = new Definicion("DOLY Estaciones", new Guid("6b4a7df1-23be-45ac-80f4-159bc2a7e7a0"), SpecTypeId.String.Text);
        public static readonly Definicion EstacionInicio = new Definicion("DOLY Estación inicio", new Guid("c1a33982-d6a2-44a8-958a-c4a9a372be47"), SpecTypeId.Length);
        public static readonly Definicion EstacionFin = new Definicion("DOLY Estación fin", new Guid("bc0d8b26-1dcb-4d35-af13-825858eaf418"), SpecTypeId.Length);
        public static readonly Definicion Longitud = new Definicion("DOLY Longitud en eje", new Guid("35d1d205-9a7a-4ff0-a2e2-993256e14891"), SpecTypeId.Length);
        public static readonly Definicion Desfase = new Definicion("DOLY Desfase", new Guid("4a9b3c74-ca5d-4574-9ca8-1a92d7fc89f2"), SpecTypeId.Length);
        public static readonly Definicion Volumen = new Definicion("DOLY Volumen", new Guid("84737cd5-0e7b-47a5-8404-880b58d0cfbb"), SpecTypeId.Volume);

        public static readonly Definicion[] Todos = { Eje, Placement, Estaciones, EstacionInicio, EstacionFin, Longitud, Desfase, Volumen };

        private const string Grupo = "ProyectoDoly Puentes";

        //Archivo de parametros propio, junto a la configuracion del usuario
        public static string Archivo => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProyectoDoly", "ParametrosPuente.txt");

        /// <summary>
        ///     Enlaza los parametros a las categorias dadas (si ya estan enlazados, amplia sus categorias). Dentro de una transaccion.
        /// </summary>
        public static void Enlazar(Document doc, IEnumerable<Category> categorias)
        {
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            CategorySet nuevas = app.Create.NewCategorySet();
            foreach (Category c in categorias.Where(c => c.AllowsBoundParameters)) nuevas.Insert(c);
            if (nuevas.IsEmpty) throw new InvalidOperationException("Las categorías elegidas no admiten parámetros de proyecto.");

            string anterior = app.SharedParametersFilename;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Archivo)!);
                if (!File.Exists(Archivo)) File.WriteAllText(Archivo, "");
                app.SharedParametersFilename = Archivo;
                DefinitionFile archivo = app.OpenSharedParameterFile()
                                         ?? throw new InvalidOperationException($"Revit no pudo abrir el archivo de parámetros {Archivo}.");
                DefinitionGroup grupo = archivo.Groups.get_Item(Grupo) ?? archivo.Groups.Create(Grupo);

                foreach (Definicion d in Todos)
                {
                    ExternalDefinition definicion = grupo.Definitions.OfType<ExternalDefinition>().FirstOrDefault(x => x.GUID == d.Guid)
                                                    ?? (ExternalDefinition)grupo.Definitions.Create(new ExternalDefinitionCreationOptions(d.Nombre, d.Tipo)
                                                    {
                                                        GUID = d.Guid,
                                                        Description = "Creado por ProyectoDoly (Cuantificar)",
                                                        UserModifiable = true
                                                    });

                    if (doc.ParameterBindings.get_Item(definicion) is InstanceBinding existente)
                    {
                        CategorySet union = existente.Categories;
                        foreach (Category c in nuevas) union.Insert(c);
                        doc.ParameterBindings.ReInsert(definicion, app.Create.NewInstanceBinding(union), GroupTypeId.Data);
                    }
                    else if (!doc.ParameterBindings.Insert(definicion, app.Create.NewInstanceBinding(nuevas), GroupTypeId.Data))
                    {
                        //El parametro ya existe en el proyecto con otro enlace (por ejemplo, de tipo)
                        throw new InvalidOperationException($"No se pudo enlazar el parámetro {d.Nombre} como parámetro de ejemplar.");
                    }
                }
            }
            finally
            {
                app.SharedParametersFilename = anterior;
            }
        }
    }
}
