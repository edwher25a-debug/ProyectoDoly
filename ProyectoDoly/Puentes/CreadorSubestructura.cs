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

        //Familias por linea (estribos de Armazon estructural): largo de la linea, centrada en el eje y en el plano del placement
        public double LongitudTransversal { get; set; } = 10;
    }

    public sealed class ResultadoSubestructura
    {
        public List<ElementId> Ids { get; } = new List<ElementId>();
        public string Resumen { get; set; } = "";
    }

    //Como se coloca cada familia segun su tipo de colocacion en Revit
    public enum ModoSubestructura
    {
        NoAdmitida,
        Adaptativa,     //Un punto adaptativo
        PorNivel,       //Basada en un nivel o en plano de trabajo: cimentaciones (con sus pilas, dados y pilotes anidados), modelo generico
        EntreNiveles,   //Pilar estructural: de la cota de fundacion a la cota del placement
        PorLinea        //Armazon estructural y familias basadas en linea: estribos, cabezales, vigas cabezal
    }

    /// <summary>
    ///     Coloca pilas y estribos en los placements del eje. Como en SOFiSTiK, una familia adaptativa se inserta por su punto
    ///     adaptativo; las demas, con el metodo de Revit que pide su tipo de colocacion (nivel, dos niveles o linea).
    /// </summary>
    public static class CreadorSubestructura
    {
        //Categorias que se proponen primero en la lista
        private static readonly string[] CategoriasPuente =
        {
            "OST_StructuralFoundation", "OST_StructuralColumns", "OST_StructuralFraming", "OST_GenericModel",
            "OST_BridgePiers", "OST_BridgeAbutments", "OST_BridgeFoundations", "OST_BridgeFraming", "OST_BridgeBearings"
        };

        //Cualquier familia cargable de modelo que se pueda colocar por punto, por niveles o por linea
        public static List<FamilySymbol> Tipos(Document doc)
        {
            HashSet<long> preferidas = new HashSet<long>();
            foreach (string nombre in CategoriasPuente)
                if (Enum.TryParse(nombre, out BuiltInCategory categoria)) preferidas.Add((long)categoria);

            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(t => t.Family != null && !t.Family.IsInPlace && t.Family.FamilyCategory != null)
                .Where(t => t.Family.FamilyCategory.CategoryType == CategoryType.Model)
                .Where(t => t.Family.FamilyCategory.Id.Value != (long)BuiltInCategory.OST_ProfileFamilies)
                .Where(t => Modo(t.Family) != ModoSubestructura.NoAdmitida)
                .OrderBy(t => preferidas.Contains(t.Family.FamilyCategory.Id.Value) ? 0 : 1)
                .ThenBy(t => t.Family.Name).ThenBy(t => t.Name)
                .ToList();
        }

        public static ModoSubestructura Modo(Family familia)
        {
            if (AdaptiveComponentFamilyUtils.IsAdaptiveComponentFamily(familia))
                return AdaptiveComponentFamilyUtils.GetNumberOfPlacementPoints(familia) == 1 ? ModoSubestructura.Adaptativa : ModoSubestructura.NoAdmitida;

            switch (familia.FamilyPlacementType)
            {
                case FamilyPlacementType.OneLevelBased:
                case FamilyPlacementType.WorkPlaneBased:
                    return ModoSubestructura.PorNivel;
                case FamilyPlacementType.TwoLevelsBased:
                    return ModoSubestructura.EntreNiveles;
                case FamilyPlacementType.CurveBased:
                case FamilyPlacementType.CurveBasedDetail:
                case FamilyPlacementType.CurveDrivenStructural:
                    return familia.FamilyPlacementType == FamilyPlacementType.CurveBasedDetail ? ModoSubestructura.NoAdmitida : ModoSubestructura.PorLinea;
                default:
                    return ModoSubestructura.NoAdmitida;
            }
        }

        //Descripcion corta de como se insertara la familia
        public static string Insercion(Family familia)
        {
            string categoria = familia.FamilyCategory?.Name ?? "";
            switch (Modo(familia))
            {
                case ModoSubestructura.Adaptativa:
                    return $"por su punto adaptativo ({categoria})";
                case ModoSubestructura.PorNivel:
                    return $"por su origen, en el nivel más cercano por debajo ({categoria}). Las familias anidadas compartidas vienen con ella";
                case ModoSubestructura.EntreNiveles:
                    return $"como pilar, desde la cota de fundación hasta el placement ({categoria})";
                case ModoSubestructura.PorLinea:
                    return $"por una línea en el plano del placement, centrada en el eje ({categoria})";
                default:
                    return AdaptiveComponentFamilyUtils.IsAdaptiveComponentFamily(familia)
                        ? $"no admitida: tiene {AdaptiveComponentFamilyUtils.GetNumberOfPlacementPoints(familia)} puntos adaptativos (debe tener 1)"
                        : $"no admitida: su tipo de colocación ({familia.FamilyPlacementType}) necesita una cara o una vista";
            }
        }

        public static ResultadoSubestructura Crear(Document doc, OpcionesSubestructura opciones)
        {
            Eje eje = opciones.Eje.Eje;
            Transform transformacion = opciones.Eje.Transformacion;
            Family familia = opciones.Tipo.Family;
            ModoSubestructura modo = Modo(familia);
            if (modo == ModoSubestructura.NoAdmitida)
                throw new InvalidOperationException($"{familia.Name} se colocaría {Insercion(familia)}.");

            StructuralType estructural = TipoEstructural(familia, modo);
            List<Level> niveles = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(n => n.Elevation).ToList();
            if (modo != ModoSubestructura.Adaptativa && niveles.Count == 0)
                throw new InvalidOperationException("El modelo no tiene niveles. Cree al menos uno para colocar esta familia.");
            double fundacion = transformacion.OfPoint(CreadorEje.APies(new Punto3(0, 0, opciones.CotaFundacion))).Z;

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

                    FamilyInstance ejemplar;
                    switch (modo)
                    {
                        case ModoSubestructura.Adaptativa:
                            ejemplar = ColocarAdaptativa(doc, opciones.Tipo, origen, x, z);
                            break;
                        case ModoSubestructura.EntreNiveles:
                            ejemplar = ColocarPilar(doc, opciones.Tipo, origen, x, fundacion, NivelBajo(niveles, fundacion), estructural);
                            break;
                        case ModoSubestructura.PorLinea:
                            Punto2 plano = PlacementsEje.DireccionPlano(eje, new Placement { Estacion = placement.Estacion, Rotacion = placement.Rotacion + opciones.Ubicacion.GiroAdicional });
                            XYZ transversal = transformacion.OfVector(new XYZ(plano.X, plano.Y, 0)).Normalize();
                            ejemplar = ColocarPorLinea(doc, opciones.Tipo, origen, transversal, opciones.LongitudTransversal, NivelBajo(niveles, origen.Z), estructural);
                            break;
                        default:
                            ejemplar = ColocarPorNivel(doc, opciones.Tipo, origen, x, NivelBajo(niveles, origen.Z), estructural);
                            break;
                    }

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

                int anidadas = resultado.Ids.Sum(id => Anidadas(doc, id).Count);
                if (anidadas > 0) lineas.Insert(0, $"Con {anidadas} familias anidadas compartidas (pilas, dados, pilotes...).");

                if (transaccion.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit no pudo colocar la subestructura.");
            }

            resultado.Resumen = $"{resultado.Ids.Count} ejemplares de {familia.Name} : {opciones.Tipo.Name} en el eje \"{eje.Nombre}\", " +
                                $"insertados {Insercion(familia)}.\n\n" +
                                string.Join("\n", lineas.Take(16)) + (lineas.Count > 16 ? $"\n(y {lineas.Count - 16} más)" : "") +
                                (avisos.Count > 0 ? "\n\nAvisos:\n" + string.Join("\n", avisos.Distinct().Take(5)) : "");
            return resultado;
        }

        /// <summary>
        ///     Familias anidadas compartidas de un ejemplar, a cualquier profundidad (las pilas, dados y pilotes dentro de una cimentacion)
        /// </summary>
        public static List<FamilyInstance> Anidadas(Document doc, ElementId id)
        {
            List<FamilyInstance> todas = new List<FamilyInstance>();
            if (doc.GetElement(id) is not FamilyInstance ejemplar) return todas;
            foreach (ElementId hijo in ejemplar.GetSubComponentIds())
                if (doc.GetElement(hijo) is FamilyInstance anidada)
                {
                    todas.Add(anidada);
                    todas.AddRange(Anidadas(doc, hijo));
                }

            return todas;
        }

        private static StructuralType TipoEstructural(Family familia, ModoSubestructura modo)
        {
            long categoria = familia.FamilyCategory.Id.Value;
            if (categoria == (long)BuiltInCategory.OST_StructuralFoundation) return StructuralType.Footing;
            if (categoria == (long)BuiltInCategory.OST_StructuralColumns) return StructuralType.Column;
            if (categoria == (long)BuiltInCategory.OST_StructuralFraming) return modo == ModoSubestructura.PorLinea ? StructuralType.Beam : StructuralType.NonStructural;
            return StructuralType.NonStructural;
        }

        //Nivel mas alto que no pasa de la cota (o el mas bajo si todos estan por encima)
        private static Level NivelBajo(List<Level> niveles, double cota) =>
            niveles.LastOrDefault(n => n.Elevation <= cota + 1e-6) ?? niveles[0];

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

        //Basada en nivel o plano de trabajo: se crea en el nivel, se lleva a la cota del placement y se gira en planta
        private static FamilyInstance ColocarPorNivel(Document doc, FamilySymbol tipo, XYZ origen, XYZ x, Level nivel, StructuralType estructural)
        {
            FamilyInstance ejemplar = Intentar(tipo, () => doc.Create.NewFamilyInstance(new XYZ(origen.X, origen.Y, nivel.Elevation), tipo, nivel, estructural));
            doc.Regenerate();
            LlevarA(doc, ejemplar, origen);
            Girar(doc, ejemplar, origen, x);
            return ejemplar;
        }

        //Pilar: base en la cota de fundacion y cabeza en el placement, ambas como desfase del mismo nivel
        private static FamilyInstance ColocarPilar(Document doc, FamilySymbol tipo, XYZ origen, XYZ x, double fundacion, Level nivel, StructuralType estructural)
        {
            if (origen.Z - fundacion < 1e-3)
                throw new InvalidOperationException($"La cota de fundación debe quedar por debajo del placement para colocar el pilar {tipo.Family.Name}.");

            XYZ pie = new XYZ(origen.X, origen.Y, fundacion);
            FamilyInstance ejemplar = Intentar(tipo, () => doc.Create.NewFamilyInstance(pie, tipo, nivel, estructural));
            ejemplar.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(nivel.Id);
            ejemplar.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.Set(fundacion - nivel.Elevation);
            ejemplar.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(origen.Z - nivel.Elevation);
            doc.Regenerate();
            Girar(doc, ejemplar, pie, x);
            return ejemplar;
        }

        //Basada en linea (estribo de Armazon estructural): linea en el plano del placement, centrada en el punto de insercion
        private static FamilyInstance ColocarPorLinea(Document doc, FamilySymbol tipo, XYZ origen, XYZ transversal, double longitud, Level nivel, StructuralType estructural)
        {
            XYZ mitad = transversal * (UnitUtils.ConvertToInternalUnits(longitud, UnitTypeId.Meters) / 2);
            Line linea = Line.CreateBound(origen + mitad, origen - mitad);
            FamilyInstance ejemplar = Intentar(tipo, () => doc.Create.NewFamilyInstance(linea, tipo, nivel, estructural));
            if (estructural == StructuralType.Beam)
            {
                //Sin uniones automaticas con pilas y otros estribos: la linea queda donde se dibujo
                StructuralFramingUtils.DisallowJoinAtEnd(ejemplar, 0);
                StructuralFramingUtils.DisallowJoinAtEnd(ejemplar, 1);
            }

            return ejemplar;
        }

        private static FamilyInstance Intentar(FamilySymbol tipo, Func<FamilyInstance> crear)
        {
            try
            {
                return crear();
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException || ex is ArgumentException)
            {
                throw new InvalidOperationException($"Revit no pudo colocar {tipo.Family.Name} ({tipo.Family.FamilyCategory?.Name}): {ex.Message}");
            }
        }

        //Mueve el ejemplar para que su punto de insercion quede en el destino (la cota la fija Revit como desfase del nivel)
        private static void LlevarA(Document doc, FamilyInstance ejemplar, XYZ destino)
        {
            if (ejemplar.Location is not LocationPoint punto) return;
            XYZ delta = destino - punto.Point;
            if (delta.GetLength() < 1e-6) return;
            ElementTransformUtils.MoveElement(doc, ejemplar.Id, delta);
            doc.Regenerate();
        }

        private static void Girar(Document doc, FamilyInstance ejemplar, XYZ origen, XYZ x)
        {
            double angulo = Math.Atan2(x.Y, x.X);
            if (Math.Abs(angulo) < 1e-9) return;
            ElementTransformUtils.RotateElement(doc, ejemplar.Id, Line.CreateBound(origen, origen + XYZ.BasisZ), angulo);
        }
    }
}
