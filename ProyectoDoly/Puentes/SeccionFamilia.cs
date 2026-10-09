using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Superestructura;

namespace ProyectoDoly.Puentes
{
    /// <summary>
    ///     Lee la seccion transversal de una familia de Revit (por ejemplo las secciones de SOFiSTiK, de Modelo generico).
    ///     Se toma la cara plana de los solidos de la familia en su plano mas delgado; si la familia no tiene solidos,
    ///     las lineas cerradas (familias de perfil o lineas de modelo). El origen de la familia es el punto del eje.
    /// </summary>
    public static class SeccionFamilia
    {
        //Tipos de familia que pueden ser seccion: Modelo generico y Perfiles
        public static List<FamilySymbol> Tipos(Document doc)
        {
            long[] categorias = { (long)BuiltInCategory.OST_GenericModel, (long)BuiltInCategory.OST_ProfileFamilies };

            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(t => t.Family != null && t.Family.IsEditable && !t.Family.IsInPlace)
                .Where(t => t.Family.FamilyCategory != null && categorias.Contains(Valor(t.Family.FamilyCategory.Id)))
                .OrderBy(t => t.Family.Name).ThenBy(t => t.Name)
                .ToList();
        }

        public static Family Cargar(Document doc, string ruta)
        {
            using (Transaction transaccion = new Transaction(doc, "Cargar familia de sección"))
            {
                transaccion.Start();
                doc.LoadFamily(ruta, new SobrescribirFamilia(), out Family? familia);
                transaccion.Commit();

                //Si ya estaba cargada y no cambio, Revit no devuelve la familia: se busca por nombre
                string nombre = System.IO.Path.GetFileNameWithoutExtension(ruta);
                return familia ?? new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().FirstOrDefault(f => f.Name == nombre)
                       ?? throw new InvalidOperationException($"Revit no cargó la familia {nombre}.");
            }
        }

        public static SeccionTransversal Leer(Document doc, FamilySymbol tipo)
        {
            Document familia = doc.EditFamily(tipo.Family);
            try
            {
                //La geometria depende del tipo: se activa el tipo elegido en la copia de la familia
                FamilyType? tipoFamilia = familia.FamilyManager.Types.Cast<FamilyType>().FirstOrDefault(t => t.Name == tipo.Name);
                if (tipoFamilia != null)
                {
                    using (Transaction transaccion = new Transaction(familia, "Tipo de sección"))
                    {
                        transaccion.Start();
                        familia.FamilyManager.CurrentType = tipoFamilia;
                        transaccion.Commit();
                    }
                }

                string nombre = $"{tipo.Family.Name} : {tipo.Name}";
                List<Solid> solidos = Solidos(familia);
                if (solidos.Count > 0) return DesdeSolidos(nombre, solidos);

                List<Curve> curvas = Curvas(familia);
                if (curvas.Count > 0) return DesdeCurvas(nombre, curvas);

                throw new InvalidOperationException($"La familia {tipo.Family.Name} no tiene sólidos ni líneas cerradas para usar como sección.");
            }
            finally
            {
                familia.Close(false);
            }
        }

        //01_Solidos: cara del lado menor en el eje donde la familia es mas delgada (una extrusion fina de la seccion)
        private static SeccionTransversal DesdeSolidos(string nombre, List<Solid> solidos)
        {
            List<XYZ> vertices = solidos.SelectMany(s => s.Edges.Cast<Edge>()).SelectMany(e => e.Tessellate()).ToList();
            XYZ normal = EjeMasDelgado(vertices);

            List<Contorno> contornos = new List<Contorno>();
            foreach (Solid solido in solidos)
            {
                foreach (PlanarFace cara in solido.Faces.OfType<PlanarFace>())
                {
                    if (cara.FaceNormal.DotProduct(normal) > -0.999) continue;

                    foreach (CurveLoop lazo in cara.GetEdgesAsCurveLoops())
                        contornos.Add(new Contorno(Puntos(lazo).Select(p => En2D(p, normal))));
                }
            }

            if (contornos.Count == 0)
                throw new InvalidOperationException("No encontré una cara plana de sección en los sólidos de la familia.");

            return SeccionTransversal.DesdeContornos(nombre, contornos);
        }

        //02_Lineas: se encadenan por sus extremos en contornos cerrados
        private static SeccionTransversal DesdeCurvas(string nombre, List<Curve> curvas)
        {
            XYZ normal = EjeMasDelgado(curvas.SelectMany(c => c.Tessellate()).ToList());
            double tolerancia = 1e-4;

            List<List<XYZ>> tramos = curvas.Select(c => c.Tessellate().ToList()).ToList();
            List<Contorno> contornos = new List<Contorno>();
            while (tramos.Count > 0)
            {
                List<XYZ> cadena = new List<XYZ>(tramos[0]);
                tramos.RemoveAt(0);

                bool encontrado = true;
                while (encontrado && cadena[0].DistanceTo(cadena[cadena.Count - 1]) > tolerancia)
                {
                    encontrado = false;
                    XYZ fin = cadena[cadena.Count - 1];
                    for (int i = 0; i < tramos.Count; i++)
                    {
                        List<XYZ> tramo = tramos[i];
                        if (tramo[0].DistanceTo(fin) <= tolerancia) cadena.AddRange(tramo.Skip(1));
                        else if (tramo[tramo.Count - 1].DistanceTo(fin) <= tolerancia) cadena.AddRange(Enumerable.Reverse(tramo).Skip(1));
                        else continue;

                        tramos.RemoveAt(i);
                        encontrado = true;
                        break;
                    }
                }

                //Las lineas sueltas que no cierran no forman seccion
                if (cadena.Count >= 4 && cadena[0].DistanceTo(cadena[cadena.Count - 1]) <= tolerancia)
                    contornos.Add(new Contorno(cadena.Select(p => En2D(p, normal))));
            }

            if (contornos.Count == 0)
                throw new InvalidOperationException("Las líneas de la familia no forman ningún contorno cerrado.");

            return SeccionTransversal.DesdeContornos(nombre, contornos);
        }

        private static List<Solid> Solidos(Document familia)
        {
            Options opciones = new Options { DetailLevel = ViewDetailLevel.Fine };
            List<Solid> solidos = new List<Solid>();

            foreach (Element elemento in new FilteredElementCollector(familia).WhereElementIsNotElementType())
            {
                //Los vacios no son parte de la seccion (su efecto ya esta en los solidos que cortan)
                if (elemento is GenericForm forma && !forma.IsSolid) continue;
                if (elemento is View) continue;

                GeometryElement? geometria;
                try
                {
                    geometria = elemento.get_Geometry(opciones);
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    continue;
                }

                if (geometria != null) AgregarSolidos(geometria, solidos);
            }

            return solidos;
        }

        private static void AgregarSolidos(GeometryElement geometria, List<Solid> solidos)
        {
            foreach (GeometryObject objeto in geometria)
            {
                if (objeto is Solid solido && solido.Volume > 1e-9) solidos.Add(solido);
                else if (objeto is GeometryInstance instancia) AgregarSolidos(instancia.GetInstanceGeometry(), solidos);
            }
        }

        private static List<Curve> Curvas(Document familia) =>
            new FilteredElementCollector(familia)
                .OfClass(typeof(CurveElement))
                .Cast<CurveElement>()
                .Where(c => !(c is ModelCurve modelo && modelo.IsReferenceLine))
                .Select(c => c.GeometryCurve)
                .Where(c => c != null && c.IsBound)
                .ToList();

        //Normal del plano de la seccion: el eje X, Y o Z en el que la geometria ocupa menos
        private static XYZ EjeMasDelgado(List<XYZ> puntos)
        {
            double ancho = puntos.Max(p => p.X) - puntos.Min(p => p.X);
            double fondo = puntos.Max(p => p.Y) - puntos.Min(p => p.Y);
            double alto = puntos.Max(p => p.Z) - puntos.Min(p => p.Z);

            if (fondo <= ancho && fondo <= alto) return XYZ.BasisY;
            if (ancho <= fondo && ancho <= alto) return XYZ.BasisX;
            return XYZ.BasisZ;
        }

        //Punto de la familia (pies) a la seccion (metros): derecha y arriba vistos de frente
        private static Punto2 En2D(XYZ p, XYZ normal)
        {
            double derecha, arriba;
            if (normal.IsAlmostEqualTo(XYZ.BasisY))
            {
                derecha = p.X;
                arriba = p.Z;
            }
            else if (normal.IsAlmostEqualTo(XYZ.BasisX))
            {
                derecha = p.Y;
                arriba = p.Z;
            }
            else
            {
                derecha = p.X;
                arriba = p.Y;
            }

            return new Punto2(UnitUtils.ConvertFromInternalUnits(derecha, UnitTypeId.Meters),
                UnitUtils.ConvertFromInternalUnits(arriba, UnitTypeId.Meters));
        }

        private static IEnumerable<XYZ> Puntos(CurveLoop lazo)
        {
            foreach (Curve curva in lazo)
            {
                IList<XYZ> puntos = curva.Tessellate();
                for (int i = 0; i < puntos.Count - 1; i++) yield return puntos[i];
            }
        }

        private static long Valor(ElementId id) => id.Value;

        internal sealed class SobrescribirFamilia : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = false;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = false;
                return true;
            }
        }
    }
}
