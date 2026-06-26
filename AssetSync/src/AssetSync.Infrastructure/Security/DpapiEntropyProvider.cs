using System.Security.Cryptography;

namespace AssetSync.Infrastructure.Security;

/// <summary>Supplies the per-install DPAPI entropy used as a second factor when protecting the
/// stored credentials. See <see cref="FileDpapiEntropyProvider"/> for the storage design.</summary>
public interface IDpapiEntropyProvider
{
    /// <summary>Returns the per-install entropy bytes, generating and persisting them on first use.</summary>
    byte[] GetEntropy();
}

/// <summary>
/// Stores a per-install, cryptographically-random entropy value in a dedicated, ACL-locked directory
/// that is <b>separate from the credential database</b> (e.g. %ProgramData%\AssetSyncKeys, a sibling
/// of the DB directory). This is the per-install entropy from the plan §6b: it is mixed into the
/// DPAPI protection of the stored secrets so that obtaining only the DB blob (backup, shadow copy,
/// the historical LocalAppData copy, or an ACL slip scoped to the DB directory) is no longer enough
/// to decrypt them.
///
/// Design notes:
/// - DPAPI scope stays <see cref="DataProtectionScope.LocalMachine"/> everywhere, so both the
///   interactive app (writer) and the SYSTEM service (reader) can unwrap the entropy and decrypt the
///   credentials. The directory ACL (SYSTEM + Administrators + the creating user, Users:Read removed)
///   is what blocks other local users.
/// - The entropy value is itself DPAPI-LocalMachine-wrapped at rest, so it is never stored in the
///   clear; the ACL + separate location is the primary protection.
/// - Hardening is applied only when the file is first created, never on a later read, so a SYSTEM
///   service read cannot clobber the interactive user's ACE (mirrors the DB hardening model).
/// - Generation requires no elevation: the directory is creator-owned under ProgramData.
/// </summary>
public class FileDpapiEntropyProvider : IDpapiEntropyProvider
{
    private const int EntropyLengthBytes = 32;
    private const string EntropyFileName = "entropy.bin";

    private readonly string _directory;
    private readonly string _filePath;
    private readonly object _lock = new();
    private byte[]? _cached;

    public FileDpapiEntropyProvider(string directory)
    {
        _directory = directory;
        _filePath = Path.Combine(directory, EntropyFileName);
    }

    public byte[] GetEntropy()
    {
        lock (_lock)
        {
            if (_cached != null) return _cached;

            if (File.Exists(_filePath))
            {
                _cached = Unwrap(File.ReadAllBytes(_filePath));
                return _cached;
            }

            // First use on this machine/install: generate, persist, then lock down — once.
            Directory.CreateDirectory(_directory);
            var entropy = RandomNumberGenerator.GetBytes(EntropyLengthBytes);
            File.WriteAllBytes(_filePath, Wrap(entropy));
            DbFileSecurity.Harden(_directory, _filePath);

            _cached = entropy;
            return _cached;
        }
    }

    // LocalMachine-scoped wrap so both the interactive user and SYSTEM can unwrap; the ACL blocks
    // everyone else. No entropy on this inner layer (it is what provides entropy to the outer layer).
    private static byte[] Wrap(byte[] entropy) =>
        ProtectedData.Protect(entropy, null, DataProtectionScope.LocalMachine);

    private static byte[] Unwrap(byte[] stored) =>
        ProtectedData.Unprotect(stored, null, DataProtectionScope.LocalMachine);
}
