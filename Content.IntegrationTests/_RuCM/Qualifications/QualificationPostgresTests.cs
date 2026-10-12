using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._RuCM.Qualifications;
using Content.Shared._RuCM.Qualifications;
using Content.Server.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Content.IntegrationTests._RuCM.Qualifications;

[TestFixture]
public sealed class QualificationPostgresTests
{
    [Test]
    public async Task ExistingGameDatabaseCoexistsWithQualificationsAuditMigrationAndConcurrentWriters()
    {
        var connectionString = Environment.GetEnvironmentVariable("RUCM_QUALIFICATIONS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Ignore("Set RUCM_QUALIFICATIONS_TEST_CONNECTION to an isolated test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.That(builder.Database, Does.StartWith("rucm_qualifications_test"), "Never run against a gameplay database");
        builder.Pooling = false;
        connectionString = builder.ConnectionString;
        // The test reuses one disposable database containing the real game EF model.
        // It never creates/drops a database and never drops game tables.
        await using var guard = new NpgsqlConnection(connectionString);
        await guard.OpenAsync();
        await using (var fixtureLock = new NpgsqlCommand("SELECT pg_advisory_lock(71714502)", guard))
            await fixtureLock.ExecuteNonQueryAsync();
        await using (var existing = new NpgsqlCommand("SELECT to_regnamespace('rucm_training') IS NULL", guard))
            Assert.That(await existing.ExecuteScalarAsync(), Is.True, "Refusing to overwrite existing qualification data, even in a test database");

        var gameOptions = new DbContextOptionsBuilder<PostgresServerDbContext>().UseNpgsql(connectionString).Options;
        await using var game = new PostgresServerDbContext(gameOptions);
        await game.Database.EnsureCreatedAsync();
        var gameAccount = Guid.NewGuid();
        game.Preference.Add(new Preference { UserId = gameAccount, AdminOOCColor = "#123456", SelectedCharacterSlot = 0 });
        game.PlayTime.Add(new PlayTime { PlayerId = gameAccount, Tracker = "qualification_coexistence_test", TimeSpent = TimeSpan.FromHours(7) });
        await game.SaveChangesAsync();
        // CMU14: read the actual game UUID and interval types without a roster or qualification writes.
        var candidates = await new CMUHistoricalQualificationScanner(connectionString, false).Scan();
        Assert.That(candidates.Single(c => c.Player == gameAccount).TrackerHours["qualification_coexistence_test"], Is.EqualTo(7));
        var gameSchema = await GameSchemaFingerprint(guard);
        try
        {
        var repository = new PostgresQualificationRepository(connectionString);
        var seed = new QualificationStore();
        foreach (var id in QualificationRules.Levels.Concat(new[] { "medical" })) seed.Definitions[id] = new() { Id = id, Name = id, Items = new() { new() { Id = "practice" } } };
        seed.Roles["test_officer"] = new() { JobId = "test_officer", Govfor = true, MinimumLevel = MilitaryLevel.Officer, Tracker = "test_officer_tracker" };
        var s = new QualificationService(repository); await s.Initialize(seed);
        Assert.That(s.Available, Is.True);
        var actorId = Guid.NewGuid(); var target = Guid.NewGuid();
        var context = new TrainingContext(actorId, "test actor", "test role", 71, "test", DateTimeOffset.UtcNow);
        var authority = new QualificationAuthority(context, true, false, false, false, false);
        var req = new QualificationRequest { Target = target, Qualification = "medical", Item = "practice", Reason = "test" };
        await s.Apply(authority, QualificationAction.Complete, req);
        await s.Apply(authority, QualificationAction.Certify, req);
        await s.Apply(authority, QualificationAction.Suspend, req);
        var count = s.Snapshot().Audit.Count;
        var restarted = new QualificationService(new PostgresQualificationRepository(connectionString));
        await restarted.Initialize(seed);
        Assert.That(restarted.GetPlayerTrainingState(target).Progress["medical"].ContainsKey("practice"), Is.True);
        Assert.That(restarted.GetPlayerTrainingState(target).Grants["medical"].Status, Is.EqualTo(QualificationStatus.Suspended));
        Assert.That(restarted.Snapshot().Audit.Count, Is.EqualTo(count));
        Assert.That(restarted.Snapshot().Suspensions.Single(x => x.Target == target).Initiator.Character, Is.EqualTo("test actor"));

        // Immutable database audit, not merely a read-only UI.
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("UPDATE rucm_training.audit SET action='forged' WHERE target=@target", connection);
            command.Parameters.AddWithValue("target", target);
            Assert.ThrowsAsync<PostgresException>(async () => await command.ExecuteNonQueryAsync());
        }
        var snapshot = restarted.Snapshot();
        var stale = snapshot.Clone(); stale.Revision++;
        var winning = snapshot.Clone(); winning.Revision++;
        await repository.Save(winning, snapshot.Revision);
        Assert.ThrowsAsync<QualificationConflictException>(() => repository.Save(stale, snapshot.Revision));

        var migrationService = new QualificationService(repository); await migrationService.Initialize(seed);
        var migrationTarget = Guid.NewGuid(); var before = migrationService.Snapshot().Revision;
        var plan = migrationService.MigrationDryRun(new[] { new MigrationCandidate(migrationTarget, new() { ["test_officer_tracker"] = 10 }) }, context.At);
        Assert.That((await repository.Load()).Revision, Is.EqualTo(before));
        await migrationService.ExecuteMigration(authority, plan);
        var migrated = (await repository.Load()).Audit.Count;
        await migrationService.ExecuteMigration(authority, plan);
        Assert.That((await repository.Load()).Audit.Count, Is.EqualTo(migrated));
        var afterRestart = new QualificationService(repository); await afterRestart.Initialize(seed);
        Assert.That(QualificationRules.EffectiveLevel(afterRestart.GetPlayerTrainingState(migrationTarget)), Is.EqualTo(MilitaryLevel.Officer));

        game.ChangeTracker.Clear();
        var preference = await game.Preference.SingleAsync(p => p.UserId == gameAccount);
        Assert.That(preference.AdminOOCColor, Is.EqualTo("#123456"));
        Assert.That((await game.PlayTime.SingleAsync(p => p.PlayerId == gameAccount)).TimeSpent, Is.EqualTo(TimeSpan.FromHours(7)));
        Assert.That(await GameSchemaFingerprint(guard), Is.EqualTo(gameSchema), "Qualification initialization/writes must not change the upstream game schema");
        Assert.That(game.Database.GetDbConnection().Database, Is.EqualTo(builder.Database));
        await using (var tables = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema='rucm_training'", guard))
            Assert.That((long) await tables.ExecuteScalarAsync(), Is.EqualTo(4));

        // Game writes still work after qualification migrations and service restarts.
        preference.AdminOOCColor = "#abcdef";
        await game.SaveChangesAsync();
        Assert.That((await repository.Load()).Players[target].Grants["medical"].Status, Is.EqualTo(QualificationStatus.Suspended));
        }
        finally
        {
            // Preflight verified this schema did not exist; only our fixture owns it.
            await using var drop = new NpgsqlCommand("DROP SCHEMA IF EXISTS rucm_training CASCADE", guard);
            await drop.ExecuteNonQueryAsync();
            await game.PlayTime.Where(p => p.PlayerId == gameAccount).ExecuteDeleteAsync();
            await game.Preference.Where(p => p.UserId == gameAccount).ExecuteDeleteAsync();
        }
    }

    private static async Task<string> GameSchemaFingerprint(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT COALESCE(jsonb_agg(row ORDER BY row.table_name,row.ordinal_position)::text,'[]')
            FROM (SELECT table_name,column_name,ordinal_position,data_type,is_nullable,column_default
                  FROM information_schema.columns WHERE table_schema='public') row
            """, connection);
        return (string) await command.ExecuteScalarAsync();
    }
}
