using System;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace KIGest.App.Services;

/// <summary>
/// Mode « ligne de commande » de l'application : --initdb
/// Crée la base si absente, le login s'il manque, et donne db_owner à l'utilisateur.
/// Aucune dépendance externe (SqlClient embarqué dans .NET 8).
///
///   KIGest.App.exe --initdb --server=SERVEUR --database=POINTAGE --auth=windows
///   KIGest.App.exe --initdb --server=SERVEUR --database=POINTAGE --user=login --password=mdp --auth=sql
///
/// --auth=windows : utilise l'authentification Windows intégrée (compte exécutant le programme).
/// --auth=sql     : utilise les identifiants fournis, avec repli sur l'authentification Windows si refus.
/// Écrit un journal clair dans KIGest.setup.log à côté de l'exécutable.
/// </summary>
public static class DatabaseBootstrapper
{
    public static int Run(string[] args)
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KIGest.setup.log");
        Log(logPath, "=== SQL INITDB ===");

        string server = Option(args, "server") ?? Option(args, "serveur") ?? "localhost";
        string database = Option(args, "database") ?? Option(args, "base") ?? "POINTAGE";
        string user = Option(args, "user") ?? Option(args, "login") ?? "";
        string password = Option(args, "password") ?? Option(args, "motdepasse") ?? "";
        string auth = Option(args, "auth") ?? "sql";

        Log(logPath, $"serveur={server} base={database} auth={auth}" + (!string.IsNullOrWhiteSpace(user) ? $" user={user}" : ""));

        bool ok;
        if (auth.Equals("windows", StringComparison.OrdinalIgnoreCase))
        {
            ok = TryUseConnexion(logPath, server, database, user, password, loginsWindows: true);
        }
        else
        {
            ok = TryUseConnexion(logPath, server, database, user, password, loginsWindows: false)
                 || TryUseConnexion(logPath, server, database, user, password, loginsWindows: true);
        }

        if (!ok)
        {
            string reason = LireRaison(logPath);
            Console.Error.WriteLine("Echec d'initialisation : " + reason);
            Log(logPath, "ÉCHEC : " + reason);
            return 2;
        }

        Log(logPath, "Base POINTAGE prête (ou déjà existante).");
        Console.WriteLine("Base POINTAGE prête.");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--" + name + "=", StringComparison.OrdinalIgnoreCase))
                return arg.Substring(name.Length + 3);
        }
        return null;
    }

    private static bool TryUseConnexion(string logPath, string server, string database, string user, string password, bool loginsWindows)
    {
        if (!loginsWindows && string.IsNullOrWhiteSpace(user))
            return false;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            MultipleActiveResultSets = false,
            TrustServerCertificate = true,
            Encrypt = true,
            ConnectTimeout = 15
        };

        if (loginsWindows)
        {
            builder.IntegratedSecurity = true;   // aucun identifiant à fournir en intégrée
        }
        else
        {
            builder.UserID = user;
            builder.Password = password;
        }

        Log(logPath, loginsWindows ? "Tentative : authentification Windows (intégrée)…" : "Tentative : authentification SQL…");

        try
        {
            builder.InitialCatalog = "master";
            using (var master = new SqlConnection(builder.ConnectionString))
            {
                master.Open();
                Log(logPath, "Connecté au serveur SQL Server.");

                // 1.1. La base existe-t-elle ?
                int dbId = Convert.ToInt32(ExecuteScalar(master,
                    "SELECT ISNULL(DB_ID(N'" + Esc(database) + "'), 0)"));
                if (dbId == 0)
                {
                    Log(logPath, "Création de la base [" + database + "]…");
                    Execute(master, "CREATE DATABASE [" + EscIdent(database) + "]");
                }
                else
                {
                    Log(logPath, "Base [" + database + "] déjà présente.");
                }

                // 1.2. Le login existe-t-il (uniquement si un identifiant est fourni) ?
                if (!string.IsNullOrWhiteSpace(user))
                {
                    int loginId = Convert.ToInt32(ExecuteScalar(master,
                        "SELECT ISNULL((SELECT COUNT(*) FROM sys.server_principals WHERE name = N'" + Esc(user) + "'), 0)"));
                    if (loginId == 0)
                    {
                        Log(logPath, "Création du login [" + user + "]…");
                        Execute(master,
                            "CREATE LOGIN [" + EscIdent(user) + "] WITH PASSWORD = N'" + Esc(password) + "', CHECK_POLICY = OFF");
                    }
                }
            }

            // 1.3. Utilisateur dans la base et rôle db_owner (uniquement si un identifiant est fourni)
            if (string.IsNullOrWhiteSpace(user))
            {
                Log(logPath, "Authentification Windows intégrée : aucun compte SQL à créer côté base.");
                return true;
            }

            builder.InitialCatalog = database;
            using (var db = new SqlConnection(builder.ConnectionString))
            {
                db.Open();

                int userCount = Convert.ToInt32(ExecuteScalar(db,
                    "SELECT ISNULL((SELECT COUNT(*) FROM sys.database_principals WHERE name = N'" + Esc(user) + "'), 0)"));
                if (userCount == 0)
                {
                    Log(logPath, "Création de l'utilisateur [" + user + "] dans la base…");
                    Execute(db,
                        "CREATE USER [" + EscIdent(user) + "] FOR LOGIN [" + EscIdent(user) + "]");
                }

                // db_owner (ignore silencieusement si déjà membre)
                try { Execute(db, "ALTER ROLE db_owner ADD MEMBER [" + EscIdent(user) + "]"); }
                catch (Exception ex) { Log(logPath, "db_owner déjà présent ou non ajoutable : " + ex.Message); }

                Log(logPath, "Utilisateur [" + user + "] affecté à db_owner.");
            }

            return true;
        }
        catch (Exception ex)
        {
            Log(logPath, "Échec : " + ex.Message);
            return false;
        }
    }

    private static string LireRaison(string logPath)
    {
        try
        {
            var lignes = File.ReadAllLines(logPath);
            var erreurs = lignes.Where(l => l.Contains("Échec")).ToList();
            return erreurs.Count > 0 ? erreurs.Last() : "erreur inconnue";
        }
        catch { return "échec de connexion ou de création (voir KIGest.setup.log)"; }
    }

    private static object ExecuteScalar(SqlConnection con, string sql)
    {
        using var cmd = new SqlCommand(sql, con) { CommandTimeout = 30 };
        return cmd.ExecuteScalar() ?? 0;
    }

    private static void Execute(SqlConnection con, string sql)
    {
        using var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 };
        cmd.ExecuteNonQuery();
    }

    private static void Log(string logPath, string message)
    {
        try { File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message + Environment.NewLine); } catch { }
        try { Console.WriteLine(message); } catch { }
    }

    private static string Esc(string s) => s.Replace("'", "''");
    private static string EscIdent(string s) => s.Replace("]", "]]");
}
