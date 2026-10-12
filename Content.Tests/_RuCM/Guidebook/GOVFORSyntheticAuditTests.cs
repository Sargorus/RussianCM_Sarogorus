using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server._RuCM.Qualifications;
using Content.Shared._RuCM.Qualifications;
using NUnit.Framework;

namespace Content.Tests._RuCM.Guidebook;

/// <summary>Characterizes the existing admission/migration gap; this documentation change does not alter policy or mechanics.</summary>
public sealed class GOVFORSyntheticAuditTests
{
    [TestCase("AU14JobGOVFORAuxSupportSynth")]
    [TestCase("AU14JobGOVFORAuxSupportSynthRMC")]
    [TestCase("AU14JobGOVFORAuxSupportSynthUPP")]
    public async Task SyntheticAdmissionIsUngatedButParticipationCanProposeHumanEnlisted(string job)
    {
        var yaml = File.ReadAllText(GOVFORRegulationsTests.Find(
            "Content.CMU/Resources/Prototypes/CMU14/Roles/Military/GovFor/Platoon/Synthetics/AuxSupportSynth.yml"));
        Assert.That(Regex.IsMatch(yaml, @"(?m)^- type: job\r?\n(?:(?!^- type:).|\n)*?^  id: " + Regex.Escape(job) + @"\r?$"), Is.True, "Use an actual job prototype");
        var seed = JsonSerializer.Deserialize<QualificationStore>(File.ReadAllText(
            GOVFORRegulationsTests.Find("Resources/RuCM/Qualifications/seed.json")));
        Assert.That(seed.Roles.ContainsKey(job), Is.False);
        // Mirrors the public server initialization of concrete, unconfigured GOVFOR jobs.
        seed.Roles[job] = new() { JobId = job, Enabled = false, Govfor = true, Tracker = "AU14JobGOVFORAuxSupportSynth" };
        var service = new QualificationService(new MemoryQualificationRepository());
        await service.Initialize(seed);
        var player = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;
        Assert.That(service.CanTakeJob(player, job).Allowed, Is.True);
        Assert.That(service.IsRoleEnabled(job), Is.False);
        Assert.That(service.MigrationDryRun(new[] { new MigrationCandidate(player, new()) }, at).Grants, Is.Empty);
        await service.RecordParticipation(new(player, job, at, 42, "audit"));
        Assert.That(service.GetPlayerTrainingState(player).Grants, Is.Empty, "Joining never directly grants human training");
        var plan = service.MigrationDryRun(new[] { new MigrationCandidate(player, new Dictionary<string, double>()) }, at);
        Assert.That(plan.Grants[player], Does.Contain("enlisted"), "Known migration leak: disabled synthetic participation still counts as GOVFOR");
        Assert.That(service.GetPlayerTrainingState(player).Grants, Is.Empty, "Dry-run must remain read-only");
    }
}
