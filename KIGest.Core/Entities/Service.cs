using System.Collections.Generic;

namespace KIGest.Core.Entities;

public class Service
{
    public int Id { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public int? ResponsableId { get; set; }
    public ICollection<Employe> Employes { get; set; } = new List<Employe>();
}
