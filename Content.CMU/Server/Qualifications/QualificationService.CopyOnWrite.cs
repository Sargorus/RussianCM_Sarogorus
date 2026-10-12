using System.Collections.Generic;
using Content.Shared._RuCM.Qualifications;

namespace Content.Server._RuCM.Qualifications;

public sealed partial class QualificationService
{
    /// <summary>
    /// Player histories in published stores are immutable. Copy their index once and copy an individual
    /// history only through Player() before mutation. Metadata is still deeply copied.
    /// </summary>
    private static QualificationStore CloneForMutation(QualificationStore current)
    {
        var metadata = new QualificationStore
        {
            Revision = current.Revision,
            Definitions = current.Definitions,
            Roles = current.Roles,
            Instructors = current.Instructors,
            Management = current.Management,
            OfficerJobs = current.OfficerJobs,
            CommandingOfficerJobs = current.CommandingOfficerJobs,
            Notes = current.Notes,
            Suspensions = current.Suspensions,
            Audit = current.Audit,
            Migrations = current.Migrations,
            Participation = current.Participation,
            MigrationGroups = current.MigrationGroups,
            TrackerAliases = current.TrackerAliases,
        }.Clone();
        metadata.Players = new(current.Players);
        return metadata;
    }

    private static PlayerTrainingState ClonePlayer(PlayerTrainingState player) =>
        new()
        {
            Player = player.Player,
            FirstTrainingAt = player.FirstTrainingAt,
            Grants = CopyGrants(player),
            Progress = CopyProgress(player),
        };

    private static Dictionary<string, QualificationGrant> CopyGrants(PlayerTrainingState player)
    {
        var grants = new Dictionary<string, QualificationGrant>(player.Grants.Count);
        foreach (var (id, grant) in player.Grants)
            grants[id] = grant with { RequiredItems = (string[]) grant.RequiredItems.Clone() };
        return grants;
    }

    private static Dictionary<string, Dictionary<string, ChecklistCompletion>> CopyProgress(PlayerTrainingState player)
    {
        var progress = new Dictionary<string, Dictionary<string, ChecklistCompletion>>(player.Progress.Count);
        foreach (var (id, items) in player.Progress)
            progress[id] = new(items);
        return progress;
    }
}
