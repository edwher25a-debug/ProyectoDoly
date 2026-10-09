using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ProyectoDoly.Puentes;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Superestructura;
using Point = System.Windows.Point;

namespace ProyectoDoly.ViewModels
{
    //Opcion del combo de secciones: un tipo de familia
    public sealed class OpcionSeccion
    {
        public FamilySymbol Tipo { get; set; } = null!;
        public string Nombre => $"{Tipo.Family.Name} : {Tipo.Name}";
        public override string ToString() => Nombre;
    }

    //Poligono de la vista previa de la seccion
    public sealed class FormaSeccion
    {
        public PointCollection Puntos { get; set; } = new PointCollection();
        public bool EsHueco { get; set; }
    }

    public sealed class vm_Superestructura : ObservableObject
    {
        //Tamaño de la vista previa
        public const double AnchoVista = 340;
        public const double AltoVista = 200;

        private readonly Document doc;
        private readonly Dictionary<ElementId, SeccionTransversal> secciones = new Dictionary<ElementId, SeccionTransversal>();

        public vm_Superestructura(Document doc, List<EjeGuardado> ejes, ElementId? ejePreseleccionado)
        {
            this.doc = doc;
            CargarFamiliaBT = new RelayCommand(CargarFamilia);
            CrearBT = new RelayCommand(Crear, () => PuedeCrear);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            foreach (EjeGuardado eje in ejes) Ejes.Add(eje);
            CargarTipos(null);

            EjeSelecc = Ejes.FirstOrDefault(e => e.Id == ejePreseleccionado) ?? Ejes.FirstOrDefault();
        }

        //Resultado para el comando (null si se cancela)
        public OpcionesSuperestructura? Opciones { get; private set; }
        public Action<bool>? Cerrar { get; set; }

        //1_Eje
        public ObservableCollection<EjeGuardado> Ejes { get; } = new ObservableCollection<EjeGuardado>();

        private EjeGuardado? ejeSelecc;
        public EjeGuardado? EjeSelecc
        {
            get => ejeSelecc;
            set
            {
                if (!SetProperty(ref ejeSelecc, value)) return;

                //Por defecto, todo el eje
                estacionInicial = value != null ? Texto(value.Eje.EstacionInicial) : "";
                estacionFinal = value != null ? Texto(value.Eje.EstacionFinal) : "";
                OnPropertyChanged(nameof(EstacionInicial));
                OnPropertyChanged(nameof(EstacionFinal));
                Actualizar();
            }
        }

        //2_Seccion
        public ObservableCollection<OpcionSeccion> Tipos { get; } = new ObservableCollection<OpcionSeccion>();

        private OpcionSeccion? tipoSelecc;
        public OpcionSeccion? TipoSelecc
        {
            get => tipoSelecc;
            set
            {
                if (SetProperty(ref tipoSelecc, value)) Actualizar();
            }
        }

        private bool espejo;
        public bool Espejo
        {
            get => espejo;
            set
            {
                if (SetProperty(ref espejo, value)) Actualizar();
            }
        }

        //3_Tramo y ubicacion de la seccion
        private string estacionInicial = "";
        public string EstacionInicial
        {
            get => estacionInicial;
            set
            {
                if (SetProperty(ref estacionInicial, value)) Actualizar();
            }
        }

        private string estacionFinal = "";
        public string EstacionFinal
        {
            get => estacionFinal;
            set
            {
                if (SetProperty(ref estacionFinal, value)) Actualizar();
            }
        }

        private string paso = "1";
        public string Paso
        {
            get => paso;
            set
            {
                if (SetProperty(ref paso, value)) Actualizar();
            }
        }

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

        //Vista previa
        public ObservableCollection<FormaSeccion> Formas { get; } = new ObservableCollection<FormaSeccion>();

        private Point origen = new Point(-100, -100);
        public double OrigenX => origen.X;
        public double OrigenY => origen.Y;

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

        private OpcionesSuperestructura? listo;
        public bool PuedeCrear => listo != null;
        public bool SinEjes => Ejes.Count == 0;

        //Botones
        public RelayCommand CargarFamiliaBT { get; }
        public RelayCommand CrearBT { get; }
        public RelayCommand CancelarBT { get; }

        private void CargarTipos(ElementId? familia)
        {
            Tipos.Clear();
            foreach (FamilySymbol tipo in SeccionFamilia.Tipos(doc)) Tipos.Add(new OpcionSeccion { Tipo = tipo });

            //Tras cargar una familia se elige su primer tipo; si no, una que parezca seccion de tablero
            tipoSelecc = (familia != null ? Tipos.FirstOrDefault(t => t.Tipo.Family.Id == familia) : null)
                         ?? Tipos.FirstOrDefault(t => PareceSeccion(t.Nombre))
                         ?? Tipos.FirstOrDefault();
            OnPropertyChanged(nameof(TipoSelecc));
        }

        private void CargarFamilia()
        {
            OpenFileDialog dialogo = new OpenFileDialog
            {
                Title = "Familia de sección transversal",
                Filter = "Familias de Revit (*.rfa)|*.rfa"
            };
            if (dialogo.ShowDialog() != true) return;

            try
            {
                Family familia = SeccionFamilia.Cargar(doc, dialogo.FileName);

                //La familia pudo cambiar: se vuelven a leer sus tipos
                foreach (ElementId id in familia.GetFamilySymbolIds()) secciones.Remove(id);
                CargarTipos(familia.Id);
                Actualizar();
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Autodesk.Revit.Exceptions.ApplicationException)
            {
                Error = $"No se pudo cargar la familia: {ex.Message}";
            }
        }

        //Recalcula seccion, vista previa y resumen
        private void Actualizar()
        {
            Error = "";
            listo = null;
            Formas.Clear();
            origen = new Point(-100, -100);

            SeccionTransversal? seccion = LeerSeccion();
            if (seccion != null)
            {
                if (Espejo) seccion = seccion.Espejo();
                Dibujar(seccion);
            }

            OnPropertyChanged(nameof(OrigenX));
            OnPropertyChanged(nameof(OrigenY));

            if (EjeSelecc == null)
                MostrarError("Importe primero un eje con el botón Importar eje.");
            else if (seccion != null)
                Validar(EjeSelecc, seccion);

            CrearBT.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeCrear));
        }

        private SeccionTransversal? LeerSeccion()
        {
            Resumen = "";
            if (TipoSelecc == null)
            {
                MostrarError("Cargue una familia de sección (Modelo genérico o Perfil).");
                return null;
            }

            //Leer una familia la abre en segundo plano: se guarda lo leido por tipo
            if (!secciones.TryGetValue(TipoSelecc.Tipo.Id, out SeccionTransversal? seccion))
            {
                try
                {
                    seccion = SeccionFamilia.Leer(doc, TipoSelecc.Tipo);
                    secciones[TipoSelecc.Tipo.Id] = seccion;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is Autodesk.Revit.Exceptions.ApplicationException)
                {
                    MostrarError($"No se pudo leer la sección: {ex.Message}");
                    return null;
                }
            }

            Resumen = $"Ancho: {seccion.MaxX - seccion.MinX:F3} m  ·  Canto: {seccion.MaxY - seccion.MinY:F3} m\n" +
                      $"Área: {seccion.Area:F3} m²  ·  Piezas: {seccion.Piezas.Count}" +
                      (seccion.Piezas.Any(p => p.Huecos.Count > 0) ? $", huecos: {seccion.Piezas.Sum(p => p.Huecos.Count)}" : "");
            return seccion;
        }

        private void Validar(EjeGuardado eje, SeccionTransversal seccion)
        {
            if (!LeerNumero(EstacionInicial, out double inicial) || !LeerNumero(EstacionFinal, out double final))
            {
                MostrarError("Las estaciones deben ser números en metros (por ejemplo 1234.5).");
                return;
            }

            if (inicial < eje.Eje.EstacionInicial - 1e-6 || final > eje.Eje.EstacionFinal + 1e-6 || final - inicial < 0.01)
            {
                MostrarError($"El tramo debe estar entre {Eje.FormatoEstacion(eje.Eje.EstacionInicial)} y {Eje.FormatoEstacion(eje.Eje.EstacionFinal)}, con la estación final mayor.");
                return;
            }

            if (!LeerNumero(Paso, out double pasoValor) || pasoValor < 0.05)
            {
                MostrarError("El paso entre secciones debe ser un número de al menos 0.05 m.");
                return;
            }

            if (!LeerNumero(DesplazamientoLateral, out double lateral) || !LeerNumero(DesplazamientoVertical, out double vertical))
            {
                MostrarError("Los desplazamientos deben ser números en metros (0 = sin desplazar).");
                return;
            }

            double longitud = final - inicial;
            Resumen += $"\n\nTramo: {longitud:F2} m  ·  Volumen ≈ {seccion.Area * longitud:F1} m³\n" +
                       $"Secciones: {(int)Math.Ceiling(longitud / pasoValor) + 1} o más (se añaden en cada cambio de tramo)";

            listo = new OpcionesSuperestructura
            {
                Eje = eje,
                Seccion = seccion,
                Barrido = new OpcionesBarrido
                {
                    EstacionInicial = inicial,
                    EstacionFinal = final,
                    Paso = pasoValor,
                    DesplazamientoLateral = lateral,
                    DesplazamientoVertical = vertical
                }
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

        //Seccion a escala uniforme, con margen y el punto del eje marcado
        private void Dibujar(SeccionTransversal seccion)
        {
            const double margen = 16;
            double minX = Math.Min(seccion.MinX, 0), maxX = Math.Max(seccion.MaxX, 0);
            double minY = Math.Min(seccion.MinY, 0), maxY = Math.Max(seccion.MaxY, 0);
            double escala = Math.Min((AnchoVista - 2 * margen) / Math.Max(maxX - minX, 1e-6),
                (AltoVista - 2 * margen) / Math.Max(maxY - minY, 1e-6));
            double desplX = (AnchoVista - (maxX - minX) * escala) / 2;
            double desplY = (AltoVista - (maxY - minY) * escala) / 2;

            Point Vista(Punto2 p) => new Point(desplX + (p.X - minX) * escala, AltoVista - desplY - (p.Y - minY) * escala);

            PointCollection Puntos(Contorno c)
            {
                PointCollection puntos = new PointCollection(c.Puntos.Select(Vista));
                puntos.Freeze();
                return puntos;
            }

            //Primero los macizos y despues los huecos, que se pintan encima con el color de fondo
            foreach (Pieza pieza in seccion.Piezas) Formas.Add(new FormaSeccion { Puntos = Puntos(pieza.Exterior) });
            foreach (Contorno hueco in seccion.Piezas.SelectMany(p => p.Huecos)) Formas.Add(new FormaSeccion { Puntos = Puntos(hueco), EsHueco = true });

            origen = Vista(new Punto2(0, 0));
        }

        private static bool PareceSeccion(string nombre) =>
            new[] { "SECTION", "SECCION", "SECCIÓN", "TABLERO", "DECK" }.Any(p => nombre.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        private static string Texto(double valor) => valor.ToString("0.###", CultureInfo.InvariantCulture);

        //Acepta coma o punto decimal y el formato vial 1+234.5
        private static bool LeerNumero(string texto, out double valor)
        {
            valor = 0;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            string limpio = texto.Trim().Replace(',', '.');
            int mas = limpio.IndexOf('+');
            if (mas > 0 && double.TryParse(limpio.Substring(0, mas), NumberStyles.Float, CultureInfo.InvariantCulture, out double km)
                        && double.TryParse(limpio.Substring(mas + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double metros))
            {
                valor = km * 1000 + metros;
                return true;
            }

            return double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }
    }
}
