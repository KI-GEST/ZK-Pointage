namespace KIGest.Core.Entities;

public class WorkCode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool Actif { get; set; } = true;
}
