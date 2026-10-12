using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RuCM.Qualifications;
using Content.Shared.CCVar;
using Npgsql;
using Robust.Shared.Configuration;

namespace Content.Server._RuCM.Qualifications;

public interface IRuCMQualificationRepository
{
    Task<QualificationStore?> Load(CancellationToken cancel = default);
    Task Save(QualificationStore store, long expectedRevision, CancellationToken cancel = default);
}

/// <summary>Own tables in the game's PostgreSQL database, transactional CAS and append-only audit.</summary>
public sealed partial class PostgresQualificationRepository : IRuCMQualificationRepository, ICMUQualificationDeltaRepository // CMU14: selective transactional writes.
{
    private readonly string _connection;
    public PostgresQualificationRepository(string connection) { _connection = connection; }

    /// <summary>
    /// Uses the same public database CVars as ServerDbManager.CreatePostgresOptions.
    /// There is deliberately no qualification-specific database or credential override.
    /// SQLite is selected independently by QualificationRepositoryFactory using the game's SQLite file.
    /// </summary>
    public static string? GameConnectionString(IConfigurationManager configuration)
    {
        if (!string.Equals(configuration.GetCVar(CCVars.DatabaseEngine), "postgres", StringComparison.OrdinalIgnoreCase))
            return null;

        return new NpgsqlConnectionStringBuilder
        {
            Host = configuration.GetCVar(CCVars.DatabasePgHost),
            Port = configuration.GetCVar(CCVars.DatabasePgPort),
            Database = configuration.GetCVar(CCVars.DatabasePgDatabase),
            Username = configuration.GetCVar(CCVars.DatabasePgUsername),
            Password = configuration.GetCVar(CCVars.DatabasePgPassword)
        }.ConnectionString;
    }

    public const string Schema = """
        CREATE SCHEMA IF NOT EXISTS rucm_training;
        CREATE TABLE IF NOT EXISTS rucm_training.schema_migration(version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());
        CREATE TABLE IF NOT EXISTS rucm_training.state(id integer PRIMARY KEY CHECK(id=1), revision bigint NOT NULL, body jsonb NOT NULL);
        CREATE TABLE IF NOT EXISTS rucm_training.record(kind text NOT NULL, key text NOT NULL, player uuid NULL, body jsonb NOT NULL, PRIMARY KEY(kind,key));
        CREATE INDEX IF NOT EXISTS rucm_training_record_player ON rucm_training.record(player,kind);
        CREATE TABLE IF NOT EXISTS rucm_training.audit(id uuid PRIMARY KEY, actor uuid NOT NULL, target uuid NULL, at timestamptz NOT NULL, action text NOT NULL, body jsonb NOT NULL);
        CREATE INDEX IF NOT EXISTS rucm_training_audit_target ON rucm_training.audit(target,at);
        CREATE OR REPLACE FUNCTION rucm_training.immutable_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Qualification audit is append-only'; END; $$;
        DROP TRIGGER IF EXISTS immutable_audit ON rucm_training.audit;
        CREATE TRIGGER immutable_audit BEFORE UPDATE OR DELETE ON rucm_training.audit FOR EACH ROW EXECUTE FUNCTION rucm_training.immutable_audit();
        INSERT INTO rucm_training.schema_migration(version) VALUES(1) ON CONFLICT DO NOTHING;
        """;

    // CMU14 method: schema setup and deserialization are outside the game tick.
    public Task<QualificationStore?> Load(CancellationToken cancel = default) =>
        Task.Run(() => LoadVersion(null, cancel), cancel);

    // CMU14: full writes remain available for initialization and callers without an owned mutation.
    public Task Save(QualificationStore store, long expectedRevision, CancellationToken cancel = default) =>
        SaveCore(store, expectedRevision, CMUQualificationWriteSet.Full(store), cancel);

    public Task SaveChanges(QualificationStore store, QualificationStore previous, CancellationToken cancel = default) =>
        SaveCore(store, previous.Revision, CMUQualificationWriteSet.Changes(store, previous), cancel);

    private async Task SaveCore(QualificationStore store, long expectedRevision, CMUQualificationWriteSet changes, CancellationToken cancel)
    {
        await using var connection = new NpgsqlConnection(_connection);
        await connection.OpenAsync(cancel);
        await using var transaction = await connection.BeginTransactionAsync(cancel);
        await using (var command = new NpgsqlCommand("""
            INSERT INTO rucm_training.state(id,revision,body)
            SELECT 1,@revision,CAST(@body AS jsonb) WHERE @expected=0 OR EXISTS(SELECT 1 FROM rucm_training.state WHERE id=1)
            ON CONFLICT(id) DO UPDATE SET revision=EXCLUDED.revision,body=EXCLUDED.body
            WHERE rucm_training.state.revision=@expected
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("revision", store.Revision);
            command.Parameters.AddWithValue("expected", expectedRevision);
            command.Parameters.AddWithValue("body", JsonSerializer.Serialize(store));
            if (await command.ExecuteNonQueryAsync(cancel) != 1)
                throw new QualificationConflictException();
        }

        // CMU14: bounded batches reduce network round trips while retaining the single CAS transaction.
        await using var batch = new NpgsqlBatch(connection, transaction);
        async Task Flush()
        {
            if (batch.BatchCommands.Count == 0) return;
            await batch.ExecuteNonQueryAsync(cancel);
            batch.BatchCommands.Clear();
        }
        async Task Queue(NpgsqlBatchCommand command)
        {
            batch.BatchCommands.Add(command);
            if (batch.BatchCommands.Count >= 256) await Flush();
        }
        async Task Record(string kind, string key, Guid? player, object value)
        {
            var command = new NpgsqlBatchCommand("""
                INSERT INTO rucm_training.record(kind,key,player,body) VALUES(@kind,@key,@player,CAST(@body AS jsonb))
                ON CONFLICT(kind,key) DO UPDATE SET player=EXCLUDED.player,body=EXCLUDED.body
                """);
            command.Parameters.AddWithValue("kind", kind);
            command.Parameters.AddWithValue("key", key);
            command.Parameters.AddWithValue("player", NpgsqlTypes.NpgsqlDbType.Uuid, (object?) player ?? DBNull.Value);
            command.Parameters.AddWithValue("body", JsonSerializer.Serialize(value));
            await Queue(command);
        }
        foreach (var (id, definition) in changes.Records.Definitions) await Record("qualification_definition", id, null, definition);
        foreach (var (id, role) in changes.Records.Roles) await Record("role_requirement", id, null, role);
        var progressKeys = new System.Collections.Generic.List<string>();
        foreach (var (id, player) in changes.Records.Players)
        {
            await Record("player", id.ToString(), id, player);
            foreach (var (qualification, grant) in player.Grants)
                await Record("grant", JsonSerializer.Serialize(new[] { id.ToString(), qualification }), id, grant);
            foreach (var (qualification, progress) in player.Progress)
            foreach (var (item, completion) in progress)
            {
                var key = JsonSerializer.Serialize(new[] { id.ToString(), qualification, item });
                progressKeys.Add(key); await Record("progress", key, id, completion);
            }
        }
        await Flush();
        await using (var obsolete = new NpgsqlCommand(
            changes.Players == null
                ? "DELETE FROM rucm_training.record WHERE kind='progress' AND NOT(key=ANY(@keys))"
                : "DELETE FROM rucm_training.record WHERE kind='progress' AND player=ANY(@players) AND NOT(key=ANY(@keys))",
            connection, transaction))
        {
            obsolete.Parameters.AddWithValue("keys", progressKeys.ToArray());
            if (changes.Players != null) obsolete.Parameters.AddWithValue("players", changes.Players);
            await obsolete.ExecuteNonQueryAsync(cancel);
        }
        foreach (var (id, accreditation) in changes.Records.Instructors) await Record("instructor", id.ToString(), id, accreditation);
        foreach (var note in changes.Records.Notes) await Record("training_note", note.Id.ToString(), note.Target, note);
        foreach (var suspension in changes.Records.Suspensions) await Record("suspension", suspension.Id.ToString(), suspension.Target, suspension);
        await Record("system_setting", "management", null, changes.Records.Management);
        await Record("system_setting", "officer_jobs", null, changes.Records.OfficerJobs);
        await Record("system_setting", "co_jobs", null, changes.Records.CommandingOfficerJobs);
        await Record("system_setting", "migration_groups", null, changes.Records.MigrationGroups);
        await Record("system_setting", "tracker_aliases", null, changes.Records.TrackerAliases);
        foreach (var key in changes.Records.Migrations) await Record("migration_state", key, null, new { Key = key });
        foreach (var audit in changes.Records.Audit)
        {
            var command = new NpgsqlBatchCommand("""
                INSERT INTO rucm_training.audit(id,actor,target,at,action,body)
                VALUES(@id,@actor,@target,@at,@action,CAST(@body AS jsonb)) ON CONFLICT(id) DO NOTHING
                """);
            command.Parameters.AddWithValue("id", audit.Id);
            command.Parameters.AddWithValue("actor", audit.Actor);
            command.Parameters.AddWithValue("target", NpgsqlTypes.NpgsqlDbType.Uuid, (object?) audit.Target ?? DBNull.Value);
            command.Parameters.AddWithValue("at", audit.At.ToUniversalTime());
            command.Parameters.AddWithValue("action", audit.Action);
            command.Parameters.AddWithValue("body", JsonSerializer.Serialize(audit));
            await Queue(command);
        }
        await Flush();
        await transaction.CommitAsync(cancel);
    }
}

public sealed class QualificationConflictException : Exception;

/// <summary>Explicit test fixture storage, never selected by the production integration.</summary>
public sealed class MemoryQualificationRepository : IRuCMQualificationRepository
{
    private QualificationStore? _store;
    public Task<QualificationStore?> Load(CancellationToken cancel = default)
    { lock (this) return Task.FromResult(_store?.Clone()); }
    public Task Save(QualificationStore store, long expectedRevision, CancellationToken cancel = default)
    {
        lock (this)
        {
            if ((_store?.Revision ?? 0) != expectedRevision) throw new QualificationConflictException();
            _store = store.Clone();
        }
        return Task.CompletedTask;
    }
}
