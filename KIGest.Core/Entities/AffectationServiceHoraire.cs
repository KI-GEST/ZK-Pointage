using System;

namespace KIGest.Core.Entities;

public class AffectationServiceHoraire
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service? Service { get; set; }
    public int HoraireId { get; set; }
    public Horaire? Horaire { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
}
