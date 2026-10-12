using System.Diagnostics;
using System.IO;
using Content.Server._RuCM.Qualifications;
using Content.Server.Database;
using Content.Shared._RuCM.Qualifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Content.IntegrationTests.CMU14.Qualifications;

[TestFixture]
public sealed class CMUQualificationRefreshTest
{
    [Test]
    public async Task UnchangedSqliteRevisionsSkipHistoryAndChangedOrMissingStateIsDetected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cmu-qualification-refresh-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString;
        using var providerSetup = new SqliteServerDbContext(
            new DbContextOptionsBuilder<SqliteServerDbContext>().UseSqlite(connectionString).Options);
        try
        {
            using (var create = new SqliteConnection(connectionString)) create.Open();
            var repository = new SqliteQualificationRepository(connectionString);
            Assert.That(await repository.Load(), Is.Null);
            var seed = new QualificationStore { Revision = 1 };
            for (var i = 0; i < 5000; i++)
            {
                var id = Guid.NewGuid();
                seed.Players[id] = new() { Player = id };
            }
            await repository.Save(seed, 0);
            Assert.That(await repository.LoadIfChanged(1), Is.Null);
            var timer = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++) Assert.That(await repository.LoadIfChanged(1), Is.Null);
            timer.Stop();
            TestContext.Progress.WriteLine($"20 unchanged SQLite revision polls (5000 players): {timer.Elapsed.TotalMilliseconds:F3} ms.");
            var changed = seed.Clone();
            var manager = Guid.NewGuid();
            changed.Management.Add(manager);
            changed.Revision++;
            await new SqliteQualificationRepository(connectionString).Save(changed, 1);
            Assert.That((await repository.LoadIfChanged(1))!.Management, Does.Contain(manager));
            Assert.That(await repository.LoadIfChanged(2), Is.Null);
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var delete = new SqliteCommand("DELETE FROM rucm_training_state WHERE id=1", connection);
            delete.ExecuteNonQuery();
            Assert.ThrowsAsync<QualificationValidationException>(() => repository.LoadIfChanged(2));
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }
}
