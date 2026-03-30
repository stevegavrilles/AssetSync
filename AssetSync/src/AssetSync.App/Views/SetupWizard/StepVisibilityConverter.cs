using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AssetSync.App.Views.SetupWizard;

public class StepVisibilityConverter : IValueConverter
{
    public static readonly StepVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string paramStr && int.TryParse(paramStr, out var targetStep))
            return currentStep == targetStep ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
