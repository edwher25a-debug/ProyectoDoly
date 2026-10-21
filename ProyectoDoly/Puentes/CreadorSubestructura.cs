using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Subestructura;

namespace ProyectoDoly.Puentes
{
    public sealed class OpcionesSubestructura
    {
        public EjeGuardado Eje { get; set; } = null!;
        public FamilySymbol Tipo { get; set; } = null!;
        public List<Placement> Placements { get; set; } = new List<Placement>();
        public OpcionesUbicacion Ubicacion { get; set; } = new OpcionesUbicacion();

        //Varios desplazamientos laterales = una fila de ejemplares por placement (diafragmas entre vigas, pilas multiples,
        //como Cross Member Array de SOFiSTiK). Vacio = solo Ubicacion.DesplazamientoLateral
        public List<double> Laterales { get; set; } = new List<double>();

        //Parametro de longitud que recibe la altura desde la cota de fundacion (null = no se toca)
        public string? ParametroAltura { get; set; }
        public double CotaFundacion { get; set; }
    }

    public sealed class ResultadoSubestructura
    {
        public List<ElementId> Ids { get; } = new List<ElementId>();
        public string Resumen { get; set; } = "";
    }

    /// <summary>
    ///     Coloca pilas y estribos en los placements del eje. Como en SOFiSTiK, la familia se inserta por su punto
    ///     adaptativo (familia de Modelo generico adaptativo con un punto); si no es adaptativa, por su origen.
    /// </summary>
    public static class CreadorSubestructura
    {
        //Categorias de familia que pueden ser subestructura
        public static List<FamilySymbol> Tipos(Document doc)
        {
            HashSet<long> categorias = new HashSet<long>
            {
                (long)BuiltInCategory.OST_GenericModel,
                (long)BuiltInCategory.OST_StructuralColumns,
                (long)BuiltInCategory.OST_StructuralFoundation
            };
            foreach (string nombre in new[] { "OST_BridgePiers", "OST_BridgeAbutments", "OST_BridgeFoundations" })
                if (Enum.TryParse(nombre, out BuiltInCategory categoria)) categorias.Add((long)categoria);

            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(t => t.Family != null && !t.Family.IsInPlace && t.Family.FamilyCategory != null)
                .Where(t => categorias.Contains(t.Family.FamilyCategory.Id.Value))
                .OrderBy(t => t.Family.Name).ThenBy(t => t.Name)
                .ToList();
        }

        //Descripcion corta de como se insertara la familia
        public static string Insercion(Family familia)
        {
            if (!AdaptiveComponentFamilyUtils.IsAdaptiveComponentFamily(familia)) return "por su origen (no es adaptativa)";
            int puntos = AdaptiveComponentFamilyUtils.GetNumberOfPlacementPoints(familia);
            return puntos == 1 ? "por su punto adaptativo" : $"no admitida: tiene {puntos} puntos adaptativos (debe tener 1)";
        }

        public static ResultadoSubestructura Crear(Document doc, OpcionesSubestructura opciones)
        {
            Eje eje = opciones.Eje.Eje;
            Transform transformacion = opciones.Eje.Transformacion;
            Family familia = opciones.Tipo.Family;
            bool adaptativa = AdaptiveComponentFamilyUtils.IsAdaptiveComponentFamily(familia);
            if (adaptativa && AdaptiveComponentFamilyUtils.GetNumberOfPlacementPoints(familia) != 1)
                throw new InvalidOperationException(
                    $"La familia {familia.Name} tiene {AdaptiveComponentFamilyUtils.GetNumberOfPlacementPoints(familia)} puntos adaptativos. " +
                    "Para colocarla en un placement debe tener un solo punto adaptativo (el punto de inserción en el eje).");

            ResultadoSubestructura resultado = new ResultadoSubestructura();
            List<string> lineas = new List<string>();
            List<string> avisos = new List<string>();

            using (Transaction transaccion = new Transaction(doc, $"Subestructura {eje.Nombre}"))
            {
                transaccion.Start();
                if (!opciones.Tipo.IsActive) opciones.Tipo.Activate();

                List<double> laterales = opciones.Laterales.Count > 0 ? opciones.Laterales : new List<double> { opciones.Ubicacion.DesplazamientoLateral };
                foreach (Placement placement in opciones.Placements)
                foreach (double lateral in laterales)
                {
                    OpcionesUbicacion ubicacion = new OpcionesUbicacion
                    {
                        DesplazamientoLateral = lateral,
                        DesplazamientoVertical = opciones.Ubicacion.DesplazamientoVertical,
                        GiroAdicional = opciones.Ubicacion.GiroAdicional,
                        SiempreVertical = opciones.Ubicacion.SiempreVertical
                    };
                    Ubicacion3 u = UbicacionSubestructura.Calcular(eje, placement, ubicacion);
                    XYZ origen = transformacion.OfPoint(CreadorEje.APies(u.Origen));
                    XYZ x = transformacion.OfVector(new XYZ(u.EjeX.X, u.EjeX.Y, u.EjeX.Z)).Normalize();
                    XYZ z = transformacion.OfVector(new XYZ(u.EjeZ.X, u.EjeZ.Y, u.EjeZ.Z)).Normalize();

                    FamilyInstance ejemplar = adaptativa
                        ? ColocarAdaptativa(doc, opciones.Tipo, origen, x, z)
                        : ColocarPorOrigen(doc, opciones.Tipo, origen, x);

                    string altura = "";
                    if (!string.IsNullOrEmpty(opciones.ParametroAltura))
                    {
                        double valor = UbicacionSubestructura.Altura(u, opciones.CotaFundacion);
                        Parameter? parametro = ejemplar.LookupParameter(opciones.ParametroAltura);
                        if (parametro == null || parametro.IsReadOnly || parametro.StorageType != StorageType.Double)
                            avisos.Add($"{placement.Id}: el parámetro {opciones.ParametroAltura} no es de ejemplar o no se puede escribir (es de tipo o tiene fórmula).");
                        else
                            parametro.Set(UnitUtils.ConvertToInternalUnits(valor, UnitTypeId.Meters));
                        altura = $"   altura {valor:F2} m";
                    }

                    ejemplar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?
                        .Set($"Subestructura | Eje {eje.Nombre} | {placement.Id} | {Eje.FormatoEstacion(placement.Estacion)}" + (laterales.Count > 1 ? $" | derecha {lateral:0.###} m" : ""));
                    resultado.Ids.Add(ejemplar.Id);
                    lineas.Add($"{placement.Id}{(laterales.Count > 1 ? $" ({lateral:0.##} m)" : "")}   {Eje.FormatoEstacion(placement.Estacion)}   cota {u.Origen.Z:F2}{altura}");
                }

                if (transaccion.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit no pudo colocar la subestructura.");
            }

            resultado.Resumen = $"{resultado.Ids.Count} ejemplares de {familia.Name} : {opciones.Tipo.Name} en el eje \"{eje.Nombre}\", " +
                                $"insertados {(adaptativa ? "por su punto adaptativo" : "por su origen")}.\n\n" +
                                string.Join("\n", lineas.Take(15)) + (lineas.Count > 15 ? $"\n(y {lineas.Count - 15} más)" : "") +
                                (avisos.Count > 0 ? "\n\nAvisos:\n" + string.Join("\n", avisos.Distinct().Take(5)) : "");
            return resultado;
        }

        //Un punto adaptativo: se lleva al punto del placement y se orienta con su sistema de coordenadas
        private static FamilyInstance ColocarAdaptativa(Document doc, FamilySymbol tipo, XYZ origen, XYZ x, XYZ z)
        {
            FamilyInstance ejemplar = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, tipo);
            ElementId idPunto = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(ejemplar).First();
            ReferencePoint punto = (ReferencePoint)doc.GetElement(idPunto);

            Transform sistema = Transform.Identity;
            sistema.Origin = origen;
            sistema.BasisX = x;
            sistema.BasisY = z.CrossProduct(x).Normalize();
            sistema.BasisZ = z;
            try
            {
                punto.SetCoordinateSystem(sistema);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                //Si Revit no deja orientar el punto, se coloca y se gira en planta
                punto.Position = origen;
                Girar(doc, ejemplar, origen, x);
            }

            return ejemplar;
        }

        //Familia no adaptativa: se coloca en el punto y se gira en planta hacia el sentido del eje
        private static FamilyInstance ColocarPorOrigen(Document doc, FamilySymbol tipo, XYZ origen, XYZ x)
        {
            FamilyInstance ejemplar;
            try
            {
                ejemplar = doc.Create.NewFamilyInstance(origen, tipo, StructuralType.NonStructural);
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException || ex is ArgumentException)
            {
                throw new InvalidOperationException(
                    $"Revit no pudo colocar {tipo.Family.Name} en un punto ({ex.Message}). " +
                    "Use una familia de Modelo genérico adaptativo con un punto adaptativo, como las de subestructura de SOFiSTiK.");
            }

            doc.Regenerate();
            Girar(doc, ejemplar, origen, x);
            return ejemplar;
        }

        private static void Girar(Document doc, FamilyInstance ejemplar, XYZ origen, XYZ x)
        {
            double angulo = Math.Atan2(x.Y, x.X);
            if (Math.Abs(angulo) < 1e-9) return;
            ElementTransformUtils.RotateElement(doc, ejemplar.Id, Line.CreateBound(origen, origen + XYZ.BasisZ), angulo);
        }
    }
}
