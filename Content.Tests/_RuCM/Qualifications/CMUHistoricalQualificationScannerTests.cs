// CMU14: historical qualification migration.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._RuCM.Qualifications;
using Content.Server.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Content.Tests._RuCM.Qualifications;

[TestFixture]
public sealed class CMUHistoricalQualificationScannerTests
{
    [Test]
    public async Task FullScanIncludesOfflineZeroTimerAndTimerOnlyAccountsBeyondRosterLimitWithoutWrites()
    {
        var directory = Directory.CreateTempSubdirectory("cmu-historical-scan-");
        var path = Path.Combine(directory.FullName, "game.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString;
        try
        {
            // Match the game's provider initialization; only the two upstream tables are needed for this scanner fixture.
            using var providerSetup = new SqliteServerDbContext(new DbContextOptionsBuilder<SqliteServerDbContext>().UseSqlite(connectionString).Options);
            var timerOnly = Guid.NewGuid();
            var timed = Guid.NewGuid();
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var schema = connection.CreateCommand();
                schema.CommandText = "CREATE TABLE player(user_id TEXT PRIMARY KEY); CREATE TABLE play_time(player_id TEXT, tracker TEXT, time_spent TEXT);";
                await schema.ExecuteNonQueryAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await using var insert = connection.CreateCommand(); insert.Transaction = (SqliteTransaction) transaction;
                insert.CommandText = "INSERT INTO player VALUES ($id)";
                var id = insert.Parameters.AddWithValue("$id", timed);
                await insert.ExecuteNonQueryAsync();
                for (var i = 0; i < 10001; i++) { id.Value = Guid.NewGuid(); await insert.ExecuteNonQueryAsync(); }
                insert.CommandText = "INSERT INTO play_time VALUES ($id, 'rifle', $time)";
                insert.Parameters.AddWithValue("$time", TimeSpan.FromHours(3) + TimeSpan.FromSeconds(1));
                id.Value = timed; await insert.ExecuteNonQueryAsync();
                id.Value = timerOnly; await insert.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            var bytes = await File.ReadAllBytesAsync(path);
            var candidates = await new CMUHistoricalQualificationScanner(connectionString, true).Scan();
            Assert.That(candidates.Count, Is.EqualTo(10003));
            Assert.That(candidates.Count(c => c.TrackerHours.Count == 0), Is.EqualTo(10001));
            Assert.That(candidates.Single(c => c.Player == timed).TrackerHours["rifle"], Is.EqualTo((TimeSpan.FromHours(3) + TimeSpan.FromSeconds(1)).TotalHours));
            Assert.That(candidates.Any(c => c.Player == timerOnly), Is.True);
            Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(bytes), "Dry Run must not create tables, markers or grants");
        }
        finally
        {
            foreach (var file in directory.GetFiles()) file.Delete();
            directory.Delete();
        }
    }
}
