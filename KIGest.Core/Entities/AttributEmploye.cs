namespace KIGest.Core.Entities;

public class AttributEmploye
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe? Employe { get; set; }
    public string Cle { get; set; } = string.Empty;
    public string? Valeur { get; set; }
}
