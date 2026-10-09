using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Visibility = System.Windows.Visibility;

namespace ProyectoDoly.Views
{
    //Convertidores de visibilidad usados con x:Static desde XAML
    public static class Visible
    {
        //Muestra el control solo si el numero recibido (por ejemplo Count) es mayor que cero
        public static readonly IValueConverter SiHayElementos = new SiHayElementosConverter();

        private sealed class SiHayElementosConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
                value is int cantidad && cantidad > 0 ? Visibility.Visible : Visibility.Collapsed;

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
                throw new NotSupportedException();
        }
    }
}
