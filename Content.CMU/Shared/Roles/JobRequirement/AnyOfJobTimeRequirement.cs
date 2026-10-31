using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Roles;

/// <summary>
/// Passes when the player has at least <see cref="Time"/> on any one of <see cref="Jobs"/>.
/// Unlike TotalJobsTimeRequirement, this does not sum time across jobs.
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class AnyOfJobTimeRequirement : JobRequirement
{
    /// <summary>
    /// Jobs that can satisfy the requirement. Time is checked per job, not summed.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<JobPrototype>> Jobs = new();

    /// <summary>
    /// How long (in seconds) the player needs on one of the jobs.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Time;

    public override bool Check(
        IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        var reasons = new List<FormattedMessage>();
        var checkedTrackers = new HashSet<ProtoId<PlayTimeTrackerPrototype>>();

        foreach (var jobId in Jobs)
        {
            if (!protoManager.TryIndex(jobId, out var job))
                continue;

            // OPFOR corpsmen and some other variants share a tracker with their base job.
            if (!checkedTrackers.Add(job.PlayTimeTracker))
                continue;

            var requirement = new RoleTimeRequirement
            {
                Role = job.PlayTimeTracker,
                Time = Time,
            };

            if (requirement.Check(entManager, protoManager, profile, playTimes, out var childReason))
            {
                reason = null;
                return true;
            }

            if (childReason != null)
                reasons.Add(childReason);
        }

        if (reasons.Count == 0)
        {
            reason = new FormattedMessage();
            return false;
        }

        var combined = new FormattedMessage();
        for (var i = 0; i < reasons.Count; i++)
        {
            if (i > 0)
                combined.PushNewline();

            combined.AddMessage(reasons[i]);
        }

        reason = combined;
        return false;
    }
}
