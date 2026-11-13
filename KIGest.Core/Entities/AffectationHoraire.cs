using System;

namespace KIGest.Core.Entities;

public class AffectationHoraire
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe? Employe { get; set; }
    public int HoraireId { get; set; }
    public Horaire? Horaire { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
}
