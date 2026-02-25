using AssetSync.Core.Enums;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using Microsoft.Data.Sqlite;

namespace AssetSync.Infrastructure.Data;

public class SqliteLogRepository : ILogRepository
{
    private readonly string _connectionString;

    public SqliteLogRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AppendAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO logs (timestamp_utc, level, source_system, action, serial_number, device_name, field_name, old_value, new_value, success, error_detail, sync_run_id)
            VALUES ($ts, $lv, $src, $act, $ser, $dev, $fld, $old, $new, $ok, $err, $run)";
        cmd.Parameters.AddWithValue("$ts", entry.TimestampUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$lv", entry.Level.ToString());
        cmd.Parameters.AddWithValue("$src", entry.SourceSystem.ToString());
        cmd.Parameters.AddWithValue("$act", entry.Action);
        cmd.Parameters.AddWithValue("$ser", entry.SerialNumber ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$dev", entry.DeviceName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$fld", entry.FieldName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$old", entry.OldValue ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$new", entry.NewValue ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$ok", entry.Success ? 1 : 0);
        cmd.Parameters.AddWithValue("$err", entry.ErrorDetail ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$run", entry.SyncRunId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LogEntry>> GetEntriesAsync(LogFilter filter, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "1=1" };
        if (filter.FromUtc.HasValue) conditions.Add("timestamp_utc >= $from");
        if (filter.ToUtc.HasValue) conditions.Add("timestamp_utc <= $to");
        if (filter.MinLevel.HasValue) conditions.Add("level >= $level");
        if (filter.SourceSystem.HasValue) conditions.Add("source_system = $src");
        if (!string.IsNullOrEmpty(filter.Action)) conditions.Add("action = $act");
        if (!string.IsNullOrEmpty(filter.SerialNumber)) conditions.Add("serial_number = $ser");
        if (!string.IsNullOrEmpty(filter.SyncRunId)) conditions.Add("sync_run_id = $run");
        if (!string.IsNullOrEmpty(filter.FreeText))
            conditions.Add("(error_detail LIKE $ft OR device_name LIKE $ft OR serial_number LIKE $ft)");

        var sql = "SELECT id, timestamp_utc, level, source_system, action, serial_number, device_name, field_name, old_value, new_value, success, error_detail, sync_run_id FROM logs WHERE " + string.Join(" AND ", conditions) + " ORDER BY timestamp_utc DESC";
        if (filter.Limit.HasValue) sql += " LIMIT " + filter.Limit.Value;
        if (filter.Offset.HasValue) sql += " OFFSET " + filter.Offset.Value;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (filter.FromUtc.HasValue) cmd.Parameters.AddWithValue("$from", filter.FromUtc.Value.UtcDateTime.ToString("O"));
        if (filter.ToUtc.HasValue) cmd.Parameters.AddWithValue("$to", filter.ToUtc.Value.UtcDateTime.ToString("O"));
        if (filter.MinLevel.HasValue) cmd.Parameters.AddWithValue("$level", filter.MinLevel.Value.ToString());
        if (filter.SourceSystem.HasValue) cmd.Parameters.AddWithValue("$src", filter.SourceSystem.Value.ToString());
        if (!string.IsNullOrEmpty(filter.Action)) cmd.Parameters.AddWithValue("$act", filter.Action);
        if (!string.IsNullOrEmpty(filter.SerialNumber)) cmd.Parameters.AddWithValue("$ser", filter.SerialNumber);
        if (!string.IsNullOrEmpty(filter.SyncRunId)) cmd.Parameters.AddWithValue("$run", filter.SyncRunId);
        if (!string.IsNullOrEmpty(filter.FreeText)) cmd.Parameters.AddWithValue("$ft", "%" + filter.FreeText + "%");

        var list = new List<LogEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new LogEntry
            {
                Id = reader.GetInt64(0),
                TimestampUtc = DateTimeOffset.Parse(reader.GetString(1)),
                Level = Enum.Parse<LogLevel>(reader.GetString(2)),
                SourceSystem = Enum.Parse<SourceSystem>(reader.GetString(3)),
                Action = reader.GetString(4),
                SerialNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                DeviceName = reader.IsDBNull(6) ? null : reader.GetString(6),
                FieldName = reader.IsDBNull(7) ? null : reader.GetString(7),
                OldValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                NewValue = reader.IsDBNull(9) ? null : reader.GetString(9),
                Success = reader.GetInt32(10) != 0,
                ErrorDetail = reader.IsDBNull(11) ? null : reader.GetString(11),
                SyncRunId = reader.GetString(12)
            });
        }
        return list;
    }

    public async Task PurgeOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - retention;
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM logs WHERE timestamp_utc < $cutoff";
        cmd.Parameters.AddWithValue("$cutoff", cutoff.UtcDateTime.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
