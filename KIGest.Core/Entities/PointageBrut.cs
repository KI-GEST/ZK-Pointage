using System;

namespace KIGest.Core.Entities;

public class PointageBrut
{
    public int Id { get; set; }
    public int PointeuseId { get; set; }
    public Pointeuse? Pointeuse { get; set; }
    public string BadgeNumero { get; set; } = string.Empty;
    public DateTime DateHeure { get; set; }
    public int ModeVerification { get; set; }
    public int SensES { get; set; }
    public bool Importe { get; set; }
}
