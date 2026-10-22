using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Subestructura;

namespace ProyectoDoly.ViewModels
{
    //Placement con casilla para elegirlo
    public sealed class FilaPlacementElegido : ObservableObject
    {
        private readonly Action cambio;

        public FilaPlacementElegido(Placement placement, bool elegido, Action cambio)
        {
            Placement = placement;
            this.elegido = elegido;
            this.cambio = cambio;
        }

        public Placement Placement { get; }
        public string Texto => $"{Placement.Id}   {Eje.FormatoEstacion(Placement.Estacion)}" + (Math.Abs(Placement.Rotacion) > 1e-9 ? $"   giro {Placement.Rotacion:0.##}°" : "");

        private bool elegido;
        public bool Elegido
        {
            get => elegido;
            set
            {
                if (SetProperty(ref elegido, value)) cambio();
            }
        }
    }

    public sealed class vm_Subestructura : ObservableObject
    {
        private const string SinParametro = "(no cambiar la altura)";
        private readonly Document doc;

        public vm_Subestructura(Document doc, List<EjeGuardado> ejes, ElementId? preseleccionado)
        {
            this.doc = doc;
            CargarFamiliaBT = new RelayCommand(CargarFamilia);
            TodosBT = new RelayCommand(() => Marcar(true));
            NingunoBT = new RelayCommand(() => Marcar(false));
            CrearBT = new RelayCommand(Crear, () => PuedeCrear);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            foreach (EjeGuardado eje in ejes) Ejes.Add(eje);
            CargarTipos(null);
            EjeSelecc = Ejes.FirstOrDefault(e => e.Id == preseleccionado) ?? Ejes.FirstOrDefault();
        }

        public OpcionesSubestructura? Opciones { get; private set; }
        public Action<bool>? Cerrar { get; set; }

        //1_Eje y placements
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
                OnPropertyChanged(nameof(SinPlacements));
                Actualizar();
            }
        }

        public ObservableCollection<FilaPlacementElegido> Placements { get; } = new ObservableCollection<FilaPlacementElegido>();
        public bool SinPlacements => Placements.Count == 0;

        //2_Familia
        public ObservableCollection<OpcionSeccion> Tipos { get; } = new ObservableCollection<OpcionSeccion>();

        private OpcionSeccion? tipoSelecc;
        public OpcionSeccion? TipoSelecc
        {
            get => tipoSelecc;
            set
            {
                if (!SetProperty(ref tipoSelecc, value)) return;
                LeerParametros();
                Actualizar();
            }
        }

        private ModoSubestructura modo = ModoSubestructura.NoAdmitida;
        public bool EsLineal => modo == ModoSubestructura.PorLinea;
        public bool EsPilar => modo == ModoSubestructura.EntreNiveles;

        private string longitudTransversal = "10";
        public string LongitudTransversal
        {
            get => longitudTransversal;
            set
            {
                if (SetProperty(ref longitudTransversal, value)) Actualizar();
            }
        }

        private string insercion = "";
        public string Insercion
        {
            get => insercion;
            private set => SetProperty(ref insercion, value);
        }

        //3_Ubicacion
        private string desplazamientoLateral = "0";
        public string DesplazamientoLateral
        {
            get => desplazamientoLateral;
            set
            {
                if (SetProperty(ref desplazamientoLateral, value)) Actualizar();
            }
        }

        private string desplazamientoVertical = "0";
        public string DesplazamientoVertical
        {
            get => desplazamientoVertical;
            set
            {
                if (SetProperty(ref desplazamientoVertical, value)) Actualizar();
            }
        }

        private string giroAdicional = "0";
        public string GiroAdicional
        {
            get => giroAdicional;
            set
            {
                if (SetProperty(ref giroAdicional, value)) Actualizar();
            }
        }

        private bool siempreVertical = true;
        public bool SiempreVertical
        {
            get => siempreVertical;
            set
            {
                if (SetProperty(ref siempreVertical, value)) Actualizar();
            }
        }

        //4_Altura
        public ObservableCollection<string> ParametrosAltura { get; } = new ObservableCollection<string>();

        private string parametroAltura = SinParametro;
        public string ParametroAltura
        {
            get => parametroAltura;
            set
            {
                if (SetProperty(ref parametroAltura, value ?? SinParametro)) Actualizar();
            }
        }

        private string cotaFundacion = "0";
        public string CotaFundacion
        {
            get => cotaFundacion;
            set
            {
                if (SetProperty(ref cotaFundacion, value)) Actualizar();
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

        private OpcionesSubestructura? listo;
        public bool PuedeCrear => listo != null;

        public RelayCommand CargarFamiliaBT { get; }
        public RelayCommand TodosBT { get; }
        public RelayCommand NingunoBT { get; }
        public RelayCommand CrearBT { get; }
        public RelayCommand CancelarBT { get; }

        private void CargarTipos(ElementId? familia)
        {
            Tipos.Clear();
            foreach (FamilySymbol tipo in CreadorSubestructura.Tipos(doc)) Tipos.Add(new OpcionSeccion { Tipo = tipo });

            //Tras cargar una familia se elige su primer tipo; si no, una que parezca pila o estribo
            string[] pistas = { "PILA", "PIER", "ESTRIBO", "ABUTMENT", "SUBSTRUCTURE", "SUBESTRUCTURA" };
            tipoSelecc = (familia != null ? Tipos.FirstOrDefault(t => t.Tipo.Family.Id == familia) : null)
                         ?? Tipos.FirstOrDefault(t => pistas.Any(p => t.Nombre.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0))
                         ?? Tipos.FirstOrDefault();
            OnPropertyChanged(nameof(TipoSelecc));
            LeerParametros();
        }

        private void CargarFamilia()
        {
            OpenFileDialog dialogo = new OpenFileDialog { Title = "Familia de pila o estribo", Filter = "Familias de Revit (*.rfa)|*.rfa" };
            if (dialogo.ShowDialog() != true) return;

            try
            {
                Family familia = SeccionFamilia.Cargar(doc, dialogo.FileName);
                CargarTipos(familia.Id);
                Actualizar();
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Autodesk.Revit.Exceptions.ApplicationException)
            {
                Error = $"No se pudo cargar la familia: {ex.Message}";
            }
        }

        //Parametros de longitud de la familia para la altura (se abre la familia en segundo plano y se cierra enseguida)
        private void LeerParametros()
        {
            string anterior = ParametroAltura;
            ParametrosAltura.Clear();
            ParametrosAltura.Add(SinParametro);
            Insercion = "";
            modo = TipoSelecc == null ? ModoSubestructura.NoAdmitida : CreadorSubestructura.Modo(TipoSelecc.Tipo.Family);
            OnPropertyChanged(nameof(EsLineal));
            OnPropertyChanged(nameof(EsPilar));
            if (TipoSelecc == null) return;

            Insercion = "Se inserta " + CreadorSubestructura.Insercion(TipoSelecc.Tipo.Family);
            try
            {
                using (LectorSeccion lector = new LectorSeccion(doc, TipoSelecc.Tipo))
                    foreach (ParametroSeccion p in lector.Parametros.Where(p => p.Unidad == UnidadParametro.Metros && !p.DeTipo))
                        ParametrosAltura.Add(p.Nombre);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Autodesk.Revit.Exceptions.ApplicationException)
            {
                //Sin lista de parametros: se puede colocar igual, sin altura
            }

            //Se propone un parametro que parezca la altura
            string? propuesto = ParametrosAltura.FirstOrDefault(n => n == anterior)
                                ?? ParametrosAltura.Skip(1).FirstOrDefault(n => new[] { "altura", "height", "alto", "h_" }.Any(p => n.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0));
            parametroAltura = propuesto ?? SinParametro;
            OnPropertyChanged(nameof(ParametroAltura));
        }

        private void Marcar(bool valor)
        {
            foreach (FilaPlacementElegido fila in Placements) fila.Elegido = valor;
        }

        private void Actualizar()
        {
            Error = "";
            listo = null;
            Resumen = "";

            if (EjeSelecc == null) MostrarError("Importe primero un eje con el botón Importar eje.");
            else if (Placements.Count == 0) MostrarError("El eje no tiene placements. Créelos con el botón Placements.");
            else if (TipoSelecc == null) MostrarError("Cargue una familia de pila, cimentación o estribo.");
            else if (Insercion.Contains("no admitida")) MostrarError($"{TipoSelecc.Nombre}: {Insercion.Replace("Se inserta ", "")}.");
            else Validar(EjeSelecc);

            CrearBT.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeCrear));
        }

        private void Validar(EjeGuardado eje)
        {
            //Uno o varios desplazamientos a la derecha separados por punto y coma (una fila de ejemplares por placement)
            List<double> laterales = new List<double>();
            bool lateralesOk = true;
            foreach (string parte in DesplazamientoLateral.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                if (LeerNumero(parte, out double valor)) laterales.Add(valor);
                else lateralesOk = false;
            }

            if (!lateralesOk || laterales.Count == 0 || !LeerNumero(DesplazamientoVertical, out double vertical) || !LeerNumero(GiroAdicional, out double giro))
            {
                MostrarError("Los desplazamientos y el giro deben ser números (0 = sin cambio). A la derecha admite varios valores con punto y coma: -4.5; 0; 4.5");
                return;
            }

            double lateral = laterales[0];

            bool conAltura = ParametroAltura != SinParametro;
            double fundacion = 0;
            if ((conAltura || EsPilar) && !LeerNumero(CotaFundacion, out fundacion))
            {
                MostrarError(EsPilar ? "Un pilar va desde la cota de fundación hasta el placement: escriba esa cota en metros." : "La cota de fundación debe ser un número en metros.");
                return;
            }

            double longitud = 10;
            if (EsLineal && (!LeerNumero(LongitudTransversal, out longitud) || longitud <= 0))
            {
                MostrarError("La longitud del estribo debe ser un número mayor que cero, en metros.");
                return;
            }

            List<Placement> elegidos = Placements.Where(f => f.Elegido).Select(f => f.Placement).ToList();
            if (elegidos.Count == 0)
            {
                MostrarError("Elija al menos un placement.");
                return;
            }

            OpcionesUbicacion ubicacion = new OpcionesUbicacion
            {
                DesplazamientoLateral = lateral,
                DesplazamientoVertical = vertical,
                GiroAdicional = giro,
                SiempreVertical = SiempreVertical
            };

            //Vista previa: cota del punto de insercion y altura de cada pila
            List<string> lineas = new List<string>();
            if (laterales.Count > 1) lineas.Add($"{laterales.Count} por placement, a {string.Join("; ", laterales.Select(l => l.ToString("0.##", CultureInfo.InvariantCulture)))} m");
            foreach (Placement p in elegidos.Take(12))
            {
                Ubicacion3 u = UbicacionSubestructura.Calcular(eje.Eje, p, ubicacion);
                lineas.Add($"{p.Id}:  cota {u.Origen.Z:F2} m" + (conAltura || EsPilar ? $"  ·  altura {UbicacionSubestructura.Altura(u, fundacion):F2} m" : ""));
                if (EsPilar && UbicacionSubestructura.Altura(u, fundacion) < 0.01)
                {
                    MostrarError($"En {p.Id} la cota de fundación queda por encima del placement.");
                    return;
                }
            }

            Resumen = $"{elegidos.Count * laterales.Count} ejemplares de {TipoSelecc!.Nombre}\n" + string.Join("\n", lineas) + (elegidos.Count > 12 ? $"\n(y {elegidos.Count - 12} más)" : "");

            listo = new OpcionesSubestructura
            {
                Eje = eje,
                Tipo = TipoSelecc.Tipo,
                Placements = elegidos,
                Ubicacion = ubicacion,
                Laterales = laterales.Count > 1 ? laterales : new List<double>(),
                ParametroAltura = conAltura ? ParametroAltura : null,
                CotaFundacion = fundacion,
                LongitudTransversal = longitud
            };
        }

        private void Crear()
        {
            if (listo == null) return;
            Opciones = listo;
            Cerrar?.Invoke(true);
        }

        private void MostrarError(string texto)
        {
            Error = texto;
            CrearBT.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeCrear));
        }

        private static bool LeerNumero(string texto, out double valor) => Puentes.Superestructura.SeccionVariable.LeerNumero(texto, out valor);
    }
}
