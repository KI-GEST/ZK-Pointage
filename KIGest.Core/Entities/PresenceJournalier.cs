using System;

namespace KIGest.Core.Entities;

public class PresenceJournalier
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe? Employe { get; set; }
    public DateTime Date { get; set; }
    public DateTime? Entree { get; set; }
    public DateTime? Sortie { get; set; }
    public double HeuresTravaillees { get; set; }
    public int RetardMin { get; set; }
    public int DepartAnticipeMin { get; set; }
    public double HeuresSup { get; set; }
    public string Statut { get; set; } = "Absent";
}
