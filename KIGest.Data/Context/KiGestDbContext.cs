using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;

namespace KIGest.Data.Context;

public class KiGestDbContext : DbContext
{
    public KiGestDbContext(DbContextOptions<KiGestDbContext> options) : base(options)
    {
    }

    public DbSet<Employe> Employes => Set<Employe>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Pointeuse> Pointeuses => Set<Pointeuse>();
    public DbSet<PointageBrut> PointagesBruts => Set<PointageBrut>();
    public DbSet<Horaire> Horaires => Set<Horaire>();
    public DbSet<AffectationHoraire> AffectationsHoraires => Set<AffectationHoraire>();
    public DbSet<AffectationServiceHoraire> AffectationsServiceHoraires => Set<AffectationServiceHoraire>();
    public DbSet<WorkCode> WorkCodes => Set<WorkCode>();
    public DbSet<AttributEmploye> AttributsEmployes => Set<AttributEmploye>();
    public DbSet<MessageAppareil> MessagesAppareil => Set<MessageAppareil>();
    public DbSet<JourFerie> JoursFeries => Set<JourFerie>();
    public DbSet<Conge> Conges => Set<Conge>();
    public DbSet<PresenceJournalier> PresencesJournalieres => Set<PresenceJournalier>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Journal> Journaux => Set<Journal>();
    public DbSet<CorrectionPointage> CorrectionsPointages => Set<CorrectionPointage>();
    public DbSet<Parametre> Parametres => Set<Parametre>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PointageBrut>()
            .HasIndex(p => new { p.PointeuseId, p.BadgeNumero, p.DateHeure })
            .IsUnique();

        modelBuilder.Entity<Parametre>()
            .HasIndex(p => p.Cle)
            .IsUnique();

        modelBuilder.Entity<CorrectionPointage>()
            .HasIndex(c => new { c.EmployeId, c.Date })
            .IsUnique();

        modelBuilder.Entity<AffectationServiceHoraire>()
            .HasIndex(a => new { a.ServiceId, a.HoraireId, a.DateDebut });

        modelBuilder.Entity<Pointeuse>()
            .HasIndex(p => p.AdresseIP)
            .IsUnique();
    }
}
