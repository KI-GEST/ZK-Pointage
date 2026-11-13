using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using KIGest.Core.Services;

namespace KIGest.App.Services;

/// <summary>
/// Couleurs et codes courts des statuts de présence (version claire, pour les cellules de grille).
/// </summary>
public class StatutPresenceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string mode = parameter?.ToString() ?? "Fond";

        if (value is not StatutPresence statut)
        {
            // Valeur absente (jour non calculé) : rendu neutre
            return mode switch
            {
                "Fond" => new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
                "Texte" => new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                "Code" => string.Empty,
                "Libelle" => string.Empty,
                "Icone" => string.Empty,
                _ => Brushes.Transparent
            };
        }

        switch (mode)
        {
            case "Fond":
                return new SolidColorBrush(statut switch
                {
                    StatutPresence.Present => Color.FromRgb(0xDC, 0xFC, 0xE7),
                    StatutPresence.Retard => Color.FromRgb(0xFE, 0xF3, 0xC7),
                    StatutPresence.Absent => Color.FromRgb(0xFE, 0xE2, 0xE2),
                    StatutPresence.Conge => Color.FromRgb(0xDB, 0xEA, 0xFE),
                    StatutPresence.Ferie => Color.FromRgb(0xED, 0xE9, 0xFE),
                    StatutPresence.Repos => Color.FromRgb(0xE2, 0xE8, 0xF0),
                    StatutPresence.PasDePointage => Color.FromRgb(0xF8, 0xFA, 0xFC),
                    _ => Color.FromRgb(0xF1, 0xF5, 0xF9)
                });

            case "Texte":
                return new SolidColorBrush(statut switch
                {
                    StatutPresence.Present => Color.FromRgb(0x15, 0x80, 0x3D),
                    StatutPresence.Retard => Color.FromRgb(0xB4, 0x53, 0x09),
                    StatutPresence.Absent => Color.FromRgb(0xB9, 0x1C, 0x1C),
                    StatutPresence.Conge => Color.FromRgb(0x1D, 0x4E, 0xD8),
                    StatutPresence.Ferie => Color.FromRgb(0x6D, 0x28, 0xD9),
                    StatutPresence.Repos => Color.FromRgb(0x47, 0x55, 0x69),
                    StatutPresence.PasDePointage => Color.FromRgb(0xA8, 0xB5, 0xC7),
                    _ => Color.FromRgb(0x64, 0x74, 0x8B)
                });

            case "Code":
                return statut switch
                {
                    StatutPresence.Present => "P",
                    StatutPresence.Retard => "R",
                    StatutPresence.Absent => "A",
                    StatutPresence.Conge => "C",
                    StatutPresence.Ferie => "F",
                    StatutPresence.Repos => "—",
                    StatutPresence.PasDePointage => "·",
                    _ => string.Empty
                };

            case "Libelle":
                return statut switch
                {
                    StatutPresence.Present => "Présent",
                    StatutPresence.Retard => "Retard",
                    StatutPresence.Absent => "Absent",
                    StatutPresence.Conge => "Congé",
                    StatutPresence.Ferie => "Férié",
                    StatutPresence.Repos => "Repos",
                    StatutPresence.PasDePointage => "Pas de pointage",
                    _ => "Inconnu"
                };

            case "Icone":
                return statut switch
                {
                    StatutPresence.Present => "✔",
                    StatutPresence.Retard => "⏱",
                    StatutPresence.Absent => "✖",
                    StatutPresence.Conge => "✈",
                    StatutPresence.Ferie => "★",
                    StatutPresence.Repos => "—",
                    StatutPresence.PasDePointage => "○",
                    _ => "?"
                };

            default:
                return Brushes.Transparent;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
