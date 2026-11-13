using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace KIGest.Device.Services;

public record AttLog(string UserId, DateTime Time, int VerifyMode, int InOutMode);

/// <summary>Utilisateur enregistré sur la pointeuse.</summary>
public record ZkUser(int EnrollNumber, string Name, string UserId, string Card, int Privilege, string GroupId);

public class ZkDevice : IDisposable
{
    private dynamic? _zk;
    private bool _connected;

    public ZkDevice()
    {
        try
        {
            var type = Type.GetTypeFromProgID("zkemkeeper.ZKEM") ?? Type.GetTypeFromProgID("zkemkeeper.CZKEM");
            if (type != null)
                _zk = Activator.CreateInstance(type);
            else
                _zk = null;
        }
        catch (System.IO.FileNotFoundException)
        {
            _zk = null;
            throw new InvalidOperationException("zkemkeeper.dll n'est pas trouvé ou n'est pas enregistré. Copiez toutes les DLL du SDK dans le dossier de l'application et enregistrez zkemkeeper.dll avec regsvr32 (SysWOW64) en tant qu'administrateur.");
        }
        catch
        {
            _zk = null;
        }
    }

    public bool Connect(string ip, int port = 4370, int commKey = 0)
    {
        if (_zk == null) throw new InvalidOperationException("zkemkeeper.dll n'est pas trouvé ou n'est pas enregistré. Copiez toutes les DLL du SDK dans le dossier de l'application et enregistrez zkemkeeper.dll avec regsvr32 (SysWOW64) en tant qu'administrateur.");
        try
        {
            if (commKey != 0) _zk.SetCommPassword(commKey);
            _connected = _zk.Connect_Net(ip, port);
            return _connected;
        }
        catch
        {
            return false;
        }
    }

    public IEnumerable<AttLog> ReadAttendance(int machineNumber = 1)
    {
        var logs = new List<AttLog>();
        if (_zk == null || !_connected) return logs;
        try
        {
            _zk.EnableDevice(machineNumber, false);
            if (_zk.ReadGeneralLogData(machineNumber))
            {
                logs.AddRange(LireJournaux(machineNumber));
            }
            _zk.EnableDevice(machineNumber, true);
        }
        catch
        {
            try { _zk.EnableDevice(machineNumber, true); } catch { }
        }
        return logs;
    }

    /// <summary>
    /// Lecture incrémentale : ne renvoie que les pointages enregistrés depuis la dernière lecture
    /// (ReadNewGLogData). N'immobilise PAS l'appareil, les employés peuvent badger pendant la lecture.
    /// </summary>
    public List<AttLog> ReadNewLogs(int machineNumber = 1)
    {
        var logs = new List<AttLog>();
        if (_zk == null || !_connected) return logs;
        try
        {
            if (_zk.ReadNewGLogData(machineNumber))
            {
                logs.AddRange(LireJournaux(machineNumber));
            }
        }
        catch
        {
            // lecture incrémentale impossible sur ce firmware — ignorer silencieusement
        }
        return logs;
    }

    private List<AttLog> LireJournaux(int machineNumber)
    {
        var logs = new List<AttLog>();
        string userId = string.Empty;
        int verifyMode = 0, inOutMode = 0, year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0, workCode = 0;
        int garde = 0;
        while (garde++ < 100000 && _zk.SSR_GetGeneralLogData(machineNumber,
                   out userId, out verifyMode, out inOutMode,
                   out year, out month, out day,
                   out hour, out minute, out second, ref workCode))
        {
            logs.Add(new AttLog(userId,
                new DateTime(year, month, day, hour, minute, second),
                verifyMode, inOutMode));
        }
        return logs;
    }

    public void SetUserInfo(int machineNumber, string enrollNumber, string name, string password, int privilege, bool enabled)
    {
        if (_zk == null || !_connected) throw new InvalidOperationException("ZkDevice non connecté.");
        _zk.SSR_SetUserInfo(machineNumber, enrollNumber, name, password, privilege, enabled);
    }

    public void SyncTime(int machineNumber = 1)
    {
        if (_zk == null || !_connected) return;
        try
        {
            var now = DateTime.Now;
            _zk.SetDeviceTime2(machineNumber, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        }
        catch { /* vérif supportée sur firmware */ }
    }

    public void RestartDevice(int machineNumber = 1)
    {
        if (_zk == null || !_connected) return;
        try { _zk.RestartDevice(machineNumber); } catch { /* peut échouer */ }
    }

    public void ClearAttendanceLogs(int machineNumber = 1)
    {
        if (_zk == null || !_connected) return;
        try { _zk.ClearGLog(machineNumber); } catch { /* pas toujours supporté */ }
    }

    public void PushMessage(int machineNumber, string content, int minutesValid = 60)
    {
        if (_zk == null || !_connected) return;
        try
        {
            // ID = concaténation du timestamp pour garantir l'unicité sur l'appareil
            int id = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
            string startTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            _zk.SetSMS(machineNumber, id, 1, minutesValid, startTime, content);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Erreur d'envoi de message : " + ex.Message, ex);
        }
    }

    public void PushWorkCodeBatch(int machineNumber, string csvLines)
    {
        if (_zk == null || !_connected) return;
        try
        {
            // Registre batch : "WORKCCODENAME\nWORKCCODENAME\n..."
            _zk.SSR_SetWorkCodeExBatch(machineNumber, csvLines);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Erreur d'envoi des work codes : " + ex.Message, ex);
        }
    }

    public void DeleteWorkCode(int machineNumber, string workCodeNum)
    {
        if (_zk == null || !_connected) return;
        try { _zk.SSR_DeleteWorkCodeEx(machineNumber, workCodeNum); } catch { }
    }

    public void Disconnect()
    {
        if (_zk == null || !_connected) return;
        try { _zk.Disconnect(); } catch { }
        _connected = false;
    }

    /// <summary>
    /// Lit tous les utilisateurs inscrits sur la pointeuse.
    /// Utilise ReadAllUserID + GetAllUserInfo (et en repli SSR_GetAllUserInfo).
    /// </summary>
    public List<ZkUser> ReadAllUsers(int machineNumber = 1, Action<string>? log = null)
    {
        var users = new List<ZkUser>();
        if (_zk == null || !_connected)
        {
            log?.Invoke("ReadAllUsers : aucune connexion active.");
            return users;
        }

        try
        {
            _zk.EnableDevice(machineNumber, true);
        }
        catch (Exception ex)
        {
            log?.Invoke("EnableDevice : " + ex.Message);
        }

        // --- Méthode 1 (recommandée) : SSR_GetAllUserInfo (enrollNumber en chaîne) ---
        try
        {
            int garde = 0;
            while (garde++ < 20000)
            {
                bool ok = _zk.SSR_GetAllUserInfo(machineNumber,
                    out string enrollNumber,
                    out string name,
                    out string password,
                    out int privilege,
                    out bool enabled);

                if (!ok) break;

                int numero = int.TryParse(enrollNumber, out int n) ? n : 0;
                users.Add(new ZkUser(
                    EnrollNumber: numero,
                    Name: name ?? string.Empty,
                    UserId: enrollNumber ?? string.Empty,
                    Card: string.Empty,
                    Privilege: privilege,
                    GroupId: enabled ? "Actif" : "Désactivé"));
            }

            log?.Invoke($"SSR_GetAllUserInfo : {users.Count} utilisateur(s).");
        }
        catch (Exception ex)
        {
            log?.Invoke("SSR_GetAllUserInfo a échoué : " + ex.Message);
        }

        // --- Méthode 2 (repli) : ReadAllUserID + GetAllUserInfo ---
        if (users.Count == 0)
        {
            try
            {
                object? r = _zk.ReadAllUserID(machineNumber);
                log?.Invoke($"ReadAllUserID => {r}");

                int garde = 0;
                while (garde++ < 20000)
                {
                    bool ok = _zk.GetAllUserInfo(machineNumber,
                        out int enrollNumber,
                        out string name,
                        out string password,
                        out int privilege,
                        out bool enabled);

                    if (!ok) break;

                    users.Add(new ZkUser(
                        EnrollNumber: enrollNumber,
                        Name: name ?? string.Empty,
                        UserId: enrollNumber.ToString(),
                        Card: string.Empty,
                        Privilege: privilege,
                        GroupId: enabled ? "Actif" : "Désactivé"));
                }

                log?.Invoke($"GetAllUserInfo : {users.Count} utilisateur(s).");
            }
            catch (Exception ex)
            {
                log?.Invoke("GetAllUserInfo a échoué : " + ex.Message);
            }
        }

        return users;
    }

    public void Dispose()
    {
        Disconnect();
        if (_zk != null && Marshal.IsComObject(_zk))
        {
            try { Marshal.ReleaseComObject(_zk); } catch { }
        }
        _zk = null;
    }
}
