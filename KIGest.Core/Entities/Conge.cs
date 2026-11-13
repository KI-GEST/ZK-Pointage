using System;

namespace KIGest.Core.Entities;

public class Conge
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe? Employe { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public string Statut { get; set; } = "EnAttente";
    public string? Motif { get; set; }
}
