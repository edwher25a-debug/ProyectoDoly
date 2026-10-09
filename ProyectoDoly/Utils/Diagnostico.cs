using Autodesk.Revit.UI;

namespace ProyectoDoly.Utils
{
    /// <summary>
    ///     Muestra los errores inesperados con su detalle (en vez del "error interno" generico de Revit)
    ///     y los guarda en %TEMP%\ProyectoDoly_error.txt para poder enviarlos.
    /// </summary>
    public static class Diagnostico
    {
        public static void MostrarError(string herramienta, string paso, Exception ex)
        {
            string archivo = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProyectoDoly_error.txt");
            try
            {
                System.IO.File.WriteAllText(archivo, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{herramienta} | {paso}\n\n{ex}");
            }
            catch (Exception escritura) when (escritura is System.IO.IOException || escritura is UnauthorizedAccessException)
            {
                archivo = "(no se pudo guardar)";
            }

            TaskDialog dialogo = new TaskDialog(herramienta)
            {
                MainInstruction = $"Falló: {paso}",
                MainContent = $"{ex.GetType().Name}: {ex.Message}\n\nDetalle guardado en:\n{archivo}",
                ExpandedContent = ex.ToString(),
                MainIcon = TaskDialogIcon.TaskDialogIconError
            };
            dialogo.Show();
        }
    }
}
