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
