using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface ISnipeItService
{
    Task<Device?> GetAssetBySerialAsync(string normalizedSerial, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> SearchAssetsBySerialAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<Device?> CreateAssetAsync(Device device, CancellationToken cancellationToken = default);
    Task<bool> UpdateAssetAsync(int assetId, IReadOnlyDictionary<string, object?> updates, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SnipeItLookup>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SnipeItLookup>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SnipeItLookup>> GetUsersAsync(CancellationToken cancellationToken = default);

    // --- Software licenses / seats ---
    Task<IReadOnlyList<SnipeItLookup>> GetLicensesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LicenseSeat>> GetLicenseSeatsAsync(int licenseId, CancellationToken cancellationToken = default);
    Task<bool> CheckoutSeatAsync(int licenseId, int seatId, int snipeItUserId, CancellationToken cancellationToken = default);
    Task<bool> CheckinSeatAsync(int licenseId, int seatId, CancellationToken cancellationToken = default);
}
