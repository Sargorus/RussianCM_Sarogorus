using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RuCM.Qualifications;
using Microsoft.Data.Sqlite;

namespace Content.Server._RuCM.Qualifications;

public sealed partial class SqliteQualificationRepository : ICMUQualificationRefreshRepository
{
    private readonly object _schemaLock = new();
    private bool _schemaReady;

    public Task<QualificationStore?> LoadIfChanged(long revision, CancellationToken cancel = default) =>
        Task.Run(() => LoadVersion(revision, cancel), cancel);

    private QualificationStore? LoadVersion(long? revision, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        using var connection = new SqliteConnection(_connection);
        connection.Open();
        if (!Volatile.Read(ref _schemaReady))
        {
            lock (_schemaLock)
            {
                if (!_schemaReady)
                {
                    // Serialize independent server starts, then release the write lock before ordinary reads.
                    using var transaction = connection.BeginTransaction(deferred: false);
                    using (var setup = Command(connection, transaction, "CREATE TABLE IF NOT EXISTS rucm_training_schema_migration(version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)"))
                        setup.ExecuteNonQuery();
                    using (var version = Command(connection, transaction, "SELECT EXISTS(SELECT 1 FROM rucm_training_schema_migration WHERE version=1)"))
                    {
                        if (Convert.ToInt64(version.ExecuteScalar()) == 0)
                        {
                            using var schema = Command(connection, transaction, Schema);
                            schema.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                    Volatile.Write(ref _schemaReady, true);
                }
            }
        }
        cancel.ThrowIfCancellationRequested();
        using var read = new SqliteCommand(
            "SELECT CASE WHEN revision=@revision THEN NULL ELSE body END FROM rucm_training_state WHERE id=1", connection);
        read.Parameters.AddWithValue("revision", revision ?? -1);
        var body = read.ExecuteScalar();
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
