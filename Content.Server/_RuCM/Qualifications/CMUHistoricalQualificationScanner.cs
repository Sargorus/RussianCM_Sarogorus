// CMU14: historical qualification migration.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Npgsql;
using Robust.Shared.Configuration;

namespace Content.Server._RuCM.Qualifications;

/// <summary>Reads historical accounts and role timers in the game DB without changing its schema or data.</summary>
public sealed class CMUHistoricalQualificationScanner
{
    private readonly string _connection;
    private readonly bool _sqlite;

    public CMUHistoricalQualificationScanner(string connection, bool sqlite)
    {
        _connection = sqlite ? new SqliteConnectionStringBuilder(connection) { Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString : connection;
        _sqlite = sqlite;
    }

    public static CMUHistoricalQualificationScanner? Create(IConfigurationManager configuration, string? userDataRoot)
    {
        if (PostgresQualificationRepository.GameConnectionString(configuration) is { } postgres)
            return new(postgres, false);
        if (SqliteQualificationRepository.GameConnectionString(configuration, userDataRoot) is { } sqlite)
            return new(new SqliteConnectionStringBuilder(sqlite) { Mode = SqliteOpenMode.ReadOnly }.ConnectionString, true);
        return null;
    }

    // SQLite's async APIs perform synchronous I/O. Keep the entire scan off the simulation thread.
    public Task<List<MigrationCandidate>> Scan(CancellationToken cancel = default) =>
        Task.Run(() => ScanCore(cancel), cancel);

    private async Task<List<MigrationCandidate>> ScanCore(CancellationToken cancel)
    {
        await using DbConnection connection = _sqlite ? new SqliteConnection(_connection) : new NpgsqlConnection(_connection);
        await connection.OpenAsync(cancel);
        await using var command = connection.CreateCommand();
        // Include accounts with zero timers and old timer-only accounts. A single statement gives a consistent snapshot.
        command.CommandText = """
            SELECT accounts.user_id, timers.tracker, timers.time_spent
            FROM (SELECT user_id FROM player UNION SELECT player_id AS user_id FROM play_time) AS accounts
            LEFT JOIN play_time AS timers ON timers.player_id = accounts.user_id
            ORDER BY accounts.user_id
            """;
        command.CommandTimeout = 120;
        await using var reader = await command.ExecuteReaderAsync(cancel);
        var candidates = new List<MigrationCandidate>();
        MigrationCandidate? candidate = null;
        while (await reader.ReadAsync(cancel))
        {
            cancel.ThrowIfCancellationRequested();
            var player = reader.GetGuid(0);
            if (player == Guid.Empty) continue;
            if (candidate == null || candidate.Player != player)
            {
                candidate = new(player, new());
                candidates.Add(candidate);
            }
            if (reader.IsDBNull(1)) continue;
            var tracker = reader.GetString(1);
            var hours = reader.GetFieldValue<TimeSpan>(2).TotalHours;
            candidate.TrackerHours[tracker] = candidate.TrackerHours.GetValueOrDefault(tracker) + hours;
        }
        return candidates;
    }
}
