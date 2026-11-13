using System;
using System.Globalization;
using System.Windows.Data;

namespace KIGest.App.Services;

/// <summary>
/// Convertit les codes techniques ZKTeco en libellés lisibles.
/// Paramètre : "Mode" (mode de vérification) ou "Sens" (entrée / sortie).
/// </summary>
public class CodeZKTecoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        string type = parameter?.ToString() ?? "Mode";

        if (type == "Sens")
        {
            return value switch
            {
                0 => "Entrée",
                1 => "Sortie",
                2 => "Sortie",
                3 => "Pause",
                4 => "Sortie",
                _ => $"Code {value}"
            };
        }

        return value switch
        {
            0 => "Mot de passe",
            1 => "Empreinte",
            2 => "Badge / carte",
            3 => "Empreinte + code",
            4 => "Empreinte + badge",
            5 => "Empreinte + code + badge",
            6 => "Empreinte + badge + code",
            7 => "Code + badge",
            8 => "Empreinte + code + badge + visage",
            9 => "Code + badge + visage",
            10 => "Empreinte + badge + code + visage",
            11 => "Empreinte + code + badge + visage",
            12 => "Visage",
            13 => "Visage + code",
            14 => "Visage + badge",
            15 => "Visage",
            16 => "Empreinte + visage",
            17 => "Badge + visage",
            18 => "Empreinte + badge + visage",
            19 => "Code + visage",
            20 => "Code + badge + visage",
            21 => "Empreinte + code + visage",
            22 => "Empreinte + code + badge",
            _ => $"Mode {value}"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Convertit un booléen en libellé (ex. état d'import).
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public string Vrai { get; set; } = "Oui";
    public string Faux { get; set; } = "Non";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return b ? Vrai : Faux;
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
