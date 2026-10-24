using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EffortLess.App;

/// <summary>Presentation-only lookup; shared brushes remain live across themes.</summary>
public sealed class ActiveLibraryBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        Application.Current.Resources[value is true ? (string)parameter : "PlayerTransparentBrush"];
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
