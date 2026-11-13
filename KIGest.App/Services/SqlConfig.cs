using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace KIGest.App.Services;

/// <summary>
/// Source unique de la chaîne de connexion SQL Server, avec chiffrement DPAPI « LocalMachine ».
///
/// Ordre de lecture :
///   1. KIGest.json à côté de l'exécutable ; sinon ancien ZKPointage.json (compat clients ayant installé KI-GEST Pointage ≤ 1.1) ; sinon appsettings.json
///      a. Si "ConnectionStrings:DefaultConnectionDpapi" est présent → déchiffré avec la machine locale.
///      b. Si "ConnectionStrings:DefaultConnection" est présent (en clair) → utilisé, puis le fichier
///         est RÉÉCRIT en chiffré DPAPI (DefaultConnection supprimée). Le texte en clair
///         ne persiste donc pas sur disque après le premier démarrage.
/// </summary>
public static class SqlConfig
{
    private const string DpapiKey = "DefaultConnectionDpapi";
    private const string PlainKey = "DefaultConnection";

    private static readonly Lazy<string?> _connectionString = new(ReadAndMigrate);

    public static string? ConnectionString => _connectionString.Value;

    private static string? ReadAndMigrate()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        string kigest = Path.Combine(dir, "KIGest.json");
        string zkpointage = Path.Combine(dir, "ZKPointage.json");   // compatibilité clients existants
        string appsettings = Path.Combine(dir, "appsettings.json");

        string file = File.Exists(kigest) ? kigest : (File.Exists(zkpointage) ? zkpointage : appsettings);

        try
        {
            string json = File.ReadAllText(file);
            var root = JsonNode.Parse(json)?.AsObject();
            var section = root?["ConnectionStrings"]?.AsObject();
            if (section == null) return null;

            //  a. Déjà chiffré (DPAPI LocalMachine)
            string? cipher = section[DpapiKey]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(cipher))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(cipher);
                    byte[] plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.LocalMachine);
                    return Encoding.UTF8.GetString(plain);
                }
                catch
                {
                    return null; // chiffré sur une autre machine : illisible ici (délibéré)
                }
            }

            //  b. Clair présent → utiliser puis migrer en chiffré
            string? clair = section[PlainKey]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(clair))
            {
                try
                {
                    byte[] bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(clair), null, DataProtectionScope.LocalMachine);
                    section[DpapiKey] = Convert.ToBase64String(bytes);
                    section.Remove(PlainKey);

                    // Réécriture atomique (fichier temporaire + remplacement)
                    string tmp = file + ".tmp";
                    File.WriteAllText(tmp, root!.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    File.Replace(tmp, file, null);
                }
                catch (Exception ex)
                {
                    // Fichier non accessible en écriture : on utilise la valeur en clair sans bloquer
                    try { File.AppendAllText(Path.Combine(dir, "ki_startup.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - SqlConfig: migration DPAPI impossible (" + ex.Message + ")" + Environment.NewLine); } catch { }
                }
                return clair;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
