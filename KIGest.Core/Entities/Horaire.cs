using System;

namespace KIGest.Core.Entities;

public class Horaire
{
    public int Id { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public TimeSpan HeureDebut { get; set; }
    public TimeSpan HeureFin { get; set; }
    public TimeSpan? PauseDebut { get; set; }
    public TimeSpan? PauseFin { get; set; }
    public int ToleranceRetardMin { get; set; } = 0;
    public bool NuitOuiNon { get; set; }
}
