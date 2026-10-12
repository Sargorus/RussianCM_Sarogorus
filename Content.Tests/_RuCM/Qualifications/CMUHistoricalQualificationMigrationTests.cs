// CMU14: historical qualification migration.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._RuCM.Qualifications;
using Content.Shared._RuCM.Qualifications;
using NUnit.Framework;

namespace Content.Tests._RuCM.Qualifications;

[TestFixture]
public sealed class CMUHistoricalQualificationMigrationTests
{
    private static QualificationStore Seed()
    {
        var seed = new QualificationStore();
        foreach (var id in QualificationRules.Levels.Concat(new[] { "medical", "commanding_officer" }))
            seed.Definitions[id] = new() { Id = id, Name = id };
        seed.Roles["rifle"] = new() { JobId = "rifle", Govfor = true, Tracker = "rifle", Enabled = false };
        seed.Roles["support"] = new() { JobId = "support", Govfor = true, Tracker = "support" };
        seed.Roles["sergeant"] = new() { JobId = "sergeant", Govfor = true, Tracker = "sergeant", MinimumLevel = MilitaryLevel.Sergeant };
        seed.Roles["officer"] = new() { JobId = "officer", Govfor = true, Tracker = "officer", MinimumLevel = MilitaryLevel.Officer };
        seed.Roles["medic"] = new() { JobId = "medic", Govfor = true, Tracker = "medic", Professional = new() { "medical" } };
        seed.Roles["synth"] = new() { JobId = "synth", Govfor = true, Synthetic = true, Tracker = "synth", MinimumLevel = MilitaryLevel.Officer, Professional = new() { "medical" } };
        seed.Roles["co"] = new() { JobId = "co", Govfor = true, Tracker = "co", MinimumLevel = MilitaryLevel.Officer, Professional = new() { "commanding_officer" } };
        seed.Roles["outsider"] = new() { JobId = "outsider", Tracker = "outsider", MinimumLevel = MilitaryLevel.Officer, Professional = new() { "medical" } };
        seed.CommandingOfficerJobs.Add("co");
        return seed;
    }

    [TestCase(3, false)]
    [TestCase(3.000277777777778, true)]
    [TestCase(2.999722222222222, false)]
    public async Task EnlistedHasStrictTotalHumanHoursThreshold(double hours, bool enlisted)
    {
        var service = new QualificationService(new MemoryQualificationRepository()); await service.Initialize(Seed());
        var player = Guid.NewGuid();
        var plan = service.MigrationDryRun(new[] { new MigrationCandidate(player, new() { ["rifle"] = hours / 2, ["support"] = hours / 2 }) }, DateTimeOffset.UtcNow);
        Assert.That(plan.Grants.GetValueOrDefault(player)?.Contains("enlisted") ?? false, Is.EqualTo(enlisted));
        Assert.That(plan.AccountsScanned, Is.EqualTo(1));
    }

    [TestCase("sergeant", 4.999, "enlisted")]
    [TestCase("sergeant", 5, "enlisted,sergeant")]
    [TestCase("officer", 9.999, "enlisted")]
    [TestCase("officer", 10, "enlisted,sergeant,officer")]
    [TestCase("medic", 4.999, "enlisted")]
    [TestCase("medic", 5, "enlisted,medical")]
    [TestCase("co", 100, "enlisted")]
    [TestCase("synth", 100, "")]
    [TestCase("outsider", 100, "")]
    public async Task LevelsProfessionsAndExcludedRoles(string tracker, double hours, string expected)
    {
        var service = new QualificationService(new MemoryQualificationRepository()); await service.Initialize(Seed());
        var player = Guid.NewGuid();
        var plan = service.MigrationDryRun(new[] { new MigrationCandidate(player, new() { [tracker] = hours }) }, DateTimeOffset.UtcNow);
        Assert.That(plan.Grants.GetValueOrDefault(player) ?? new(), Is.EquivalentTo(expected.Split(',', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Test]
    public async Task AliasesSharedTrackersAndInvalidTimersCannotInflateHumanService()
    {
        var seed = Seed();
        seed.TrackerAliases["legacy"] = "rifle";
        seed.Roles["support"].Tracker = "legacy";
        seed.TrackerAliases["synth"] = "medic";
        seed.MigrationGroups["medical"] = new() { "synth", "medic", "outsider" };
        var service = new QualificationService(new MemoryQualificationRepository()); await service.Initialize(seed);
        var player = Guid.NewGuid();
        var plan = service.MigrationDryRun(new[] { new MigrationCandidate(player, new() { ["legacy"] = 2, ["synth"] = 100, ["medic"] = 100, ["officer"] = double.NaN, ["sergeant"] = double.PositiveInfinity, ["outsider"] = 100 }) }, DateTimeOffset.UtcNow);
        Assert.That(plan.Grants, Is.Empty, "Alias sharing must not double count 2 hours; synthetic aliases cannot prove medical service");
    }

    [TestCase(QualificationStatus.Suspended)]
    [TestCase(QualificationStatus.Revoked)]
    public async Task DryRunAndRestartPreserveExistingRestrictionsAndOneTimeMarker(QualificationStatus status)
    {
        var seed = Seed(); var player = Guid.NewGuid(); var manager = Guid.NewGuid();
        var context = new TrainingContext(manager, "test", "test", 1, "test", DateTimeOffset.UtcNow);
        seed.Players[player] = new() { Player = player, Grants = new() { ["enlisted"] = new("enlisted", status, 1, Array.Empty<string>(), context, "test") } };
        var repository = new MemoryQualificationRepository();
        var service = new QualificationService(repository); await service.Initialize(seed);
        var before = System.Text.Json.JsonSerializer.Serialize(service.Snapshot());
        var plan = service.MigrationDryRun(new[] { new MigrationCandidate(player, new() { ["officer"] = 10, ["medic"] = 5 }), new(Guid.NewGuid(), new()), new(Guid.Empty, new()) }, context.At);
        Assert.That(plan.AccountsScanned, Is.EqualTo(2));
        Assert.That(plan.Grants[player], Does.Not.Contain("enlisted"));
        Assert.That(System.Text.Json.JsonSerializer.Serialize(service.Snapshot()), Is.EqualTo(before));
        var authority = new QualificationAuthority(context, true, false, false, false, false);
        await service.ExecuteMigration(authority, plan);
        Assert.That(service.Snapshot().Players[player].Grants["enlisted"].Status, Is.EqualTo(status));
        var after = System.Text.Json.JsonSerializer.Serialize(service.Snapshot());
        var restarted = new QualificationService(repository); await restarted.Initialize(seed);
        await restarted.ExecuteMigration(authority, plan);
        Assert.That(System.Text.Json.JsonSerializer.Serialize(restarted.Snapshot()), Is.EqualTo(after));
        Assert.That(restarted.MigrationDryRun(new[] { new MigrationCandidate(Guid.NewGuid(), new() { ["rifle"] = 100 }) }, context.At).Grants, Is.Empty);
    }
}
