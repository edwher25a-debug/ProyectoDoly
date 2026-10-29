using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.ViewModels
{
    public sealed class vm_EjeDesdeLineas : ObservableObject
    {
        private readonly List<TramoDibujado> tramos;
        private readonly Punto2? cercaDe;
        private readonly List<string> existentes;

        public vm_EjeDesdeLineas(List<TramoDibujado> tramos, Punto2? cercaDe, double cotaSugerida, List<string> existentes)
        {
            this.tramos = tramos;
            this.cercaDe = cercaDe;
            this.existentes = existentes;
            CrearBT = new RelayCommand(Crear, () => PuedeCrear);
            CancelarBT = new RelayCommand(() => Cerrar?.Invoke(false));

            nombre = Libre("Eje dibujado");
            cotaInicial = Texto(cotaSugerida);
            Actualizar();
        }

        public Eje? Resultado { get; private set; }
        public string Descripcion { get; private set; } = "";
        public double MarcasCadaValor { get; private set; } = 20;
        public Action<bool>? Cerrar { get; set; }

        //1_Nombre y estaciones
        private string nombre;
        public string Nombre
        {
            get => nombre;
            set
            {
                if (SetProperty(ref nombre, value ?? "")) Actualizar();
            }
        }

        private string estacionInicial = "0+000";
        public string EstacionInicial
        {
            get => estacionInicial;
            set
            {
                if (SetProperty(ref estacionInicial, value)) Actualizar();
            }
        }

        private bool alReves;
        public bool AlReves
        {
            get => alReves;
            set
            {
                if (SetProperty(ref alReves, value)) Actualizar();
            }
        }

        private string tolerancia = "10";
        public string Tolerancia
        {
            get => tolerancia;
            set
            {
                if (SetProperty(ref tolerancia, value)) Actualizar();
            }
        }

        //2_Rasante
        private bool conPendiente = true;
        public bool ConPendiente
        {
            get => conPendiente;
            set
            {
                if (!SetProperty(ref conPendiente, value)) return;
                OnPropertyChanged(nameof(ConTabla));
                Actualizar();
            }
        }

        public bool ConTabla
        {
            get => !conPendiente;
            set => ConPendiente = !value;
        }

        private string cotaInicial;
        public string CotaInicial
        {
            get => cotaInicial;
            set
            {
                if (SetProperty(ref cotaInicial, value)) Actualizar();
            }
        }

        private string pendiente = "0";
        public string Pendiente
        {
            get => pendiente;
            set
            {
                if (SetProperty(ref pendiente, value)) Actualizar();
            }
        }

        private string tablaPiv = "";
        public string TablaPiv
        {
            get => tablaPiv;
            set
            {
                if (SetProperty(ref tablaPiv, value)) Actualizar();
            }
        }

        //3_Dibujo
        private string marcasCada = "20";
        public string MarcasCada
        {
            get => marcasCada;
            set
            {
                if (SetProperty(ref marcasCada, value)) Actualizar();
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
            string limpio = Nombre.Trim();
            if (limpio.Length == 0) throw new ArgumentException("Escriba el nombre del eje.");
            if (existentes.Any(e => string.Equals(e, limpio, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Ya hay un eje llamado \"{limpio}\".");
            if (!LeerNumero(EstacionInicial, out double inicial)) throw new ArgumentException("La estación inicial debe ser un número o una estación como 0+000.");
            if (!LeerNumero(Tolerancia, out double mm) || mm <= 0) throw new ArgumentException("La tolerancia de unión debe ser un número mayor que cero, en milímetros.");
            if (!LeerNumero(MarcasCada, out double marcas) || marcas < 0) throw new ArgumentException("Las marcas de estación deben ser un número en metros (0 = sin marcas).");

            OpcionesEjeDesdeTramos opciones = new OpcionesEjeDesdeTramos { Nombre = limpio, EstacionInicial = inicial, Tolerancia = mm / 1000, CercaDe = cercaDe };
            if (ConPendiente)
            {
                if (!LeerNumero(CotaInicial, out double cota) || !LeerNumero(Pendiente, out double p))
                    throw new ArgumentException("La cota inicial (m) y la pendiente (%) deben ser números.");
                opciones.CotaInicial = cota;
                opciones.PendientePorciento = p;
            }
            else
            {
                opciones.Piv.AddRange(EjeDesdeTramos.LeerPiv(TablaPiv));
                if (opciones.Piv.Count < 2) throw new ArgumentException("Escriba al menos dos PIV en la tabla: estación = cota (y / longitud de curva si la tiene).");
            }

            //Al reves: se empieza por el otro extremo libre de la cadena
            if (AlReves)
            {
                List<TramoDibujado> cadena = EjeDesdeTramos.Encadenar(tramos, opciones.Tolerancia, cercaDe);
                opciones.CercaDe = cadena[cadena.Count - 1].Fin;
            }

            ResultadoEjeDesdeTramos r = EjeDesdeTramos.Crear(tramos, opciones);

            Eje eje = r.Eje;
            if (!ConPendiente && (opciones.Piv[0].Estacion > eje.EstacionInicial + 1e-3 || opciones.Piv[opciones.Piv.Count - 1].Estacion < eje.EstacionFinal - 1e-3))
                throw new ArgumentException($"La tabla de rasante debe cubrir todo el eje, de {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)}.");

            Resumen = $"Eje \"{eje.Nombre}\"\n" +
                      $"{r.Rectas} rectas y {r.Arcos} arcos encadenados\n" +
                      $"Estaciones: {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)}\n" +
                      $"Longitud: {eje.Horizontal.Longitud:F3} m\n" +
                      $"Cota: {eje.Vertical.CotaEn(eje.EstacionInicial):F2} m al inicio, {eje.Vertical.CotaEn(eje.EstacionFinal):F2} m al final" +
                      (r.Quiebres.Count > 0
                          ? "\n\nQuiebres (las piezas no son tangentes):\n" + string.Join("\n", r.Quiebres.Take(6).Select(q => $"{Eje.FormatoEstacion(q.estacion)}: {q.grados:0.##}°")) +
                            (r.Quiebres.Count > 6 ? $"\n(y {r.Quiebres.Count - 6} más)" : "")
                          : "\n\nTodas las uniones son tangentes.");

            Descripcion = $"Eje | {eje.Nombre} | {Eje.FormatoEstacion(eje.EstacionInicial)} a {Eje.FormatoEstacion(eje.EstacionFinal)} | " +
                          $"dibujado desde {tramos.Count} líneas | " +
                          (ConPendiente ? $"cota {Texto(opciones.CotaInicial)} m, pendiente {Texto(opciones.PendientePorciento)} %" : $"{opciones.Piv.Count} PIV");
            MarcasCadaValor = marcas;
            listo = eje;
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

        private static string Texto(double valor) => valor.ToString("0.###", CultureInfo.InvariantCulture);
        private static bool LeerNumero(string texto, out double valor) => Puentes.Superestructura.SeccionVariable.LeerNumero(texto, out valor);
    }
}
