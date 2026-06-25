using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using AssetSync.Infrastructure.Data;
using AssetSync.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AssetSync.Infrastructure.Tests;

public class DbFileSecurityTests
{
    [Fact]
    public void Harden_RemovesUsersRead_PreservesSystemAdminsAndCurrentUser_AndDbStillOpens()
    {
        if (!OperatingSystem.IsWindows())
            return; // ACL hardening is a Windows-only control.

        VerifyOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyOnWindows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "AssetSyncAclTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "assetsync.db");
        try
        {
            // Create + initialize a real DB so there is a file to lock.
            var connectionString = $"Data Source={dbPath}";
            new DatabaseInitializer(connectionString).Initialize();
            Assert.True(File.Exists(dbPath));

            DbFileSecurity.Harden(dir, dbPath);

            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var currentUser = WindowsIdentity.GetCurrent().User!;

            foreach (var sec in new CommonObjectSecurity[]
                     {
                         new DirectoryInfo(dir).GetAccessControl(),
                         new FileInfo(dbPath).GetAccessControl(),
                     })
            {
                // Inheritance is broken (so ProgramData's inherited grants no longer apply).
                Assert.True(sec.AreAccessRulesProtected);

                var rules = sec.GetAccessRules(true, false, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>()
                    .Where(r => r.AccessControlType == AccessControlType.Allow)
                    .ToList();

                // BUILTIN\Users can no longer read.
                Assert.DoesNotContain(rules, r => r.IdentityReference.Equals(users));

                // SYSTEM (service) and Administrators retain full control; the interactive writer is allowed.
                Assert.Contains(rules, r => r.IdentityReference.Equals(system));
                Assert.Contains(rules, r => r.IdentityReference.Equals(admins));
                Assert.Contains(rules, r => r.IdentityReference.Equals(currentUser));
            }

            // The current identity (the app's writer) can still open and use the DB after hardening.
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM config";
            cmd.ExecuteScalar();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
