namespace KIGest.Core.Entities;

public class MessageAppareil
{
    public int Id { get; set; }
    public string Titre { get; set; } = string.Empty;
    public string Contenu { get; set; } = string.Empty;
    public bool EstPublic { get; set; } = true;
    public int? EmployeId { get; set; }
    public Employe? Employe { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; } = DateTime.Now;
}
