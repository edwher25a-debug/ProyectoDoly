using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using Point = System.Windows.Point;

namespace ProyectoDoly.ViewModels
{
    //Fila de la tabla de placements (los numeros se escriben como texto para aceptar 1+234.5 y coma decimal)
    public sealed class FilaPlacement : ObservableObject
    {
        private readonly Action cambio;

        public FilaPlacement(Placement placement, Action cambio)
        {
            this.cambio = cambio;
            id = placement.Id;
            estacion = Eje.FormatoEstacion(placement.Estacion);
            rotacion = placement.Rotacion.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private string id;
        public string Id
        {
            get => id;
            set
            {
                if (SetProperty(ref id, value)) cambio();
            }
        }

        private string estacion;
        public string Estacion
        {
            get => estacion;
            set
            {
                if (SetProperty(ref estacion, value)) cambio();
            }
        }

        private string rotacion;
        public string Rotacion
        {
            get => rotacion;
            set
            {
                if (SetProperty(ref rotacion, value)) cambio();
            }
        }
    }

    //Marca de un placement en la vista en planta
    public sealed class MarcaPlanta
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public string Texto { get; set; } = "";
        public double TextoX { get; set; }
        public double TextoY { get; set; }
    }

    public sealed class vm_Placements : ObservableObject
    {
        public const double AnchoVista = 360;
        public const double AltoVista = 260;

        public vm_Placements(List<EjeGuardado> ejes, ElementId? preseleccionado)
        {
            AgregarBT = new RelayCommand(Agregar, () => EjeSelecc != null);
            QuitarBT = new RelayCommand(Quitar, () => FilaSelecc != null);
            DistribuirBT = new RelayCommand(Distribuir, () => EjeSelecc != null);
            GuardarBT = new RelayCommand(Guardar, () => PuedeGuardar);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            foreach (EjeGuardado eje in ejes) Ejes.Add(eje);
            EjeSelecc = Ejes.FirstOrDefault(e => e.Id == preseleccionado) ?? Ejes.FirstOrDefault();
        }

        //Resultado para el comando (null si se cancela)
        public List<Placement>? Resultado { get; private set; }
        public Action<bool>? Cerrar { get; set; }

        public ObservableCollection<EjeGuardado> Ejes { get; } = new ObservableCollection<EjeGuardado>();

        private EjeGuardado? ejeSelecc;
        public EjeGuardado? EjeSelecc
        {
            get => ejeSelecc;
            set
            {
                if (!SetProperty(ref ejeSelecc, value)) return;

                Filas.Clear();
                if (value != null)
                {
                    foreach (Placement p in value.Eje.Placements.OrderBy(p => p.Estacion)) Filas.Add(new FilaPlacement(p, Actualizar));
                    distribuirDesde = Eje.FormatoEstacion(value.Eje.EstacionInicial);
                    distribuirHasta = Eje.FormatoEstacion(value.Eje.EstacionFinal);
                    OnPropertyChanged(nameof(DistribuirDesde));
                    OnPropertyChanged(nameof(DistribuirHasta));
                }

                AgregarBT.NotifyCanExecuteChanged();
                DistribuirBT.NotifyCanExecuteChanged();
                Actualizar();
            }
        }

        public ObservableCollection<FilaPlacement> Filas { get; } = new ObservableCollection<FilaPlacement>();

        private FilaPlacement? filaSelecc;
        public FilaPlacement? FilaSelecc
        {
            get => filaSelecc;
            set
            {
                if (SetProperty(ref filaSelecc, value)) QuitarBT.NotifyCanExecuteChanged();
            }
        }

        //Distribucion automatica: un placement cada cierta distancia (por ejemplo, ejes de pilas cada 30 m)
        private string distribuirDesde = "";
        public string DistribuirDesde
        {
            get => distribuirDesde;
            set => SetProperty(ref distribuirDesde, value);
        }

        private string distribuirHasta = "";
        public string DistribuirHasta
        {
            get => distribuirHasta;
            set => SetProperty(ref distribuirHasta, value);
        }

        private string distribuirCada = "30";
        public string DistribuirCada
        {
            get => distribuirCada;
            set => SetProperty(ref distribuirCada, value);
        }

        private string prefijo = "P";
        public string Prefijo
        {
            get => prefijo;
            set => SetProperty(ref prefijo, value);
        }

        //Vista en planta
        public PointCollection Planta { get; private set; } = new PointCollection();
        public ObservableCollection<MarcaPlanta> Marcas { get; } = new ObservableCollection<MarcaPlanta>();

        private string resumen = "";
        public string Resumen
        {
            get => resumen;
            private set => SetProperty(ref resumen, value);
        }

        private string error = "";
        public string Error
        {
            get => error;
            private set => SetProperty(ref error, value);
        }

        private List<Placement>? validos;
        public bool PuedeGuardar => validos != null;

        public RelayCommand AgregarBT { get; }
        public RelayCommand QuitarBT { get; }
        public RelayCommand DistribuirBT { get; }
        public RelayCommand GuardarBT { get; }
        public RelayCommand CancelarBT { get; }

        //Nueva fila a mitad del ultimo vano libre (o al inicio si no hay ninguna)
        private void Agregar()
        {
            if (EjeSelecc == null) return;
            Eje eje = EjeSelecc.Eje;
            List<Placement> actuales = Leer(out _) ?? new List<Placement>();
            double estacion = actuales.Count == 0 ? eje.EstacionInicial : Math.Min(actuales.Max(p => p.Estacion) + 10, eje.EstacionFinal);
            FilaPlacement fila = new FilaPlacement(new Placement { Id = PlacementsEje.SiguienteId(actuales, Prefijo), Estacion = estacion }, Actualizar);
            Filas.Add(fila);
            FilaSelecc = fila;
            Actualizar();
        }

        private void Quitar()
        {
            if (FilaSelecc == null) return;
            Filas.Remove(FilaSelecc);
            FilaSelecc = null;
            Actualizar();
        }

        private void Distribuir()
        {
            if (EjeSelecc == null) return;
            if (!LeerNumero(DistribuirDesde, out double desde) || !LeerNumero(DistribuirHasta, out double hasta) || !LeerNumero(DistribuirCada, out double cada))
            {
                Error = "Escriba desde, hasta y cada cuántos metros (por ejemplo 0+000, 0+120 y 30).";
                return;
            }

            try
            {
                List<Placement> actuales = Leer(out _) ?? new List<Placement>();
                string numeroInicial = PlacementsEje.SiguienteId(actuales, Prefijo).Substring(Prefijo.Length);
                int primero = int.TryParse(numeroInicial, out int n) ? n : 1;
                foreach (Placement p in PlacementsEje.Distribuir(desde, hasta, cada, Prefijo, primero))
                {
                    //No se duplica un placement que ya existe en la misma estacion
                    if (actuales.Any(a => Math.Abs(a.Estacion - p.Estacion) < 1e-3)) continue;
                    Filas.Add(new FilaPlacement(p, Actualizar));
                }

                Actualizar();
            }
            catch (ArgumentException ex)
            {
                Error = ex.Message;
            }
        }

        private void Guardar()
        {
            if (validos == null) return;
            Resultado = validos;
            Cerrar?.Invoke(true);
        }

        //Lee la tabla; null y un mensaje si algun numero no se entiende
        private List<Placement>? Leer(out string? problema)
        {
            problema = null;
            List<Placement> lista = new List<Placement>();
            foreach (FilaPlacement fila in Filas)
            {
                if (!LeerNumero(fila.Estacion, out double estacion))
                {
                    problema = $"{(fila.Id.Length > 0 ? fila.Id : "Una fila")}: la estación \"{fila.Estacion}\" no es un número (por ejemplo 0+120 o 120).";
                    return null;
                }

                double rotacion = 0;
                if (!string.IsNullOrWhiteSpace(fila.Rotacion) && !LeerNumero(fila.Rotacion, out rotacion))
                {
                    problema = $"{fila.Id}: el giro \"{fila.Rotacion}\" no es un número de grados.";
                    return null;
                }

                lista.Add(new Placement { Id = fila.Id.Trim(), Estacion = estacion, Rotacion = rotacion });
            }

            return lista;
        }

        private void Actualizar()
        {
            Error = "";
            validos = null;
            Marcas.Clear();
            Planta = new PointCollection();

            if (EjeSelecc != null)
            {
                Eje eje = EjeSelecc.Eje;
                List<Placement>? lista = Leer(out string? problema);
                if (lista == null)
                {
                    Error = problema ?? "";
                }
                else
                {
                    List<string> errores = PlacementsEje.Validar(eje, lista);
                    if (errores.Count > 0) Error = string.Join("\n", errores.Take(3)) + (errores.Count > 3 ? $"\n(y {errores.Count - 3} más)" : "");
                    else validos = lista.OrderBy(p => p.Estacion).ToList();
                }

                Dibujar(eje, lista ?? new List<Placement>());
                List<Placement> orden = (lista ?? new List<Placement>()).OrderBy(p => p.Estacion).ToList();
                Resumen = $"{orden.Count} placements" +
                          (orden.Count > 1
                              ? $"  ·  vanos: {string.Join(", ", orden.Zip(orden.Skip(1), (a, b) => (b.Estacion - a.Estacion).ToString("0.##", CultureInfo.InvariantCulture)))} m"
                              : "");
            }
            else
            {
                Error = "Importe primero un eje con el botón Importar eje.";
                Resumen = "";
            }

            OnPropertyChanged(nameof(Planta));
            GuardarBT.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeGuardar));
        }

        //Planta del eje con la marca de cada placement en su plano (con su giro)
        private void Dibujar(Eje eje, List<Placement> placements)
        {
            List<PuntoEje> muestras = eje.Muestrear(Math.Max(eje.Horizontal.Longitud / 300, 0.5));
            const double margen = 24;
            double minX = muestras.Min(m => m.Posicion.X), maxX = muestras.Max(m => m.Posicion.X);
            double minY = muestras.Min(m => m.Posicion.Y), maxY = muestras.Max(m => m.Posicion.Y);
            double escala = Math.Min((AnchoVista - 2 * margen) / Math.Max(maxX - minX, 1e-6), (AltoVista - 2 * margen) / Math.Max(maxY - minY, 1e-6));
            double desplX = (AnchoVista - (maxX - minX) * escala) / 2;
            double desplY = (AltoVista - (maxY - minY) * escala) / 2;
            Point Vista(double x, double y) => new Point(desplX + (x - minX) * escala, AltoVista - desplY - (y - minY) * escala);

            PointCollection puntos = new PointCollection(muestras.Select(m => Vista(m.Posicion.X, m.Posicion.Y)));
            puntos.Freeze();
            Planta = puntos;

            const double largo = 18;
            foreach (Placement p in placements.Where(p => p.Estacion >= eje.EstacionInicial - 1e-6 && p.Estacion <= eje.EstacionFinal + 1e-6))
            {
                Punto3 centro = eje.Evaluar(p.Estacion).Posicion;
                Punto2 d = PlacementsEje.DireccionPlano(eje, p);
                Point c = Vista(centro.X, centro.Y);
                double dx = d.X * largo / 2, dy = -d.Y * largo / 2;
                Marcas.Add(new MarcaPlanta
                {
                    X1 = c.X + dx, Y1 = c.Y + dy, X2 = c.X - dx, Y2 = c.Y - dy,
                    Texto = p.Id, TextoX = c.X + dx * 1.4 - 6, TextoY = c.Y + dy * 1.4 - 8
                });
            }
        }

        //Acepta coma o punto decimal y el formato vial 1+234.5
        private static bool LeerNumero(string texto, out double valor) => Puentes.Superestructura.SeccionVariable.LeerNumero(texto, out valor);
    }
}
