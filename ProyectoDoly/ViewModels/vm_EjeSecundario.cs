using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.ViewModels
{
    public sealed class vm_EjeSecundario : ObservableObject
    {
        private readonly List<EjeGuardado> todos;

        public vm_EjeSecundario(List<EjeGuardado> ejes, ElementId? preseleccionado)
        {
            todos = ejes;
            CrearBT = new RelayCommand(Crear, () => PuedeCrear);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            foreach (EjeGuardado eje in ejes) Ejes.Add(eje);
            EjeSelecc = Ejes.FirstOrDefault(e => e.Id == preseleccionado) ?? Ejes.FirstOrDefault();
        }

        public Eje? Resultado { get; private set; }
        public string Descripcion { get; private set; } = "";

        //Para volver a crear el eje secundario si cambia el principal
        public OpcionesEjeSecundario? OpcionesUsadas { get; private set; }
        public Action<bool>? Cerrar { get; set; }

        public ObservableCollection<EjeGuardado> Ejes { get; } = new ObservableCollection<EjeGuardado>();

        private EjeGuardado? ejeSelecc;
        public EjeGuardado? EjeSelecc
        {
            get => ejeSelecc;
            set
            {
                if (!SetProperty(ref ejeSelecc, value)) return;
                if (value != null) nombre = Libre($"{value.Eje.Nombre} borde");
                OnPropertyChanged(nameof(Nombre));
                Actualizar();
            }
        }

        private string nombre = "";
        public string Nombre
        {
            get => nombre;
            set
            {
                if (SetProperty(ref nombre, value ?? "")) Actualizar();
            }
        }

        private bool constante = true;
        public bool Constante
        {
            get => constante;
            set
            {
                if (!SetProperty(ref constante, value)) return;
                OnPropertyChanged(nameof(Variable));
                Actualizar();
            }
        }

        public bool Variable
        {
            get => !constante;
            set => Constante = !value;
        }

        private string derecha = "6";
        public string Derecha
        {
            get => derecha;
            set
            {
                if (SetProperty(ref derecha, value)) Actualizar();
            }
        }

        //Tabla "estacion = desplazamiento; ..." para desplazamiento variable
        private string tabla = "";
        public string Tabla
        {
            get => tabla;
            set
            {
                if (SetProperty(ref tabla, value)) Actualizar();
            }
        }

        private string subir = "0";
        public string Subir
        {
            get => subir;
            set
            {
                if (SetProperty(ref subir, value)) Actualizar();
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

        private Eje? listo;
        public bool PuedeCrear => listo != null;

        public RelayCommand CrearBT { get; }
        public RelayCommand CancelarBT { get; }

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
            if (EjeSelecc == null) throw new ArgumentException("Importe primero el eje principal con el botón Importar eje.");
            string limpio = Nombre.Trim();
            if (limpio.Length == 0) throw new ArgumentException("Escriba el nombre del eje secundario.");
            if (todos.Any(e => string.Equals(e.Eje.Nombre, limpio, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Ya hay un eje llamado \"{limpio}\".");
            if (!LeerNumero(Subir, out double subirValor)) throw new ArgumentException("El desplazamiento vertical debe ser un número en metros.");

            Eje principal = EjeSelecc.Eje;
            List<(double, double)> valores = new List<(double, double)>();
            if (Constante)
            {
                if (!LeerNumero(Derecha, out double d)) throw new ArgumentException("El desplazamiento a la derecha debe ser un número en metros (negativo = izquierda).");
                valores.Add((principal.EstacionInicial, d));
            }
            else
            {
                foreach (string parte in Tabla.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0))
                {
                    string[] lados = parte.Split('=');
                    if (lados.Length != 2 || !LeerNumero(lados[0], out double e) || !LeerNumero(lados[1], out double d))
                        throw new ArgumentException($"\"{parte}\" debe ser estación = desplazamiento, por ejemplo 0+040 = 6.5");
                    if (e < principal.EstacionInicial - 1e-6 || e > principal.EstacionFinal + 1e-6)
                        throw new ArgumentException($"La estación {Eje.FormatoEstacion(e)} está fuera del eje principal.");
                    valores.Add((e, d));
                }

                if (valores.Count < 2) throw new ArgumentException("Escriba al menos dos puntos estación = desplazamiento.");
            }

            OpcionesEjeSecundario opciones = new OpcionesEjeSecundario { Nombre = limpio, Derecha = valores, Subir = subirValor };
            Eje secundario = EjeSecundario.Crear(principal, opciones);

            Resumen = $"Eje \"{secundario.Nombre}\"\n" +
                      $"Estaciones: {Eje.FormatoEstacion(secundario.EstacionInicial)} a {Eje.FormatoEstacion(secundario.EstacionFinal)}\n" +
                      $"Longitud: {secundario.Horizontal.Longitud:F3} m (principal {principal.Horizontal.Longitud:F3} m)\n" +
                      $"Tramos: {secundario.Horizontal.Elementos.Count}\n" +
                      (secundario.Placements.Count > 0 ? $"Placements trasladados: {secundario.Placements.Count}\n" : "") +
                      (Constante ? $"Desplazamiento: {Texto(valores[0].Item2)} m a la derecha" : $"Desplazamiento variable en {valores.Count} puntos") +
                      (Math.Abs(subirValor) > 1e-9 ? $", {Texto(subirValor)} m en altura" : "");

            Descripcion = $"Eje secundario | {secundario.Nombre} | de {principal.Nombre} | " +
                          (Constante ? $"derecha {Texto(valores[0].Item2)} m" : string.Join("; ", valores.Select(v => $"{Eje.FormatoEstacion(v.Item1)}={Texto(v.Item2)}"))) +
                          (Math.Abs(subirValor) > 1e-9 ? $" | subir {Texto(subirValor)} m" : "");
            OpcionesUsadas = opciones;
            listo = secundario;
        }

        private void Crear()
        {
            if (listo == null) return;
            Resultado = listo;
            Cerrar?.Invoke(true);
        }

        private string Libre(string baseNombre)
        {
            string libre = baseNombre;
            for (int i = 2; todos.Any(e => string.Equals(e.Eje.Nombre, libre, StringComparison.OrdinalIgnoreCase)); i++) libre = $"{baseNombre} {i}";
            return libre;
        }

        private static string Texto(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static bool LeerNumero(string texto, out double valor) => Puentes.Superestructura.SeccionVariable.LeerNumero(texto, out valor);
    }
}
