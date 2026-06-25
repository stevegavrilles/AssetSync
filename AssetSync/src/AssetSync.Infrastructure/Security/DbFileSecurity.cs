using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AssetSync.Infrastructure.Security;

/// <summary>
/// Locks down the AssetSync data directory (and the SQLite DB + its WAL/SHM sidecars) so that the
/// DPAPI-encrypted credentials at rest are not readable by ordinary local users.
///
/// ProgramData grants <c>BUILTIN\Users: Read</c> by default; because the credential store uses
/// DPAPI <see cref="System.Security.Cryptography.DataProtectionScope.LocalMachine"/> with no
/// entropy, any local user who can read the blob can decrypt it. This applies an explicit,
/// inheritance-protected ACL granting only:
///   - LocalSystem (the SYSTEM service — the reader/writer), FullControl
///   - BUILTIN\Administrators (management + the typical elevated interactive user), FullControl
///   - the current interactive user (the WPF app — the writer), Modify
/// and drops the inherited Users:Read.
///
/// The DPAPI scope is intentionally NOT changed — both the interactive app and the SYSTEM service
/// must still be able to decrypt the shared DB, which is exactly why LocalMachine scope is used.
/// Granting Administrators FullControl keeps the (almost always elevated) interactive writer
/// working even if the SYSTEM service later re-applies the ACL.
///
/// Best-effort: failures (non-NTFS volume, insufficient rights, etc.) are swallowed so they never
/// block startup. No-op on non-Windows.
///
/// NOTE: per-install DPAPI entropy is a separate, harder hardening step and is NOT done here — see
/// the plan (docs/entra-license-sync-plan.md, §6b). It would require storing a per-install secret
/// readable by both the interactive user and SYSTEM but not co-located with the DB in a
/// Users-readable path (machine-keyed store or a low-priv service account), then threading it
/// through DpapiCredentialStore.Protect/Unprotect's optionalEntropy parameter.
/// </summary>
public static class DbFileSecurity
{
    public static void Harden(string directory, string? dbFilePath = null)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            HardenWindows(directory, dbFilePath);
        }
        catch
        {
            // Best-effort: hardening must never prevent the app/service from starting.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void HardenWindows(string directory, string? dbFilePath)
    {
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var currentUser = WindowsIdentity.GetCurrent().User; // interactive writer, or SYSTEM when run by the service

        if (Directory.Exists(directory))
        {
            var dirInfo = new DirectoryInfo(directory);
            var dirSec = new DirectorySecurity();
            // Break inheritance and discard inherited ACEs — this removes ProgramData's Users:Read.
            dirSec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            dirSec.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            dirSec.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            if (currentUser != null)
                dirSec.AddAccessRule(new FileSystemAccessRule(currentUser, FileSystemRights.Modify, inherit, PropagationFlags.None, AccessControlType.Allow));

            dirInfo.SetAccessControl(dirSec);
        }

        if (!string.IsNullOrEmpty(dbFilePath))
        {
            // Lock the DB and its WAL/SHM sidecars too (they hold the same data).
            foreach (var path in new[] { dbFilePath, dbFilePath + "-wal", dbFilePath + "-shm" })
            {
                if (!File.Exists(path)) continue;
                var fileInfo = new FileInfo(path);
                var fileSec = new FileSecurity();
                fileSec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                fileSec.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));
                fileSec.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));
                if (currentUser != null)
                    fileSec.AddAccessRule(new FileSystemAccessRule(currentUser, FileSystemRights.Modify, AccessControlType.Allow));
                fileInfo.SetAccessControl(fileSec);
            }
        }
    }
}
