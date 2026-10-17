using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Planos;

namespace ProyectoDoly.ViewModels
{
    public sealed class vm_Secciones : ObservableObject
    {
        public vm_Secciones(Document doc, List<EjeGuardado> ejes, ElementId? preseleccionado)
        {
            TodosBT = new RelayCommand(() => Marcar(true));
            NingunoBT = new RelayCommand(() => Marcar(false));
            CrearBT = new RelayCommand(Crear, () => PuedeCrear);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            foreach (ViewFamilyType tipo in CreadorSecciones.TiposVista(doc)) TiposVista.Add(tipo);
            tipoVista = TiposVista.FirstOrDefault();
            foreach (EjeGuardado eje in ejes) Ejes.Add(eje);
            EjeSelecc = Ejes.FirstOrDefault(e => e.Id == preseleccionado) ?? Ejes.FirstOrDefault();
        }

        public OpcionesSecciones? Opciones { get; private set; }
        public Action<bool>? Cerrar { get; set; }

        public ObservableCollection<EjeGuardado> Ejes { get; } = new ObservableCollection<EjeGuardado>();

        private EjeGuardado? ejeSelecc;
        public EjeGuardado? EjeSelecc
        {
            get => ejeSelecc;
            set
            {
                if (!SetProperty(ref ejeSelecc, value)) return;
                Placements.Clear();
                if (value != null)
                    foreach (Placement p in value.Eje.Placements.OrderBy(p => p.Estacion)) Placements.Add(new FilaPlacementElegido(p, true, Actualizar));
                enPlacements = Placements.Count > 0;
                OnPropertyChanged(nameof(EnPlacements));
                OnPropertyChanged(nameof(SinPlacements));
                Actualizar();
            }
        }

        public ObservableCollection<FilaPlacementElegido> Placements { get; } = new ObservableCollection<FilaPlacementElegido>();
        public bool SinPlacements => Placements.Count == 0;

        private bool enPlacements = true;
        public bool EnPlacements
        {
            get => enPlacements;
            set
            {
                if (SetProperty(ref enPlacements, value)) Actualizar();
            }
        }

        private bool entrePlacements;
        public bool EntrePlacements
        {
            get => entrePlacements;
            set
            {
                if (SetProperty(ref entrePlacements, value)) Actualizar();
            }
        }

        private string cada = "10";
        public string Cada
        {
            get => cada;
            set
            {
                if (SetProperty(ref cada, value)) Actualizar();
            }
        }

        private bool considerarGiro = true;
        public bool ConsiderarGiro
        {
            get => considerarGiro;
            set
            {
                if (SetProperty(ref considerarGiro, value)) Actualizar();
            }
        }

        //Estaciones sueltas separadas por punto y coma
        private string estaciones = "";
        public string Estaciones
        {
            get => estaciones;
            set
            {
                if (SetProperty(ref estaciones, value)) Actualizar();
            }
        }

        //Tamano del recorte
        private string ancho = "20";
        public string Ancho
        {
            get => ancho;
            set
            {
                if (SetProperty(ref ancho, value)) Actualizar();
            }
        }

        private string arriba = "5";
        public string Arriba
        {
            get => arriba;
            set
            {
                if (SetProperty(ref arriba, value)) Actualizar();
            }
        }

        private string abajo = "25";
        public string Abajo
        {
            get => abajo;
            set
            {
                if (SetProperty(ref abajo, value)) Actualizar();
            }
        }

        private string profundidad = "1";
        public string Profundidad
        {
            get => profundidad;
            set
            {
                if (SetProperty(ref profundidad, value)) Actualizar();
            }
        }

        public ObservableCollection<ViewFamilyType> TiposVista { get; } = new ObservableCollection<ViewFamilyType>();

        private ViewFamilyType? tipoVista;
        public ViewFamilyType? TipoVista
        {
            get => tipoVista;
            set => SetProperty(ref tipoVista, value);
        }

        private string escala = "100";
        public string Escala
        {
            get => escala;
            set
            {
                if (SetProperty(ref escala, value)) Actualizar();
            }
        }

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

        private OpcionesSecciones? listo;
        public bool PuedeCrear => listo != null;

        public RelayCommand TodosBT { get; }
        public RelayCommand NingunoBT { get; }
        public RelayCommand CrearBT { get; }
        public RelayCommand CancelarBT { get; }

        private void Marcar(bool valor)
        {
            foreach (FilaPlacementElegido fila in Placements) fila.Elegido = valor;
        }

        private void Actualizar()
        {
            Error = "";
            Resumen = "";
            listo = null;
            try
            {
                Validar();
            }
            catch (ArgumentException ex)
            {
                Error = ex.Message;
                listo = null;
            }

            CrearBT.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeCrear));
        }

        private void Validar()
        {
            if (EjeSelecc == null) throw new ArgumentException("Importe primero un eje con el botón Importar eje.");

            List<double> sueltas = new List<double>();
            foreach (string parte in Estaciones.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                if (!LeerNumero(parte, out double e)) throw new ArgumentException($"\"{parte}\" no es una estación (por ejemplo 0+050).");
                sueltas.Add(e);
            }

            if (!LeerNumero(Cada, out double cadaValor)) throw new ArgumentException("La separación entre secciones debe ser un número en metros.");
            if (!LeerNumero(Ancho, out double anchoValor) || !LeerNumero(Arriba, out double arribaValor) || !LeerNumero(Abajo, out double abajoValor)
                || !LeerNumero(Profundidad, out double profundidadValor) || anchoValor <= 0 || arribaValor + abajoValor <= 0 || profundidadValor <= 0)
                throw new ArgumentException("El tamaño del recorte debe ser de números positivos en metros.");
            if (!int.TryParse(Escala.Trim(), out int escalaValor) || escalaValor < 1) throw new ArgumentException("La escala debe ser un número entero, por ejemplo 100 para 1:100.");

            List<Placement> elegidos = Placements.Where(f => f.Elegido).Select(f => f.Placement).ToList();
            List<CorteSeccion> cortes = EstacionesSeccion.Calcular(EjeSelecc.Eje, elegidos, new OpcionesCortes
            {
                EnPlacements = EnPlacements,
                EntrePlacements = EntrePlacements,
                Cada = cadaValor,
                ConsiderarGiro = ConsiderarGiro,
                Estaciones = sueltas
            });

            if (cortes.Count == 0) throw new ArgumentException("No hay secciones que crear: elija placements o escriba estaciones.");

            Resumen = $"{cortes.Count} secciones a escala 1:{escalaValor}\n" +
                      string.Join("\n", cortes.Take(16).Select(c => $"{c.Nombre}   {Eje.FormatoEstacion(c.Estacion)}" + (Math.Abs(c.Giro) > 1e-9 ? $"   giro {c.Giro:0.##}°" : ""))) +
                      (cortes.Count > 16 ? $"\n(y {cortes.Count - 16} más)" : "");

            listo = new OpcionesSecciones
            {
                Eje = EjeSelecc,
                Cortes = cortes,
                Ancho = anchoValor,
                Arriba = arribaValor,
                Abajo = abajoValor,
                Profundidad = profundidadValor,
                Escala = escalaValor
            };
        }

        private void Crear()
        {
            if (listo == null) return;
            listo.TipoVista = TipoVista;
            Opciones = listo;
            Cerrar?.Invoke(true);
        }

        private static bool LeerNumero(string texto, out double valor) => Puentes.Superestructura.SeccionVariable.LeerNumero(texto, out valor);
    }
}
