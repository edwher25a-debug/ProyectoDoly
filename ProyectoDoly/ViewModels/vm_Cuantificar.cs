using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyectoDoly.Puentes;

namespace ProyectoDoly.ViewModels
{
    public sealed class vm_Cuantificar : ObservableObject
    {
        private readonly Document doc;
        private readonly List<ElementId> seleccion;
        private List<ElementId> creados = new List<ElementId>();

        public vm_Cuantificar(Document doc, List<EjeGuardado> ejes, ElementId? preseleccionado, List<ElementId> seleccion)
        {
            this.doc = doc;
            this.seleccion = seleccion;
            CrearBT = new RelayCommand(Crear, () => PuedeCrear);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            usarSeleccion = seleccion.Count > 0;
            foreach (EjeGuardado eje in ejes) Ejes.Add(eje);
            EjeSelecc = Ejes.FirstOrDefault(e => e.Id == preseleccionado) ?? Ejes.FirstOrDefault();
        }

        public OpcionesCuantificacion? Opciones { get; private set; }
        public Action<bool>? Cerrar { get; set; }

        public ObservableCollection<EjeGuardado> Ejes { get; } = new ObservableCollection<EjeGuardado>();

        private EjeGuardado? ejeSelecc;
        public EjeGuardado? EjeSelecc
        {
            get => ejeSelecc;
            set
            {
                if (!SetProperty(ref ejeSelecc, value)) return;
                creados = value != null ? CuantificadorPuente.CreadosEnEje(doc, value.Eje) : new List<ElementId>();
                OnPropertyChanged(nameof(TextoCreados));
                Actualizar();
            }
        }

        public bool HaySeleccion => seleccion.Count > 0;
        public string TextoSeleccion => $"Los elementos seleccionados ({seleccion.Count})";
        public string TextoCreados => $"Todo lo creado con Puentes en este eje: tableros, parapetos, pilas y estribos ({creados.Count})";

        private bool usarSeleccion;
        public bool UsarSeleccion
        {
            get => usarSeleccion;
            set
            {
                if (!SetProperty(ref usarSeleccion, value)) return;
                OnPropertyChanged(nameof(UsarCreados));
                Actualizar();
            }
        }

        public bool UsarCreados
        {
            get => !usarSeleccion;
            set => UsarSeleccion = !value;
        }

        public string Parametros => string.Join("\n", ParametrosPuente.Todos.Select(d => d.Nombre));

        private string error = "";
        public string Error
        {
            get => error;
            private set => SetProperty(ref error, value);
        }

        public bool PuedeCrear => EjeSelecc != null && Elegidos.Count > 0;
        private List<ElementId> Elegidos => UsarSeleccion ? seleccion : creados;

        public RelayCommand CrearBT { get; }
        public RelayCommand CancelarBT { get; }

        private void Actualizar()
        {
            Error = EjeSelecc == null ? "Importe primero un eje con el botón Importar eje."
                : Elegidos.Count == 0 ? (UsarSeleccion ? "Seleccione elementos en el modelo antes de abrir Cuantificar." : "No hay elementos creados con Puentes en este eje.")
                : "";
            CrearBT.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeCrear));
        }

        private void Crear()
        {
            if (!PuedeCrear) return;
            Opciones = new OpcionesCuantificacion { Eje = EjeSelecc!, Elementos = Elegidos.ToList() };
            Cerrar?.Invoke(true);
        }
    }
}
