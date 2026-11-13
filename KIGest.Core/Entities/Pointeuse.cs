using System;

namespace KIGest.Core.Entities;

public class Pointeuse
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? Modele { get; set; }
    public string AdresseIP { get; set; } = string.Empty;
    public int Port { get; set; } = 4370;
    public int CleCom { get; set; } = 0;
    public int NumeroMachine { get; set; } = 1;
    public string? Emplacement { get; set; }
    public DateTime? DerniereSynchro { get; set; }
    public bool Actif { get; set; } = true;
}
