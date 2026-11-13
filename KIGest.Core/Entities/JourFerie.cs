using System;

namespace KIGest.Core.Entities;

public class JourFerie
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Libelle { get; set; } = string.Empty;
}
