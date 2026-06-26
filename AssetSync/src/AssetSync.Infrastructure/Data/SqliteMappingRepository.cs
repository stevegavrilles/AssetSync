using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using Microsoft.Data.Sqlite;

namespace AssetSync.Infrastructure.Data;

public class SqliteMappingRepository : IMappingRepository
{
    private readonly string _connectionString;

    public SqliteMappingRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<ModelMapping>> GetModelMappingsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllMappingsAsync<ModelMapping>("model_mappings", "mdm_model_string", "snipeit_model_id",
            r => new ModelMapping { Id = r.GetInt32(0), MdmModelString = r.GetString(1), SnipeItModelId = r.GetInt32(2) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ModelMapping?> GetModelMappingAsync(string mdmModelString, CancellationToken cancellationToken = default)
    {
        return await GetMappingAsync("model_mappings", "mdm_model_string", "snipeit_model_id", mdmModelString,
            r => new ModelMapping { Id = r.GetInt32(0), MdmModelString = r.GetString(1), SnipeItModelId = r.GetInt32(2) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveModelMappingAsync(ModelMapping mapping, CancellationToken cancellationToken = default)
    {
        await SaveMappingAsync("model_mappings", "mdm_model_string", "snipeit_model_id", mapping.Id, mapping.MdmModelString, mapping.SnipeItModelId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserMapping>> GetUserMappingsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllMappingsAsync<UserMapping>("user_mappings", "mdm_user_identifier", "snipeit_user_id",
            r => new UserMapping { Id = r.GetInt32(0), MdmUserIdentifier = r.GetString(1), SnipeItUserId = r.GetInt32(2) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserMapping?> GetUserMappingAsync(string mdmUserIdentifier, CancellationToken cancellationToken = default)
    {
        return await GetMappingAsync("user_mappings", "mdm_user_identifier", "snipeit_user_id", mdmUserIdentifier,
            r => new UserMapping { Id = r.GetInt32(0), MdmUserIdentifier = r.GetString(1), SnipeItUserId = r.GetInt32(2) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveUserMappingAsync(UserMapping mapping, CancellationToken cancellationToken = default)
    {
        await SaveMappingAsync("user_mappings", "mdm_user_identifier", "snipeit_user_id", mapping.Id, mapping.MdmUserIdentifier, mapping.SnipeItUserId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BuildMapping>> GetBuildMappingsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllMappingsAsync<BuildMapping>("build_mappings", "build_number", "friendly_name",
            r => new BuildMapping { Id = r.GetInt32(0), BuildNumber = r.GetString(1), FriendlyName = r.GetString(2) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BuildMapping?> GetBuildMappingAsync(string buildNumber, CancellationToken cancellationToken = default)
    {
        return await GetMappingAsync("build_mappings", "build_number", "friendly_name", buildNumber,
            r => new BuildMapping { Id = r.GetInt32(0), BuildNumber = r.GetString(1), FriendlyName = r.GetString(2) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveBuildMappingAsync(BuildMapping mapping, CancellationToken cancellationToken = default)
    {
        await SaveMappingAsync("build_mappings", "build_number", "friendly_name", mapping.Id, mapping.BuildNumber, mapping.FriendlyName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CategoryMapping>> GetCategoryMappingsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllMappingsAsync<CategoryMapping>("category_mappings", "mdm_device_type", "snipeit_category_id",
            r => new CategoryMapping { Id = r.GetInt32(0), MdmDeviceType = r.GetString(1), SnipeItCategoryId = r.GetInt32(2) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<CategoryMapping?> GetCategoryMappingAsync(string mdmDeviceType, CancellationToken cancellationToken = default)
    {
        return await GetMappingAsync("category_mappings", "mdm_device_type", "snipeit_category_id", mdmDeviceType,
            r => new CategoryMapping { Id = r.GetInt32(0), MdmDeviceType = r.GetString(1), SnipeItCategoryId = r.GetInt32(2) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveCategoryMappingAsync(CategoryMapping mapping, CancellationToken cancellationToken = default)
    {
        await SaveMappingAsync("category_mappings", "mdm_device_type", "snipeit_category_id", mapping.Id, mapping.MdmDeviceType, mapping.SnipeItCategoryId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<T>> GetAllMappingsAsync<T>(string table, string keyCol, string valueCol, Func<SqliteDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id, {keyCol}, {valueCol} FROM {table}";
        var list = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(map(reader));
        return list;
    }

    private async Task<T?> GetMappingAsync<T>(string table, string keyCol, string valueCol, string keyValue, Func<SqliteDataReader, T> map, CancellationToken cancellationToken) where T : class
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id, {keyCol}, {valueCol} FROM {table} WHERE {keyCol} = $k LIMIT 1";
        cmd.Parameters.AddWithValue("$k", keyValue);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? map(reader) : null;
    }

    public Task DeleteModelMappingAsync(int id, CancellationToken cancellationToken = default) => DeleteMappingAsync("model_mappings", id, cancellationToken);
    public Task DeleteUserMappingAsync(int id, CancellationToken cancellationToken = default) => DeleteMappingAsync("user_mappings", id, cancellationToken);
    public Task DeleteBuildMappingAsync(int id, CancellationToken cancellationToken = default) => DeleteMappingAsync("build_mappings", id, cancellationToken);
    public Task DeleteCategoryMappingAsync(int id, CancellationToken cancellationToken = default) => DeleteMappingAsync("category_mappings", id, cancellationToken);

    public async Task<IReadOnlyList<string>> GetIgnoredModelsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT mdm_model_string FROM model_ignores ORDER BY mdm_model_string";
        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(reader.GetString(0));
        return list;
    }

    public async Task<bool> IsModelIgnoredAsync(string mdmModelString, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM model_ignores WHERE mdm_model_string = $m LIMIT 1";
        cmd.Parameters.AddWithValue("$m", mdmModelString);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null;
    }

    public async Task AddModelIgnoreAsync(string mdmModelString, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO model_ignores (mdm_model_string) VALUES ($m)";
        cmd.Parameters.AddWithValue("$m", mdmModelString);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveModelIgnoreAsync(string mdmModelString, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM model_ignores WHERE mdm_model_string = $m";
        cmd.Parameters.AddWithValue("$m", mdmModelString);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteMappingAsync(string table, int id, CancellationToken cancellationToken)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {table} WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // --- Group <-> license mappings ---

    public async Task<IReadOnlyList<GroupLicenseMapping>> GetGroupLicenseMappingsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, entra_group_id, entra_group_name, snipeit_license_id, read_only, last_run_status, last_error FROM group_license_mappings ORDER BY entra_group_name";
        var list = new List<GroupLicenseMapping>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new GroupLicenseMapping
            {
                Id = reader.GetInt32(0),
                EntraGroupId = reader.GetString(1),
                EntraGroupName = reader.GetString(2),
                SnipeItLicenseId = reader.GetInt32(3),
                ReadOnly = reader.GetInt32(4) != 0,
                LastRunStatus = reader.IsDBNull(5) ? null : reader.GetString(5),
                LastError = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return list;
    }

    public async Task SaveGroupLicenseMappingAsync(GroupLicenseMapping mapping, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Enforce at most one write/provisioning (read_only = 0) group per license, with a friendly
        // message ahead of the partial unique index backstop.
        if (!mapping.ReadOnly)
        {
            await using var check = conn.CreateCommand();
            check.CommandText = "SELECT entra_group_name FROM group_license_mappings WHERE snipeit_license_id = $lid AND read_only = 0 AND id != $id LIMIT 1";
            check.Parameters.AddWithValue("$lid", mapping.SnipeItLicenseId);
            check.Parameters.AddWithValue("$id", mapping.Id);
            var existing = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is string existingName)
                throw new InvalidOperationException($"License already has a write/provisioning group: {existingName}");
        }

        await using var cmd = conn.CreateCommand();
        if (mapping.Id > 0)
        {
            cmd.CommandText = @"UPDATE group_license_mappings
                SET entra_group_id = $gid, entra_group_name = $gname, snipeit_license_id = $lid, read_only = $ro
                WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", mapping.Id);
        }
        else
        {
            cmd.CommandText = @"INSERT OR REPLACE INTO group_license_mappings (entra_group_id, entra_group_name, snipeit_license_id, read_only)
                VALUES ($gid, $gname, $lid, $ro)";
        }
        cmd.Parameters.AddWithValue("$gid", mapping.EntraGroupId);
        cmd.Parameters.AddWithValue("$gname", mapping.EntraGroupName);
        cmd.Parameters.AddWithValue("$lid", mapping.SnipeItLicenseId);
        cmd.Parameters.AddWithValue("$ro", mapping.ReadOnly ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteGroupLicenseMappingAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        // Grace state is keyed per license (shared across the license's groups), so deleting one
        // group does not remove it; any orphaned counters are transient and self-heal on next sync.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM group_license_mappings WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateGroupLicenseRunStatusAsync(int id, string status, string? error, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE group_license_mappings SET last_run_status = $s, last_error = $e WHERE id = $id";
        cmd.Parameters.AddWithValue("$s", status);
        cmd.Parameters.AddWithValue("$e", (object?)error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // --- Grace-period / soft-delete state ---

    public async Task<IReadOnlyList<PendingRemoval>> GetPendingRemovalsAsync(int licenseId, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT snipeit_license_id, subject_key, consecutive_misses, first_missed_utc FROM license_group_pending_removals WHERE snipeit_license_id = $lid";
        cmd.Parameters.AddWithValue("$lid", licenseId);
        var list = new List<PendingRemoval>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new PendingRemoval
            {
                LicenseId = reader.GetInt32(0),
                SubjectKey = reader.GetString(1),
                ConsecutiveMisses = reader.GetInt32(2),
                FirstMissedUtc = DateTimeOffset.TryParse(reader.GetString(3), out var dt) ? dt : DateTimeOffset.UtcNow
            });
        }
        return list;
    }

    public async Task UpsertPendingRemovalAsync(int licenseId, string subjectKey, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO license_group_pending_removals (snipeit_license_id, subject_key, consecutive_misses, first_missed_utc)
            VALUES ($lid, $key, 1, $now)
            ON CONFLICT(snipeit_license_id, subject_key) DO UPDATE SET consecutive_misses = consecutive_misses + 1";
        cmd.Parameters.AddWithValue("$lid", licenseId);
        cmd.Parameters.AddWithValue("$key", subjectKey);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearPendingRemovalAsync(int licenseId, string subjectKey, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM license_group_pending_removals WHERE snipeit_license_id = $lid AND subject_key = $key";
        cmd.Parameters.AddWithValue("$lid", licenseId);
        cmd.Parameters.AddWithValue("$key", subjectKey);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveMappingAsync(string table, string keyCol, string valueCol, int id, string keyValue, object value, CancellationToken cancellationToken)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (id > 0)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE {table} SET {keyCol} = $k, {valueCol} = $v WHERE id = $id";
            cmd.Parameters.AddWithValue("$k", keyValue);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT OR REPLACE INTO {table} ({keyCol}, {valueCol}) VALUES ($k, $v)";
            cmd.Parameters.AddWithValue("$k", keyValue);
            cmd.Parameters.AddWithValue("$v", value);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
