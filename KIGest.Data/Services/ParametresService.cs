using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;

namespace KIGest.Data.Services;

/// <summary>
/// Accès typé aux paramètres stockés en table Parametres.
/// </summary>
public class ParametresService
{
    private readonly KiGestDbContext _context;

    public ParametresService(KiGestDbContext context)
    {
        _context = context;
    }

    public string Get(string cle, string valeurDefaut = "")
    {
        var p = _context.Parametres.FirstOrDefault(x => x.Cle == cle);
        return p?.Valeur ?? valeurDefaut;
    }

    public int GetInt(string cle, int valeurDefaut = 0)
    {
        return int.TryParse(Get(cle), out int v) ? v : valeurDefaut;
    }

    public bool GetBool(string cle, bool valeurDefaut = false)
    {
        var v = Get(cle);
        if (string.IsNullOrWhiteSpace(v)) return valeurDefaut;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1" || v.Equals("vrai", StringComparison.OrdinalIgnoreCase);
    }

    public double GetDouble(string cle, double valeurDefaut = 0)
    {
        return double.TryParse(Get(cle), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : valeurDefaut;
    }

    public TimeSpan GetTime(string cle, TimeSpan valeurDefaut)
    {
        var v = Get(cle);
        return TimeSpan.TryParse(v, CultureInfo.InvariantCulture, out var t) ? t : valeurDefaut;
    }

    public void Set(string cle, string valeur)
    {
        var p = _context.Parametres.FirstOrDefault(x => x.Cle == cle);
        if (p == null)
        {
            _context.Parametres.Add(new Parametre { Cle = cle, Valeur = valeur, DateModification = DateTime.Now });
        }
        else
        {
            p.Valeur = valeur;
            p.DateModification = DateTime.Now;
        }
        _context.SaveChanges();
    }

    public List<Parametre> GetByGroupe(string groupe)
    {
        return _context.Parametres
            .Where(p => p.Groupe == groupe)
            .OrderBy(p => p.Cle)
            .ToList();
    }

    public List<string> GetGroupes()
    {
        return _context.Parametres
            .Select(p => p.Groupe)
            .Distinct()
            .OrderBy(g => g)
            .ToList();
    }
}
