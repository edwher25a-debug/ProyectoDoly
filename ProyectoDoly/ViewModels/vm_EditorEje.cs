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
    //Sistema de coordenadas en que se escriben el origen y la direccion
    public enum SistemaEje
    {
        Proyecto,     //Respecto al punto base del proyecto
        Compartidas,  //Coordenadas compartidas (topograficas)
        Interno       //Origen interno de Revit
    }

    //Fila de la tabla de elementos (texto para aceptar coma decimal); la estacion final la calcula la ventana
    public sealed class FilaElemento : ObservableObject
    {
        private readonly Action cambio;

        public FilaElemento(Action cambio, string tipo = "Recta", string longitud = "", string radioInicio = "", string radioFin = "")
        {
            this.cambio = cambio;
            this.tipo = tipo;
            this.longitud = longitud;
            this.radioInicio = radioInicio;
            this.radioFin = radioFin;
        }

        private string tipo;
        public string Tipo
        {
            get => tipo;
            set
            {
                if (!SetProperty(ref tipo, value ?? "Recta")) return;
                OnPropertyChanged(nameof(UsaRadioInicio));
                OnPropertyChanged(nameof(UsaRadioFin));
                cambio();
            }
        }

        private string longitud;
        public string Longitud
        {
            get => longitud;
            set
            {
                if (SetProperty(ref longitud, value)) cambio();
            }
        }

        private string radioInicio;
        public string RadioInicio
        {
            get => radioInicio;
            set
            {
                if (SetProperty(ref radioInicio, value)) cambio();
            }
        }

        private string radioFin;
        public string RadioFin
        {
            get => radioFin;
            set
            {
                if (SetProperty(ref radioFin, value)) cambio();
            }
        }

        public bool UsaRadioInicio => Tipo != "Recta";
        public bool UsaRadioFin => Tipo == "Clotoide";

        private string estacion = "";
        public string Estacion
        {
            get => estacion;
            set => SetProperty(ref estacion, value);
        }
    }

    //Fila de la rasante: PIV con su estacion, cota y longitud de curva vertical
    public sealed class FilaPiv : ObservableObject
    {
        private readonly Action cambio;

        public FilaPiv(Action cambio, string estacion = "", string cota = "", string curva = "0")
        {
            this.cambio = cambio;
            this.estacion = estacion;
            this.cota = cota;
            this.curva = curva;
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

        private string cota;
        public string Cota
        {
            get => cota;
            set
            {
                if (SetProperty(ref cota, value)) cambio();
            }
        }

        private string curva;
        public string Curva
        {
            get => curva;
            set
            {
                if (SetProperty(ref curva, value)) cambio();
            }
        }
    }

    //Tramo del eje en la vista previa, con el color de su tipo
    public sealed class TramoVista
    {
        public PointCollection Puntos { get; set; } = new PointCollection();
        public Brush Color { get; set; } = Brushes.Black;
    }

    /// <summary>
    ///     Editor de ejes por elementos, como el cuadro Axis Data de SOFiSTiK: pestañas Horizontal, Vertical y
    ///     Placements, con vista previa en planta y perfil
    /// </summary>
    public sealed class vm_EditorEje : ObservableObject
    {
        public const double AnchoVista = 360;
        public const double AltoPlanta = 250;
        public const double AltoPerfil = 100;

        public static readonly string[] TiposElemento = { "Recta", "Clotoide", "Arco" };

        private static readonly Brush ColorRecta = Congelar(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x4A, 0x63)));
        private static readonly Brush ColorClotoide = Congelar(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF2, 0x8C, 0x28)));
        private static readonly Brush ColorArco = Congelar(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x9D, 0x8F)));

        private readonly List<string> existentes;
        private readonly EjeGuardado? editado;
        private bool cargando = true;

        public vm_EditorEje(List<EjeGuardado> ejes, EjeGuardado? editar)
        {
            editado = editar;
            existentes = ejes.Where(e => editar == null || e.Id != editar.Id).Select(e => e.Eje.Nombre).ToList();

            AgregarBT = new RelayCommand(Agregar);
            QuitarBT = new RelayCommand(Quitar, () => FilaSelecc != null);
            SubirBT = new RelayCommand(() => Mover(-1), () => FilaSelecc != null && Filas.IndexOf(FilaSelecc) > 0);
            BajarBT = new RelayCommand(() => Mover(1), () => FilaSelecc != null && Filas.IndexOf(FilaSelecc) < Filas.Count - 1);
            AgregarPivBT = new RelayCommand(() => FilasPiv.Add(new FilaPiv(Actualizar, PivSiguiente(), CotaSiguiente())));
            QuitarPivBT = new RelayCommand(() => FilasPiv.Remove(PivSelecc!), () => PivSelecc != null);
            AgregarPlacementBT = new RelayCommand(AgregarPlacement);
            QuitarPlacementBT = new RelayCommand(() => FilasPlacement.Remove(PlacementSelecc!), () => PlacementSelecc != null);
            PasarPiBT = new RelayCommand(PasarPi);
            CrearBT = new RelayCommand(Crear, () => PuedeCrear);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            Filas.CollectionChanged += (s, e) => Actualizar();
            FilasPiv.CollectionChanged += (s, e) => Actualizar();
            FilasPlacement.CollectionChanged += (s, e) => Actualizar();

            if (editar != null) Cargar(editar.Eje);
            else
            {
                nombre = Libre("Eje");
                Filas.Add(new FilaElemento(Actualizar, "Recta", "100"));
            }

            cargando = false;
            Actualizar();
        }

        public Eje? Resultado { get; private set; }
        public string Descripcion { get; private set; } = "";
        public Action<bool>? Cerrar { get; set; }

        public bool Editando => editado != null;
        public bool Nuevo => editado == null;
        public string Titulo => Editando ? $"Editar eje {editado!.Eje.Nombre}" : "Eje por tabla";
        public string TextoBoton => Editando ? "Guardar cambios" : "Crear eje";
        public IReadOnlyList<string> Tipos => TiposElemento;

        //Datos generales
        private string nombre = "";
        public string Nombre
        {
            get => nombre;
            set
            {
                if (SetProperty(ref nombre, value ?? "")) Actualizar();
            }
        }

        private string origenX = "0";
        public string OrigenX
        {
            get => origenX;
            set
            {
                if (SetProperty(ref origenX, value)) Actualizar();
            }
        }

        private string origenY = "0";
        public string OrigenY
        {
            get => origenY;
            set
            {
                if (SetProperty(ref origenY, value)) Actualizar();
            }
        }

        private string direccionX = "1";
        public string DireccionX
        {
            get => direccionX;
            set
            {
                if (SetProperty(ref direccionX, value)) Actualizar();
            }
        }

        private string direccionY = "0";
        public string DireccionY
        {
            get => direccionY;
            set
            {
                if (SetProperty(ref direccionY, value)) Actualizar();
            }
        }

        private string estacionInicial = "0+000.000";
        public string EstacionInicial
        {
            get => estacionInicial;
            set
            {
                if (SetProperty(ref estacionInicial, value)) Actualizar();
            }
        }

        public IReadOnlyList<SistemaEje> Sistemas { get; } = new[] { SistemaEje.Proyecto, SistemaEje.Compartidas, SistemaEje.Interno };

        private SistemaEje sistema = SistemaEje.Proyecto;
        public SistemaEje Sistema
        {
            get => sistema;
            set => SetProperty(ref sistema, value);
        }

        private string marcasCada = "20";
        public string MarcasCada
        {
            get => marcasCada;
            set
            {
                if (SetProperty(ref marcasCada, value)) Actualizar();
            }
        }

        public double MarcasCadaValor { get; private set; } = 20;

        //Horizontal
        public ObservableCollection<FilaElemento> Filas { get; } = new ObservableCollection<FilaElemento>();

        private FilaElemento? filaSelecc;
        public FilaElemento? FilaSelecc
        {
            get => filaSelecc;
            set
            {
                if (!SetProperty(ref filaSelecc, value)) return;
                QuitarBT.NotifyCanExecuteChanged();
                SubirBT.NotifyCanExecuteChanged();
                BajarBT.NotifyCanExecuteChanged();
            }
        }

        //Vertical
        public ObservableCollection<FilaPiv> FilasPiv { get; } = new ObservableCollection<FilaPiv>();

        private FilaPiv? pivSelecc;
        public FilaPiv? PivSelecc
        {
            get => pivSelecc;
            set
            {
                if (SetProperty(ref pivSelecc, value)) QuitarPivBT.NotifyCanExecuteChanged();
            }
        }

        //Placements
        public ObservableCollection<FilaPlacement> FilasPlacement { get; } = new ObservableCollection<FilaPlacement>();

        private FilaPlacement? placementSelecc;
        public FilaPlacement? PlacementSelecc
        {
            get => placementSelecc;
            set
            {
                if (SetProperty(ref placementSelecc, value)) QuitarPlacementBT.NotifyCanExecuteChanged();
            }
        }

        //Desde PI: tabla X; Y; radio; clotoide que se convierte en elementos
        private string tablaPi = "";
        public string TablaPi
        {
            get => tablaPi;
            set => SetProperty(ref tablaPi, value);
        }

        private string mensajePi = "";
        public string MensajePi
        {
            get => mensajePi;
            private set => SetProperty(ref mensajePi, value);
        }

        //Vista previa
        public ObservableCollection<TramoVista> Tramos { get; } = new ObservableCollection<TramoVista>();
        public ObservableCollection<MarcaPlanta> Marcas { get; } = new ObservableCollection<MarcaPlanta>();

        private PointCollection perfil = new PointCollection();
        public PointCollection Perfil
        {
            get => perfil;
            private set => SetProperty(ref perfil, value);
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

        public RelayCommand AgregarBT { get; }
        public RelayCommand QuitarBT { get; }
        public RelayCommand SubirBT { get; }
        public RelayCommand BajarBT { get; }
        public RelayCommand AgregarPivBT { get; }
        public RelayCommand QuitarPivBT { get; }
        public RelayCommand AgregarPlacementBT { get; }
        public RelayCommand QuitarPlacementBT { get; }
        public RelayCommand PasarPiBT { get; }
        public RelayCommand CrearBT { get; }
        public RelayCommand CancelarBT { get; }

        //Lleva un eje existente a la tabla
        private void Cargar(Eje eje)
        {
            nombre = eje.Nombre;
            ElementoHorizontal primero = eje.Horizontal.Elementos[0];
            origenX = Texto(primero.Inicio.X);
            origenY = Texto(primero.Inicio.Y);
            direccionX = Texto(Math.Cos(primero.RumboInicio), "0.######");
            direccionY = Texto(Math.Sin(primero.RumboInicio), "0.######");
            estacionInicial = Eje.FormatoEstacion(eje.EstacionInicial);
            marcasCada = Texto(eje.MarcasCada);

            foreach (DatoElemento f in EjePorElementos.Filas(eje.Horizontal))
                Filas.Add(new FilaElemento(Actualizar, EjePorElementos.Nombre(f.Tipo), Texto(f.Longitud),
                    f.RadioInicio.HasValue ? Texto(f.RadioInicio.Value) : "", f.RadioFin.HasValue ? Texto(f.RadioFin.Value) : ""));
            if (eje.TieneRasante)
                foreach (PuntoVertical p in eje.Vertical.Puntos)
                    FilasPiv.Add(new FilaPiv(Actualizar, Eje.FormatoEstacion(p.Estacion), Texto(p.Cota), Texto(p.LongitudCurva)));
            foreach (Placement p in eje.Placements.OrderBy(p => p.Estacion))
                FilasPlacement.Add(new FilaPlacement(p, Actualizar));
        }

        private void Agregar()
        {
            //Despues de la fila elegida, continuando su curvatura: tras un arco, clotoide de salida; tras una clotoide
            //que termina en curva, el arco; si no, una recta
            int indice = FilaSelecc != null ? Filas.IndexOf(FilaSelecc) + 1 : Filas.Count;
            FilaElemento? anterior = indice > 0 ? Filas[indice - 1] : null;
            bool enCurva(string r) => !string.IsNullOrWhiteSpace(r) && LeerNumero(r, out double v) && Math.Abs(v) > 1e-9;
            FilaElemento nueva = anterior?.Tipo == "Arco" && enCurva(anterior.RadioInicio)
                ? new FilaElemento(Actualizar, "Clotoide", "50", anterior.RadioInicio, "0")
                : anterior?.Tipo == "Clotoide" && enCurva(anterior.RadioFin)
                    ? new FilaElemento(Actualizar, "Arco", "50", anterior.RadioFin)
                    : new FilaElemento(Actualizar, "Recta", "50");
            Filas.Insert(indice, nueva);
            FilaSelecc = nueva;
        }

        //Convierte la poligonal de PI en rectas, clotoides y arcos y reemplaza la tabla de elementos
        private void PasarPi()
        {
            try
            {
                List<PuntoInflexion> pis = EjePorTabla.Leer(TablaPi);
                if (pis.Count < 2) throw new ArgumentException("Escriba al menos dos PI, uno por línea: X; Y; radio; clotoide.");
                LeerNumero(EstacionInicial, out double inicial);
                Eje eje = EjePorTabla.Crear(pis, new OpcionesEjeDesdeTramos { Nombre = "PI", EstacionInicial = inicial }).Eje;

                cargando = true;
                ElementoHorizontal primero = eje.Horizontal.Elementos[0];
                OrigenX = Texto(primero.Inicio.X);
                OrigenY = Texto(primero.Inicio.Y);
                DireccionX = Texto(Math.Cos(primero.RumboInicio), "0.######");
                DireccionY = Texto(Math.Sin(primero.RumboInicio), "0.######");
                Filas.Clear();
                foreach (DatoElemento f in EjePorElementos.Filas(eje.Horizontal))
                    Filas.Add(new FilaElemento(Actualizar, EjePorElementos.Nombre(f.Tipo), Texto(f.Longitud),
                        f.RadioInicio.HasValue ? Texto(f.RadioInicio.Value) : "", f.RadioFin.HasValue ? Texto(f.RadioFin.Value) : ""));
                cargando = false;
                MensajePi = $"Listo: {pis.Count} PI convertidos en {Filas.Count} elementos. Revíselos en la pestaña Horizontal.";
                Actualizar();
            }
            catch (ArgumentException ex)
            {
                cargando = false;
                MensajePi = ex.Message;
            }
        }

        private void Quitar()
        {
            if (FilaSelecc == null) return;
            int indice = Filas.IndexOf(FilaSelecc);
            Filas.Remove(FilaSelecc);
            FilaSelecc = Filas.Count == 0 ? null : Filas[Math.Min(indice, Filas.Count - 1)];
        }

        private void Mover(int paso)
        {
            if (FilaSelecc == null) return;
            int indice = Filas.IndexOf(FilaSelecc);
            int destino = indice + paso;
            if (destino < 0 || destino >= Filas.Count) return;
            FilaElemento fila = FilaSelecc;
            Filas.Move(indice, destino);
            FilaSelecc = fila;
            SubirBT.NotifyCanExecuteChanged();
            BajarBT.NotifyCanExecuteChanged();
        }

        private void AgregarPlacement()
        {
            double estacion = listo != null ? listo.EstacionFinal : 0;
            if (FilasPlacement.Count == 0 && listo != null) estacion = listo.EstacionInicial;
            string id = $"P{FilasPlacement.Count + 1}";
            FilaPlacement fila = new FilaPlacement(new Placement { Id = id, Estacion = estacion }, Actualizar);
            FilasPlacement.Add(fila);
            PlacementSelecc = fila;
        }

        private string PivSiguiente() => listo == null ? "0+000" : Eje.FormatoEstacion(FilasPiv.Count == 0 ? listo.EstacionInicial : listo.EstacionFinal);
        private string CotaSiguiente() => FilasPiv.Count == 0 ? "0" : FilasPiv[FilasPiv.Count - 1].Cota;

        private void Actualizar()
        {
            if (cargando) return;
            Error = "";
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
            string limpio = Nombre.Trim();
            if (limpio.Length == 0) throw new ArgumentException("Escriba el nombre (ID) del eje.");
            if (existentes.Any(e => string.Equals(e, limpio, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Ya hay un eje llamado \"{limpio}\".");
            if (!LeerNumero(OrigenX, out double x) || !LeerNumero(OrigenY, out double y)) throw new ArgumentException("El origen X, Y debe ser un par de números en metros.");
            if (!LeerNumero(DireccionX, out double dx) || !LeerNumero(DireccionY, out double dy)) throw new ArgumentException("La dirección inicial X, Y debe ser un par de números (por ejemplo 1, 0 = hacia el Este).");
            if (!LeerNumero(EstacionInicial, out double inicial)) throw new ArgumentException("La estación inicial debe ser un número o una estación como 0+000.");
            if (!LeerNumero(MarcasCada, out double marcas) || marcas < 0) throw new ArgumentException("Las marcas de estación deben ser un número en metros (0 = sin marcas).");

            //Elementos: la estacion de cada fila se calcula aunque una fila posterior tenga un error
            List<DatoElemento> datos = new List<DatoElemento>();
            double estacion = inicial;
            string? errorFila = null;
            for (int i = 0; i < Filas.Count; i++)
            {
                FilaElemento f = Filas[i];
                string que = $"Elemento {i + 1} ({f.Tipo})";
                TipoElemento tipo = f.Tipo == "Arco" ? TipoElemento.Arco : f.Tipo == "Clotoide" ? TipoElemento.Clotoide : TipoElemento.Recta;
                if (!LeerNumero(f.Longitud, out double largo) || largo <= 0)
                {
                    errorFila ??= $"{que}: la longitud debe ser un número mayor que cero.";
                    f.Estacion = "";
                    continue;
                }

                double? r0 = LeerOpcional(f.RadioInicio, ref errorFila, que, "radio inicial");
                double? r1 = LeerOpcional(f.RadioFin, ref errorFila, que, "radio final");
                estacion += largo;
                f.Estacion = Eje.FormatoEstacion(estacion);
                datos.Add(new DatoElemento(tipo, largo, tipo == TipoElemento.Recta ? null : r0, tipo == TipoElemento.Clotoide ? r1 : null));
            }

            if (errorFila != null)
            {
                LimpiarVista();
                throw new ArgumentException(errorFila);
            }

            AlineamientoHorizontal horizontal;
            try
            {
                horizontal = EjePorElementos.Crear(new Punto2(x, y), new Punto2(dx, dy), inicial, datos);
            }
            catch (ArgumentException)
            {
                LimpiarVista();
                throw;
            }

            //Rasante: sin PIV = horizontal a cota 0; un PIV = horizontal a esa cota
            List<PuntoVertical> piv = new List<PuntoVertical>();
            foreach (FilaPiv f in FilasPiv)
            {
                if (!LeerNumero(f.Estacion, out double e) || !LeerNumero(f.Cota, out double z))
                    throw new ArgumentException("Cada PIV necesita estación y cota (m).");
                double curva = 0;
                if (!string.IsNullOrWhiteSpace(f.Curva) && (!LeerNumero(f.Curva, out curva) || curva < 0))
                    throw new ArgumentException($"PIV {Eje.FormatoEstacion(e)}: la longitud de curva debe ser un número mayor o igual que cero.");
                if (piv.Any(p => Math.Abs(p.Estacion - e) < 1e-6)) throw new ArgumentException($"Rasante: la estación {Eje.FormatoEstacion(e)} está repetida.");
                piv.Add(new PuntoVertical(e, z, curva));
            }

            PerfilVertical? vertical = piv.Count >= 2 ? new PerfilVertical("Rasante", piv)
                : piv.Count == 1 ? PerfilVertical.Horizontal(horizontal.EstacionInicial, horizontal.EstacionFinal, piv[0].Cota) : null;
            Eje eje = new Eje(limpio, horizontal, vertical) { MarcasCada = marcas };
            if (editado != null) eje.Variables.AddRange(editado.Eje.Variables);

            foreach (FilaPlacement f in FilasPlacement)
            {
                string id = f.Id.Trim();
                if (id.Length == 0) throw new ArgumentException("Cada placement necesita un ID.");
                if (eje.BuscarPlacement(id) != null) throw new ArgumentException($"El placement {id} está repetido.");
                if (!LeerNumero(f.Estacion, out double e) || e < eje.EstacionInicial - 1e-6 || e > eje.EstacionFinal + 1e-6)
                    throw new ArgumentException($"Placement {id}: la estación debe estar entre {Eje.FormatoEstacion(eje.EstacionInicial)} y {Eje.FormatoEstacion(eje.EstacionFinal)}.");
                if (!LeerNumero(f.Rotacion, out double giro)) throw new ArgumentException($"Placement {id}: el giro debe ser un número en grados.");
                eje.Placements.Add(new Placement { Id = id, Estacion = e, Rotacion = giro });
            }

            Dibujar(eje);

            int clotoides = horizontal.Elementos.Count(e => e.Tipo == TipoElemento.Clotoide);
            int arcos = horizontal.Elementos.Count(e => e.Tipo == TipoElemento.Arco);
            Punto2 fin = horizontal.PuntoEn(horizontal.EstacionFinal);
            double rumboFin = horizontal.RumboEn(horizontal.EstacionFinal);
            Resumen = $"Eje \"{eje.Nombre}\": {horizontal.Elementos.Count} elementos ({horizontal.Elementos.Count - clotoides - arcos} rectas, {clotoides} clotoides, {arcos} arcos)\n" +
                      $"Estaciones: {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)}\n" +
                      $"Longitud: {horizontal.Longitud:F3} m\n" +
                      $"Fin: X {fin.X:F3}, Y {fin.Y:F3}, dirección {Math.Cos(rumboFin):F4}, {Math.Sin(rumboFin):F4}\n" +
                      (eje.TieneRasante ? $"Cota: {eje.Vertical.CotaEn(eje.EstacionInicial):F2} a {eje.Vertical.CotaEn(eje.EstacionFinal):F2} m" : "Sin rasante: cota 0") +
                      (eje.Placements.Count > 0 ? $"\nPlacements: {eje.Placements.Count}" : "");

            Descripcion = $"Eje | {eje.Nombre} | {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)} | " +
                          $"tabla de {horizontal.Elementos.Count} elementos" + (eje.TieneRasante ? $" | {piv.Count} PIV" : "");
            MarcasCadaValor = marcas;
            listo = eje;
        }

        private static double? LeerOpcional(string texto, ref string? errorFila, string que, string campo)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Trim() == "-") return null;
            if (LeerNumero(texto, out double valor)) return valor;
            errorFila ??= $"{que}: el {campo} debe ser un número (+ derecha, - izquierda, 0 = recta).";
            return null;
        }

        //Planta a escala uniforme (norte arriba), un color por tipo de elemento; perfil con escala vertical exagerada
        private void Dibujar(Eje eje)
        {
            List<List<PuntoEje>> porElemento = new List<List<PuntoEje>>();
            AlineamientoHorizontal h = eje.Horizontal;
            for (int i = 0; i < h.Elementos.Count; i++)
            {
                double desde = h.EstacionInicioDe(i), largo = h.Elementos[i].Longitud;
                int pasos = h.Elementos[i].Tipo == TipoElemento.Recta ? 1 : 40;
                porElemento.Add(Enumerable.Range(0, pasos + 1).Select(k => eje.Evaluar(desde + largo * k / pasos)).ToList());
            }

            List<PuntoEje> todos = porElemento.SelectMany(p => p).ToList();
            const double margen = 14;
            double minX = todos.Min(m => m.Posicion.X), maxX = todos.Max(m => m.Posicion.X);
            double minY = todos.Min(m => m.Posicion.Y), maxY = todos.Max(m => m.Posicion.Y);
            double escala = Math.Min((AnchoVista - 2 * margen) / Math.Max(maxX - minX, 1e-6), (AltoPlanta - 2 * margen) / Math.Max(maxY - minY, 1e-6));
            double desplX = (AnchoVista - (maxX - minX) * escala) / 2;
            double desplY = (AltoPlanta - (maxY - minY) * escala) / 2;
            Point Planta(Punto3 p) => new Point(desplX + (p.X - minX) * escala, AltoPlanta - desplY - (p.Y - minY) * escala);

            Tramos.Clear();
            for (int i = 0; i < porElemento.Count; i++)
            {
                TipoElemento tipo = h.Elementos[i].Tipo;
                Tramos.Add(new TramoVista
                {
                    Puntos = new PointCollection(porElemento[i].Select(m => Planta(m.Posicion))),
                    Color = tipo == TipoElemento.Recta ? ColorRecta : tipo == TipoElemento.Clotoide ? ColorClotoide : ColorArco
                });
            }

            Marcas.Clear();
            foreach (Placement p in eje.Placements)
            {
                PuntoEje e = eje.Evaluar(p.Estacion);
                Point c = Planta(e.Posicion);
                Punto2 d = PlacementsEje.DireccionPlano(eje, p);
                const double medio = 10;
                Marcas.Add(new MarcaPlanta
                {
                    X1 = c.X + d.X * medio, Y1 = c.Y - d.Y * medio, X2 = c.X - d.X * medio, Y2 = c.Y + d.Y * medio,
                    Texto = p.Id, TextoX = c.X + d.X * medio + 2, TextoY = c.Y - d.Y * medio - 14
                });
            }

            double minE = eje.EstacionInicial, maxE = eje.EstacionFinal;
            double minZ = todos.Min(m => m.Posicion.Z), maxZ = todos.Max(m => m.Posicion.Z);
            double rangoZ = Math.Max(maxZ - minZ, 0.5);
            Perfil = new PointCollection(todos.OrderBy(m => m.Estacion).Select(m => new Point(
                margen + (m.Estacion - minE) / Math.Max(maxE - minE, 1e-6) * (AnchoVista - 2 * margen),
                AltoPerfil - margen - (m.Posicion.Z - minZ) / rangoZ * (AltoPerfil - 2 * margen))));
        }

        private void LimpiarVista()
        {
            Tramos.Clear();
            Marcas.Clear();
            Perfil = new PointCollection();
            Resumen = "";
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
            for (int i = 2; existentes.Any(e => string.Equals(e, libre, StringComparison.OrdinalIgnoreCase)); i++) libre = $"{baseNombre} {i}";
            return libre;
        }

        private static Brush Congelar(SolidColorBrush b)
        {
            b.Freeze();
            return b;
        }

        private static string Texto(double valor, string formato = "0.###") => valor.ToString(formato, CultureInfo.InvariantCulture);
        private static bool LeerNumero(string texto, out double valor) => Puentes.Superestructura.SeccionVariable.LeerNumero(texto, out valor);
    }
}
