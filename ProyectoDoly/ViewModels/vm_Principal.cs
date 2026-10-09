using Autodesk.Revit.UI.Selection;
using ProyectoDoly.Models;
using ProyectoDoly.Views;

namespace ProyectoDoly.ViewModels
{
    public sealed class vm_Principal : ObservableObject
    {
        //Datos de entrada
        public Document doc;
        public Selection seleccion;

        //Menu
        public List<m_Herramienta> Herramientas { get; }
        private readonly Dictionary<m_Herramienta, vm_Herramienta> creadas = new Dictionary<m_Herramienta, vm_Herramienta>();

        private m_Herramienta herramientaSelecc;
        public m_Herramienta HerramientaSelecc
        {
            get => herramientaSelecc;
            set
            {
                if (!SetProperty(ref herramientaSelecc, value)) return;
                Contenido = value == null ? null : ObtenerHerramienta(value);
            }
        }

        //Viewmodel de la herramienta activa (la vista sale de los DataTemplate de v_Principal)
        private vm_Herramienta contenido;
        public vm_Herramienta Contenido
        {
            get => contenido;
            private set => SetProperty(ref contenido, value);
        }

        //Barra de estado
        private string estado = "Listo.";
        public string Estado
        {
            get => estado;
            set => SetProperty(ref estado, value);
        }

        //Botones
        public RelayCommand CerrarBT { get; }

        //Propiedad:View
        public v_Principal v_Principal { get; set; }

        //Accion de Revit que se ejecuta con la ventana cerrada
        private Action pendiente;

        //Constructor
        public vm_Principal(Document doc, Selection seleccion)
        {
            this.doc = doc;
            this.seleccion = seleccion;

            Herramientas = new List<m_Herramienta>
            {
                //Aqui se agregan las herramientas nuevas (Grupo, Nombre, Descripcion, Crear)
                new m_Herramienta { Grupo = "EXPORTAR", Nombre = "Fichas para grafo",
                    Descripcion = "Exporta una ficha markdown por elemento coordinable.",
                    Crear = () => new vm_ExportarFichas(doc) }
            };

            CerrarBT = new RelayCommand(() => v_Principal.Close());
        }

        //Crea el viewmodel la primera vez y lo conserva para no perder los datos al reabrir
        private vm_Herramienta ObtenerHerramienta(m_Herramienta herramienta)
        {
            if (creadas.TryGetValue(herramienta, out vm_Herramienta vm)) return vm;

            try
            {
                vm = herramienta.Crear();
                vm.Principal = this;
                creadas[herramienta] = vm;
                Estado = "Listo.";
                return vm;
            }
            catch (Exception ex)
            {
                Estado = $"{herramienta.Nombre}: no se pudo abrir. {ex.Message}";
                return null;
            }
        }

        public void Ejecutar(Action accion)
        {
            pendiente = accion;
            v_Principal.Close();
        }

        //Lo llama el comando al cerrarse la ventana. True: hay que volver a abrirla
        public bool EjecutarPendiente()
        {
            if (pendiente == null) return false;

            Action accion = pendiente;
            pendiente = null;

            Estado = $"{HerramientaSelecc?.Nombre}: terminado.";
            try
            {
                accion();
            }
            catch (Exception ex)
            {
                Estado = $"{HerramientaSelecc?.Nombre}: {ex.Message}";
            }
            return true;
        }
    }
}
