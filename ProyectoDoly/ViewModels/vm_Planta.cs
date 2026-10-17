using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Planos;

namespace ProyectoDoly.ViewModels
{
    public sealed class vm_Planta : ObservableObject
    {
        private readonly ViewPlan? vista;

        public vm_Planta(Document doc, List<EjeGuardado> ejes, ElementId? preseleccionado, ViewPlan? vista)
        {
            this.vista = vista;
            TodosBT = new RelayCommand(() => Marcar(true));
            NingunoBT = new RelayCommand(() => Marcar(false));
            CrearBT = new RelayCommand(Crear, () => PuedeCrear);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            foreach (TextNoteType tipo in CreadorPlanta.TiposTexto(doc)) TiposTexto.Add(tipo);
            tipoTexto = TiposTexto.FirstOrDefault(t => t.Id == doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType)) ?? TiposTexto.FirstOrDefault();
            crearEstaciones = vista != null;
            foreach (EjeGuardado eje in ejes) Ejes.Add(eje);
            EjeSelecc = Ejes.FirstOrDefault(e => e.Id == preseleccionado) ?? Ejes.FirstOrDefault();
        }

        public OpcionesPlanta? Opciones { get; private set; }
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
                crearRejillas = Placements.Count > 0;
                OnPropertyChanged(nameof(CrearRejillas));
                OnPropertyChanged(nameof(SinPlacements));
                Actualizar();
            }
        }

        //1_Rejillas
        private bool crearRejillas;
        public bool CrearRejillas
        {
            get => crearRejillas;
            set
            {
                if (SetProperty(ref crearRejillas, value)) Actualizar();
            }
        }

        public ObservableCollection<FilaPlacementElegido> Placements { get; } = new ObservableCollection<FilaPlacementElegido>();
        public bool SinPlacements => Placements.Count == 0;

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

        private string largoRejilla = "30";
        public string LargoRejilla
        {
            get => largoRejilla;
            set
            {
                if (SetProperty(ref largoRejilla, value)) Actualizar();
            }
        }

        private string prefijo = "";
        public string Prefijo
        {
            get => prefijo;
            set
            {
                if (SetProperty(ref prefijo, value ?? "")) Actualizar();
            }
        }

        //2_Estaciones en la vista de planta activa
        public bool HayVistaPlanta => vista != null;
        public string NombreVista => vista != null ? $"En la vista activa: {vista.Name}" : "Abra una vista de planta para marcar estaciones.";

        private bool crearEstaciones;
        public bool CrearEstaciones
        {
            get => crearEstaciones;
            set
            {
                if (SetProperty(ref crearEstaciones, value)) Actualizar();
            }
        }

        private string cadaEstacion = "20";
        public string CadaEstacion
        {
            get => cadaEstacion;
            set
            {
                if (SetProperty(ref cadaEstacion, value)) Actualizar();
            }
        }

        private bool extremos = true;
        public bool Extremos
        {
            get => extremos;
            set
            {
                if (SetProperty(ref extremos, value)) Actualizar();
            }
        }

        private string largoMarca = "4";
        public string LargoMarca
        {
            get => largoMarca;
            set
            {
                if (SetProperty(ref largoMarca, value)) Actualizar();
            }
        }

        public ObservableCollection<TextNoteType> TiposTexto { get; } = new ObservableCollection<TextNoteType>();

        private TextNoteType? tipoTexto;
        public TextNoteType? TipoTexto
        {
            get => tipoTexto;
            set => SetProperty(ref tipoTexto, value);
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

        private OpcionesPlanta? listo;
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
            if (!CrearRejillas && !CrearEstaciones) throw new ArgumentException("Elija crear rejillas, marcas de estación o ambas.");

            OpcionesPlanta opciones = new OpcionesPlanta { Eje = EjeSelecc };
            List<string> lineas = new List<string>();

            if (CrearRejillas)
            {
                if (Placements.Count == 0) throw new ArgumentException("El eje no tiene placements para las rejillas. Créelos con el botón Placements.");
                if (!LeerNumero(Cada, out double cadaValor)) throw new ArgumentException("La separación entre rejillas debe ser un número en metros.");
                if (!LeerNumero(LargoRejilla, out double largo) || largo <= 0) throw new ArgumentException("El largo de las rejillas debe ser un número positivo en metros.");

                List<Placement> elegidos = Placements.Where(f => f.Elegido).Select(f => f.Placement).ToList();
                if (elegidos.Count == 0) throw new ArgumentException("Elija al menos un placement para las rejillas.");
                opciones.Rejillas = EstacionesSeccion.Calcular(EjeSelecc.Eje, elegidos, new OpcionesCortes
                {
                    EnPlacements = true,
                    EntrePlacements = EntrePlacements,
                    Cada = cadaValor,
                    ConsiderarGiro = ConsiderarGiro
                });
                opciones.LargoRejilla = largo;
                opciones.PrefijoRejilla = Prefijo.Trim();

                lineas.Add($"{opciones.Rejillas.Count} rejillas de {largo:0.##} m");
                lineas.AddRange(opciones.Rejillas.Take(10).Select(c => $"   {opciones.PrefijoRejilla}{c.Nombre}   {Eje.FormatoEstacion(c.Estacion)}"));
                if (opciones.Rejillas.Count > 10) lineas.Add($"   (y {opciones.Rejillas.Count - 10} más)");
            }

            if (CrearEstaciones)
            {
                if (vista == null) throw new ArgumentException("Abra una vista de planta para marcar las estaciones.");
                if (!LeerNumero(CadaEstacion, out double cadaValor) || cadaValor <= 0) throw new ArgumentException("La separación de las estaciones debe ser un número positivo en metros.");
                if (!LeerNumero(LargoMarca, out double largo) || largo <= 0) throw new ArgumentException("El largo de la raya debe ser un número positivo en metros.");

                opciones.Vista = vista;
                opciones.Estaciones = MarcasPlanta.EstacionesCada(EjeSelecc.Eje, cadaValor, Extremos);
                opciones.LargoMarca = largo;
                if (opciones.Estaciones.Count > 2000) throw new ArgumentException($"Serían {opciones.Estaciones.Count} marcas: aumente la separación.");

                if (lineas.Count > 0) lineas.Add("");
                lineas.Add($"{opciones.Estaciones.Count} marcas de estación cada {cadaValor:0.##} m");
                lineas.Add($"   {Eje.FormatoEstacion(opciones.Estaciones.First())} a {Eje.FormatoEstacion(opciones.Estaciones.Last())}");
            }

            Resumen = string.Join("\n", lineas);
            listo = opciones;
        }

        private void Crear()
        {
            if (listo == null) return;
            listo.TipoTexto = TipoTexto;
            Opciones = listo;
            Cerrar?.Invoke(true);
        }

        private static bool LeerNumero(string texto, out double valor) => Puentes.Superestructura.SeccionVariable.LeerNumero(texto, out valor);
    }
}
