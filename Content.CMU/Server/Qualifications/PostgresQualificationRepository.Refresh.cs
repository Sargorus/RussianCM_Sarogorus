using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RuCM.Qualifications;
using Npgsql;

namespace Content.Server._RuCM.Qualifications;

public sealed partial class PostgresQualificationRepository : ICMUQualificationRefreshRepository
{
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaReady;

    public Task<QualificationStore?> LoadIfChanged(long revision, CancellationToken cancel = default) =>
        Task.Run(() => LoadVersion(revision, cancel), cancel);

    private async Task<QualificationStore?> LoadVersion(long? revision, CancellationToken cancel)
    {
        await using var connection = new NpgsqlConnection(_connection);
        await connection.OpenAsync(cancel);
        if (!Volatile.Read(ref _schemaReady))
        {
            await _schemaLock.WaitAsync(cancel);
            try
            {
                if (!_schemaReady)
                {
                    await using var transaction = await connection.BeginTransactionAsync(cancel);
                    await using (var setup = new NpgsqlCommand("SELECT pg_advisory_xact_lock(71714501); CREATE SCHEMA IF NOT EXISTS rucm_training; CREATE TABLE IF NOT EXISTS rucm_training.schema_migration(version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());", connection, transaction))
                        await setup.ExecuteNonQueryAsync(cancel);
                    await using var version = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM rucm_training.schema_migration WHERE version=1)", connection, transaction);
                    if (await version.ExecuteScalarAsync(cancel) is not true)
                    {
                        await using var schema = new NpgsqlCommand(Schema, connection, transaction);
                        await schema.ExecuteNonQueryAsync(cancel);
                    }
                    await transaction.CommitAsync(cancel);
                    Volatile.Write(ref _schemaReady, true);
                }
            }
            finally { _schemaLock.Release(); }
        }
        // CASE keeps the revision/body comparison in one statement and avoids transferring unchanged JSON.
        await using var read = new NpgsqlCommand(
            "SELECT CASE WHEN revision=@revision THEN NULL ELSE body::text END FROM rucm_training.state WHERE id=1", connection);
        read.Parameters.AddWithValue("revision", revision ?? -1);
        var body = await read.ExecuteScalarAsync(cancel);
        if (body is DBNull)
            return null;
        if (body is not string json)
        {
            if (revision.HasValue)
                throw new QualificationValidationException("storage");
            return null;
        }
        return JsonSerializer.Deserialize<QualificationStore>(json) ?? throw new QualificationValidationException("storage");
    }
}
