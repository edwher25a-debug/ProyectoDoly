using System.Globalization;
using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Superestructura;

namespace ProyectoDoly.Puentes
{
    public enum UnidadParametro
    {
        Metros,
        Grados,
        Numero
    }

    //Parametro numerico de la familia de seccion que se puede variar a lo largo del tablero
    public sealed class ParametroSeccion
    {
        public string Nombre { get; set; } = "";
        public UnidadParametro Unidad { get; set; }
        public bool DeTipo { get; set; }

        //Valor del tipo elegido, en la unidad del parametro (m, grados o numero)
        public double Valor { get; set; }

        public string Simbolo => Unidad == UnidadParametro.Metros ? "m" : Unidad == UnidadParametro.Grados ? "°" : "";
    }

    /// <summary>
    ///     Abre la familia de seccion una vez y la evalua con distintos valores de sus parametros (por ejemplo, el canto
    ///     en cada estacion del tablero). Los cambios se deshacen despues de leer: la familia del proyecto no cambia.
    /// </summary>
    public sealed class LectorSeccion : IDisposable
    {
        private readonly Document familia;
        private readonly string nombre;
        private readonly Dictionary<string, FamilyParameter> porNombre = new Dictionary<string, FamilyParameter>();
        private readonly Dictionary<string, SeccionTransversal> leidas = new Dictionary<string, SeccionTransversal>();

        public LectorSeccion(Document doc, FamilySymbol tipo)
        {
            nombre = $"{tipo.Family.Name} : {tipo.Name}";
            familia = doc.EditFamily(tipo.Family);

            //La geometria depende del tipo: se activa el tipo elegido en la copia de la familia
            FamilyManager gestor = familia.FamilyManager;
            FamilyType? tipoFamilia = gestor.Types.Cast<FamilyType>().FirstOrDefault(t => t.Name == tipo.Name);
            if (tipoFamilia != null)
            {
                using (Transaction transaccion = new Transaction(familia, "Tipo de sección"))
                {
                    transaccion.Start();
                    gestor.CurrentType = tipoFamilia;
                    transaccion.Commit();
                }
            }

            foreach (FamilyParameter parametro in gestor.GetParameters())
            {
                //Solo los parametros numericos propios de la familia que se pueden escribir
                if (parametro.StorageType != StorageType.Double || parametro.IsReporting || parametro.IsDeterminedByFormula) continue;
                if (parametro.Id.Value < 0) continue;
                if (!(Unidad(parametro.Definition.GetDataType()) is UnidadParametro unidad)) continue;

                porNombre[parametro.Definition.Name] = parametro;
                double interno = gestor.CurrentType?.AsDouble(parametro) ?? 0;
                Parametros.Add(new ParametroSeccion
                {
                    Nombre = parametro.Definition.Name,
                    Unidad = unidad,
                    DeTipo = !parametro.IsInstance,
                    Valor = DesdeInterno(interno, unidad)
                });
            }

            Parametros.Sort((a, b) => string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase));
        }

        public List<ParametroSeccion> Parametros { get; } = new List<ParametroSeccion>();

        /// <summary>
        ///     Seccion con los valores dados (nombre del parametro -> valor en m, grados o numero).
        ///     Los parametros que no se dan quedan con el valor del tipo.
        /// </summary>
        public SeccionTransversal Leer(IReadOnlyDictionary<string, double> valores)
        {
            string clave = string.Join("|", valores.OrderBy(v => v.Key, StringComparer.Ordinal)
                .Select(v => $"{v.Key}={v.Value.ToString("R", CultureInfo.InvariantCulture)}"));
            if (leidas.TryGetValue(clave, out SeccionTransversal? guardada)) return guardada;

            SeccionTransversal seccion;
            using (Transaction transaccion = new Transaction(familia, "Valores de sección"))
            {
                transaccion.Start();
                try
                {
                    foreach (KeyValuePair<string, double> valor in valores)
                    {
                        if (!porNombre.TryGetValue(valor.Key, out FamilyParameter? parametro))
                            throw new InvalidOperationException($"La familia {nombre} no tiene el parámetro {valor.Key}.");

                        ParametroSeccion datos = Parametros.First(p => p.Nombre == valor.Key);
                        try
                        {
                            familia.FamilyManager.Set(parametro, AInterno(valor.Value, datos.Unidad));
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is Autodesk.Revit.Exceptions.ApplicationException)
                        {
                            throw new InvalidOperationException($"La familia no acepta {valor.Key} = {valor.Value:0.###} {datos.Simbolo}: {ex.Message}");
                        }
                    }

                    familia.Regenerate();
                    seccion = SeccionFamilia.LeerGeometria(familia, nombre);
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException ex)
                {
                    throw new InvalidOperationException($"La familia {nombre} no se pudo regenerar con esos valores: {ex.Message}");
                }
                finally
                {
                    //Los valores solo sirven para leer la geometria: la familia queda como estaba
                    transaccion.RollBack();
                }
            }

            leidas[clave] = seccion;
            return seccion;
        }

        public void Dispose() => familia.Close(false);

        private static UnidadParametro? Unidad(ForgeTypeId tipo)
        {
            if (tipo == SpecTypeId.Length) return UnidadParametro.Metros;
            if (tipo == SpecTypeId.Angle) return UnidadParametro.Grados;
            if (tipo == SpecTypeId.Number) return UnidadParametro.Numero;
            return null;
        }

        private static double DesdeInterno(double valor, UnidadParametro unidad) =>
            unidad == UnidadParametro.Metros ? UnitUtils.ConvertFromInternalUnits(valor, UnitTypeId.Meters)
            : unidad == UnidadParametro.Grados ? UnitUtils.ConvertFromInternalUnits(valor, UnitTypeId.Degrees)
            : valor;

        private static double AInterno(double valor, UnidadParametro unidad) =>
            unidad == UnidadParametro.Metros ? UnitUtils.ConvertToInternalUnits(valor, UnitTypeId.Meters)
            : unidad == UnidadParametro.Grados ? UnitUtils.ConvertToInternalUnits(valor, UnitTypeId.Degrees)
            : valor;
    }

    public static class SeccionesPorEstacion
    {
        /// <summary>
        ///     Una seccion por estacion del barrido con los parametros variables evaluados en ella, alineadas entre si
        ///     para que cada vertice siga al mismo vertice a lo largo del tablero
        /// </summary>
        public static List<SeccionTransversal> Calcular(LectorSeccion lector, IReadOnlyList<double> estaciones,
            IReadOnlyList<Variable> variables, bool espejo)
        {
            List<SeccionTransversal> resultado = new List<SeccionTransversal>();
            foreach (double estacion in estaciones)
            {
                SeccionTransversal seccion;
                try
                {
                    seccion = lector.Leer(SeccionVariable.ValoresEn(variables, estacion));
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException($"Estación {Eje.FormatoEstacion(estacion)}: {ex.Message}");
                }

                if (espejo) seccion = seccion.Espejo();
                if (resultado.Count > 0)
                {
                    try
                    {
                        seccion = SeccionVariable.Alinear(seccion, resultado[resultado.Count - 1]);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new InvalidOperationException($"Estación {Eje.FormatoEstacion(estacion)}: {ex.Message}");
                    }
                }

                resultado.Add(seccion);
            }

            return resultado;
        }
    }
}
