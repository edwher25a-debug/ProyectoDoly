using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.LandXml;
using Microsoft.Win32;
using ProyectoDoly.Puentes;
using Point = System.Windows.Point;

namespace ProyectoDoly.ViewModels
{
    //Opcion del combo de rasantes (Rasante = null: eje a cota 0)
    public sealed class OpcionRasante
    {
        public string Nombre { get; set; } = "";
        public PerfilVertical? Rasante { get; set; }
        public override string ToString() => Nombre;
    }

    public sealed class vm_ImportarEje : ObservableObject
    {
        //Tamaño de las vistas previas en la ventana
        public const double AnchoVista = 340;
        public const double AltoPlanta = 190;
        public const double AltoPerfil = 90;

        public vm_ImportarEje()
        {
            BuscarArchivoBT = new RelayCommand(BuscarArchivo);
            ImportarBT = new RelayCommand(Importar, () => PuedeImportar);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));
        }

        //Resultado para el comando (null si se cancela)
        public OpcionesEje? Opciones { get; private set; }
        public Action<bool>? Cerrar { get; set; }

        //1_Archivo
        private string rutaArchivo = "";
        public string RutaArchivo
        {
            get => rutaArchivo;
            private set => SetProperty(ref rutaArchivo, value);
        }

        //2_Alineamiento y rasante
        public ObservableCollection<AlineamientoLandXml> Alineamientos { get; } = new ObservableCollection<AlineamientoLandXml>();
        public ObservableCollection<OpcionRasante> Rasantes { get; } = new ObservableCollection<OpcionRasante>();

        private AlineamientoLandXml? alineamientoSelecc;
        public AlineamientoLandXml? AlineamientoSelecc
        {
            get => alineamientoSelecc;
            set
            {
                if (!SetProperty(ref alineamientoSelecc, value)) return;
                CargarRasantes();
            }
        }

        private OpcionRasante? rasanteSelecc;
        public OpcionRasante? RasanteSelecc
        {
            get => rasanteSelecc;
            set
            {
                if (SetProperty(ref rasanteSelecc, value)) Actualizar();
            }
        }

        //3_Ubicacion y dibujo
        private bool ubicarCompartidas = true;
        public bool UbicarCompartidas
        {
            get => ubicarCompartidas;
            set
            {
                if (SetProperty(ref ubicarCompartidas, value)) OnPropertyChanged(nameof(UbicarOrigen));
            }
        }

        public bool UbicarOrigen
        {
            get => !ubicarCompartidas;
            set => UbicarCompartidas = !value;
        }

        private string paso = "2";
        public string Paso
        {
            get => paso;
            set
            {
                if (SetProperty(ref paso, value)) Actualizar();
            }
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

        //Vista previa y resumen
        private PointCollection planta = new PointCollection();
        public PointCollection Planta
        {
            get => planta;
            private set => SetProperty(ref planta, value);
        }

        private PointCollection perfil = new PointCollection();
        public PointCollection Perfil
        {
            get => perfil;
            private set => SetProperty(ref perfil, value);
        }

        private string resumen = "Seleccione un archivo LandXML para ver el eje.";
        public string Resumen
        {
            get => resumen;
            private set => SetProperty(ref resumen, value);
        }

        private string textoPerfil = "";
        public string TextoPerfil
        {
            get => textoPerfil;
            private set => SetProperty(ref textoPerfil, value);
        }

        public ObservableCollection<string> Avisos { get; } = new ObservableCollection<string>();

        private string error = "";
        public string Error
        {
            get => error;
            private set => SetProperty(ref error, value);
        }

        private Eje? ejeActual;
        public bool PuedeImportar => ejeActual != null && string.IsNullOrEmpty(Error);

        //Botones
        public RelayCommand BuscarArchivoBT { get; }
        public RelayCommand ImportarBT { get; }
        public RelayCommand CancelarBT { get; }

        private void BuscarArchivo()
        {
            OpenFileDialog dialogo = new OpenFileDialog
            {
                Title = "LandXML exportado desde Civil 3D",
                Filter = "LandXML (*.xml)|*.xml|Todos los archivos (*.*)|*.*"
            };
            if (dialogo.ShowDialog() != true) return;

            CargarArchivo(dialogo.FileName);
        }

        public void CargarArchivo(string ruta)
        {
            RutaArchivo = ruta;
            Alineamientos.Clear();

            try
            {
                ArchivoLandXml archivo = LectorLandXml.Leer(ruta);
                foreach (AlineamientoLandXml alineamiento in archivo.Alineamientos) Alineamientos.Add(alineamiento);
                AlineamientoSelecc = Alineamientos.FirstOrDefault();
            }
            catch (Exception ex) when (ex is FormatException || ex is System.Xml.XmlException || ex is ArgumentException || ex is IOException)
            {
                AlineamientoSelecc = null;
                MostrarError($"No se pudo leer el archivo: {ex.Message}");
            }
        }

        private void CargarRasantes()
        {
            Rasantes.Clear();
            if (AlineamientoSelecc != null)
            {
                foreach (PerfilVertical rasante in AlineamientoSelecc.Rasantes)
                    Rasantes.Add(new OpcionRasante { Nombre = rasante.Nombre, Rasante = rasante });
                Rasantes.Add(new OpcionRasante { Nombre = "Sin rasante (eje a cota 0)" });
            }

            //La primera rasante del archivo suele ser la de proyecto
            rasanteSelecc = Rasantes.FirstOrDefault();
            OnPropertyChanged(nameof(RasanteSelecc));
            Actualizar();
        }

        //Recalcula eje, vistas previas y resumen
        private void Actualizar()
        {
            Error = "";
            Avisos.Clear();
            ejeActual = null;

            if (AlineamientoSelecc == null)
            {
                Planta = new PointCollection();
                Perfil = new PointCollection();
                Resumen = "Seleccione un archivo LandXML para ver el eje.";
                TextoPerfil = "";
                ImportarBT.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(PuedeImportar));
                return;
            }

            foreach (string aviso in AlineamientoSelecc.Avisos) Avisos.Add(aviso);

            Eje eje = AlineamientoSelecc.CrearEje(RasanteSelecc?.Rasante);
            List<PuntoEje> muestras = eje.Muestrear(Math.Max(eje.Horizontal.Longitud / 400, 0.5));
            Planta = DibujarPlanta(muestras);
            Perfil = DibujarPerfil(muestras);

            ElementosPorTipo(eje, out int rectas, out int arcos, out int clotoides);
            Resumen = $"{Eje.FormatoEstacion(eje.EstacionInicial)}  a  {Eje.FormatoEstacion(eje.EstacionFinal)}\n" +
                      $"Longitud: {eje.Horizontal.Longitud:F3} m\n" +
                      $"Tramos: {rectas} rectas, {arcos} curvas, {clotoides} clotoides";

            double cotaMin = muestras.Min(m => m.Posicion.Z);
            double cotaMax = muestras.Max(m => m.Posicion.Z);
            TextoPerfil = eje.TieneRasante
                ? $"Rasante: cota {cotaMin:F2} a {cotaMax:F2} m · {eje.Vertical.Puntos.Count} PIV"
                : "Sin rasante: el eje se dibuja a cota 0";

            if (!LeerNumero(Paso, out double pasoValor) || pasoValor <= 0)
                MostrarError("El paso de dibujo debe ser un número mayor que 0.");
            else if (!LeerNumero(MarcasCada, out double marcasValor) || marcasValor < 0)
                MostrarError("Las marcas de estación deben ser un número (0 = sin marcas).");
            else
                ejeActual = eje;

            ImportarBT.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeImportar));
        }

        private void Importar()
        {
            if (ejeActual == null) return;

            LeerNumero(Paso, out double pasoValor);
            LeerNumero(MarcasCada, out double marcasValor);
            Opciones = new OpcionesEje
            {
                Eje = ejeActual,
                Ubicacion = UbicarCompartidas ? Ubicacion.Compartidas : Ubicacion.Origen,
                Paso = pasoValor,
                MarcasCada = marcasValor,
                Archivo = RutaArchivo
            };
            Cerrar?.Invoke(true);
        }

        private void MostrarError(string texto)
        {
            Error = texto;
            ImportarBT.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeImportar));
        }

        //Acepta coma o punto decimal
        private static bool LeerNumero(string texto, out double valor) =>
            double.TryParse(texto?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);

        private static void ElementosPorTipo(Eje eje, out int rectas, out int arcos, out int clotoides)
        {
            rectas = eje.Horizontal.Elementos.Count(e => e.Tipo == TipoElemento.Recta);
            arcos = eje.Horizontal.Elementos.Count(e => e.Tipo == TipoElemento.Arco);
            clotoides = eje.Horizontal.Elementos.Count(e => e.Tipo == TipoElemento.Clotoide);
        }

        //Planta a escala uniforme, con el Norte hacia arriba
        private static PointCollection DibujarPlanta(List<PuntoEje> muestras)
        {
            const double margen = 10;
            double minX = muestras.Min(m => m.Posicion.X), maxX = muestras.Max(m => m.Posicion.X);
            double minY = muestras.Min(m => m.Posicion.Y), maxY = muestras.Max(m => m.Posicion.Y);
            double escala = Math.Min((AnchoVista - 2 * margen) / Math.Max(maxX - minX, 1e-6),
                (AltoPlanta - 2 * margen) / Math.Max(maxY - minY, 1e-6));
            double desplX = (AnchoVista - (maxX - minX) * escala) / 2;
            double desplY = (AltoPlanta - (maxY - minY) * escala) / 2;

            PointCollection puntos = new PointCollection(muestras.Select(m => new Point(
                desplX + (m.Posicion.X - minX) * escala,
                AltoPlanta - desplY - (m.Posicion.Y - minY) * escala)));
            puntos.Freeze();
            return puntos;
        }

        //Perfil con escala vertical exagerada para que se vea la rasante
        private static PointCollection DibujarPerfil(List<PuntoEje> muestras)
        {
            const double margen = 8;
            double minE = muestras.First().Estacion, maxE = muestras.Last().Estacion;
            double minZ = muestras.Min(m => m.Posicion.Z), maxZ = muestras.Max(m => m.Posicion.Z);
            double rangoZ = Math.Max(maxZ - minZ, 1);

            PointCollection puntos = new PointCollection(muestras.Select(m => new Point(
                margen + (m.Estacion - minE) / Math.Max(maxE - minE, 1e-6) * (AnchoVista - 2 * margen),
                AltoPerfil - margen - (m.Posicion.Z - minZ) / rangoZ * (AltoPerfil - 2 * margen))));
            puntos.Freeze();
            return puntos;
        }
    }
}
