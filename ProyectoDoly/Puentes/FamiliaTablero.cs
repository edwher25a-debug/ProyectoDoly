using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Superestructura;

namespace ProyectoDoly.Puentes
{
    /// <summary>
    ///     Tablero como familia adaptativa (como SOFiSTiK): una familia de Modelo generico adaptativo cuyos puntos
    ///     adaptativos son los vertices de la seccion en el inicio y el fin de un tramo, unidos por una solevacion solida
    ///     (los huecos, por solevaciones vacias). Se coloca un ejemplar por tramo, con sus puntos sobre el eje.
    /// </summary>
    public static class FamiliaTablero
    {
        public static ResultadoSuperestructura Crear(Document doc, OpcionesSuperestructura opciones)
        {
            Eje eje = opciones.Eje.Eje;
            Transform transformacion = opciones.Eje.Transformacion;
            List<Punto3[]> estaciones = BarridoTablero.PuntosPorEstacion(eje, opciones.Seccion, opciones.Barrido);
            if (estaciones.Count < 2) throw new InvalidOperationException("El tramo necesita al menos dos secciones.");

            List<XYZ[]> puntos = estaciones
                .Select(e => e.Select(p => transformacion.OfPoint(CreadorEje.APies(p))).ToArray())
                .ToList();

            //01_Familia: se dibuja con el primer tramo llevado al origen (los ejemplares mueven luego sus puntos)
            XYZ origen = puntos[0][0];
            Family familia = CrearFamilia(doc, opciones.Seccion,
                puntos[0].Select(p => p - origen).ToArray(), puntos[1].Select(p => p - origen).ToArray());
            FamilySymbol simbolo = (FamilySymbol)doc.GetElement(familia.GetFamilySymbolIds().First());

            //02_Un ejemplar por tramo
            List<ElementId> ejemplares = new List<ElementId>();
            using (Transaction transaccion = new Transaction(doc, $"Superestructura {eje.Nombre}"))
            {
                transaccion.Start();
                FailureHandlingOptions fallos = transaccion.GetFailureHandlingOptions();
                fallos.SetFailuresPreprocessor(new OcultarAvisos());
                transaccion.SetFailureHandlingOptions(fallos);

                if (!simbolo.IsActive) simbolo.Activate();
                ElementId material = MaterialHormigon(doc);

                for (int k = 0; k < puntos.Count - 1; k++)
                {
                    FamilyInstance ejemplar = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, simbolo);
                    IList<ElementId> ids = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(ejemplar);
                    XYZ[] tramo = puntos[k].Concat(puntos[k + 1]).ToArray();
                    for (int j = 0; j < ids.Count; j++) ((ReferencePoint)doc.GetElement(ids[j])).Position = tramo[j];

                    if (material != ElementId.InvalidElementId) ejemplar.LookupParameter(NombreMaterial)?.Set(material);
                    ejemplar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?
                        .Set($"Tablero {eje.Nombre} | tramo {k + 1} | {Eje.FormatoEstacion(eje.EstacionInicial)}");
                    ejemplares.Add(ejemplar.Id);
                }

                doc.Regenerate();

                //Todos los tramos en un grupo, para seleccionarlos y moverlos juntos
                ElementId seleccion = ejemplares[0];
                try
                {
                    Group grupo = doc.Create.NewGroup(ejemplares);
                    grupo.GroupType.Name = NombreLibre(doc, $"Tablero {eje.Nombre}");
                    seleccion = grupo.Id;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    //Sin grupo: los ejemplares quedan sueltos
                }

                if (transaccion.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit no pudo crear los tramos del tablero.");

                double longitud = opciones.Barrido.EstacionFinal - opciones.Barrido.EstacionInicial;
                return new ResultadoSuperestructura
                {
                    Id = seleccion,
                    Resumen = $"Tablero creado como familia adaptativa \"{familia.Name}\".\n\n" +
                              $"Sección: {opciones.Seccion.Nombre}\n" +
                              $"Desde {Eje.FormatoEstacion(opciones.Barrido.EstacionInicial)} hasta {Eje.FormatoEstacion(opciones.Barrido.EstacionFinal)} ({longitud:F2} m)\n" +
                              $"Tramos: {ejemplares.Count} (un ejemplar por tramo, agrupados)\n" +
                              $"Puntos adaptativos por tramo: {2 * puntos[0].Length}\n" +
                              $"Volumen aproximado: {opciones.Seccion.Area * longitud:F2} m³"
                };
            }
        }

        private const string NombreMaterial = "Material";

        private static Family CrearFamilia(Document doc, SeccionTransversal seccion, XYZ[] inicio, XYZ[] fin)
        {
            Document familia = doc.Application.NewFamilyDocument(Plantilla(doc.Application))
                               ?? throw new InvalidOperationException("Revit no pudo crear la familia desde la plantilla adaptativa.");
            try
            {
                using (Transaction transaccion = new Transaction(familia, "Tablero adaptativo"))
                {
                    transaccion.Start();

                    //Puntos adaptativos: primero toda la seccion inicial y despues la final (mismo orden que los ejemplares)
                    ReferencePoint[] a = inicio.Select(p => PuntoAdaptativo(familia, p)).ToArray();
                    ReferencePoint[] b = fin.Select(p => PuntoAdaptativo(familia, p)).ToArray();

                    FamilyParameter? material = null;
                    try
                    {
                        material = familia.FamilyManager.AddParameter(NombreMaterial, GroupTypeId.Materials, SpecTypeId.Reference.Material, true);
                    }
                    catch (Autodesk.Revit.Exceptions.ApplicationException)
                    {
                        //La plantilla ya trae un parametro con ese nombre
                        material = familia.FamilyManager.get_Parameter(NombreMaterial);
                    }

                    //Una solevacion por contorno: solida para el exterior de cada pieza y vacia para sus huecos
                    int desde = 0;
                    foreach (Pieza pieza in seccion.Piezas)
                    {
                        bool exterior = true;
                        foreach (Contorno contorno in pieza.Contornos)
                        {
                            int n = contorno.Puntos.Count;
                            ReferenceArrayArray perfiles = new ReferenceArrayArray();
                            perfiles.Append(Lazo(familia, a, desde, n));
                            perfiles.Append(Lazo(familia, b, desde, n));
                            Form forma = familia.FamilyCreate.NewLoftForm(exterior, perfiles);

                            Parameter? parametro = forma.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                            if (exterior && material != null && parametro != null)
                                familia.FamilyManager.AssociateElementParameterToFamilyParameter(parametro, material);

                            desde += n;
                            exterior = false;
                        }
                    }

                    transaccion.Commit();
                }

                //Nombre de la familia = archivo; cambia con la seccion para no mezclar topologias distintas
                string carpeta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProyectoDoly");
                System.IO.Directory.CreateDirectory(carpeta);
                string ruta = System.IO.Path.Combine(carpeta, $"PD_Tablero_{Limpio(seccion.Nombre)}_{inicio.Length}p.rfa");
                familia.SaveAs(ruta, new SaveAsOptions { OverwriteExistingFile = true });

                return familia.LoadFamily(doc, new SeccionFamilia.SobrescribirFamilia())
                       ?? throw new InvalidOperationException("Revit no cargó la familia del tablero en el proyecto.");
            }
            finally
            {
                familia.Close(false);
            }
        }

        private static ReferencePoint PuntoAdaptativo(Document familia, XYZ posicion)
        {
            ReferencePoint punto = familia.FamilyCreate.NewReferencePoint(posicion);
            AdaptiveComponentFamilyUtils.MakeAdaptivePoint(familia, punto.Id, AdaptivePointType.PlacementPoint);
            return punto;
        }

        //Contorno cerrado con lineas entre puntos consecutivos
        private static ReferenceArray Lazo(Document familia, ReferencePoint[] puntos, int desde, int cantidad)
        {
            ReferenceArray lazo = new ReferenceArray();
            for (int i = 0; i < cantidad; i++)
            {
                ReferencePointArray extremos = new ReferencePointArray();
                extremos.Append(puntos[desde + i]);
                extremos.Append(puntos[desde + (i + 1) % cantidad]);
                CurveByPoints linea = familia.FamilyCreate.NewCurveByPoints(extremos);
                lazo.Append(linea.GeometryCurve.Reference);
            }

            return lazo;
        }

        //Plantilla "Modelo generico adaptativo" en el idioma instalado (busca en la carpeta de plantillas de Revit)
        private static string Plantilla(Autodesk.Revit.ApplicationServices.Application app)
        {
            string raiz = app.FamilyTemplatePath;
            List<string> carpetas = new List<string> { raiz };
            string? padre = System.IO.Directory.GetParent(raiz.TrimEnd('\\', '/'))?.FullName;
            if (padre != null) carpetas.Add(padre);

            foreach (string carpeta in carpetas.Where(System.IO.Directory.Exists))
            {
                List<string> candidatas = System.IO.Directory.EnumerateFiles(carpeta, "*.rft", System.IO.SearchOption.AllDirectories)
                    .Where(r => Contiene(r, "adapt") && (Contiene(r, "gen") || Contiene(r, "generic")))
                    .ToList();
                string? elegida = candidatas.FirstOrDefault(r => Contiene(r, "métric") || Contiene(r, "metric")) ?? candidatas.FirstOrDefault();
                if (elegida != null) return elegida;
            }

            throw new InvalidOperationException(
                $"No encontré la plantilla \"Modelo genérico adaptativo\" en {raiz}.\n\nRevise la ruta de plantillas en Opciones > Ubicaciones de archivos.");
        }

        private static bool Contiene(string ruta, string texto) =>
            System.IO.Path.GetFileName(ruta).IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Limpio(string nombre)
        {
            string limpio = new string(nombre.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());
            return limpio.Length > 40 ? limpio.Substring(0, 40) : limpio;
        }

        private static string NombreLibre(Document doc, string nombre)
        {
            HashSet<string> usados = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(GroupType)).Select(g => g.Name));
            string libre = nombre;
            for (int i = 2; usados.Contains(libre); i++) libre = $"{nombre} ({i})";
            return libre;
        }

        private static ElementId MaterialHormigon(Document doc)
        {
            string[] nombres = { "hormig", "concret" };
            Material? material = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .FirstOrDefault(m => nombres.Any(n => m.Name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0));
            return material?.Id ?? ElementId.InvalidElementId;
        }

        //Los avisos (no errores) de geometria no detienen la creacion
        private sealed class OcultarAvisos : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor fallos)
            {
                foreach (FailureMessageAccessor fallo in fallos.GetFailureMessages())
                    if (fallo.GetSeverity() == FailureSeverity.Warning) fallos.DeleteWarning(fallo);
                return FailureProcessingResult.Continue;
            }
        }
    }
}
