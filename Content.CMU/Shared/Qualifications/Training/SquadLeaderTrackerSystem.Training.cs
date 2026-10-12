using Content.Shared.CMU14.Qualifications.Training;
namespace Content.Shared._RMC14.Tracker.SquadLeader;
public sealed partial class SquadLeaderTrackerSystem
{
    // Native squad tracker HUD, direction math, rotation handling and severity icons.
    private bool CMUUpdateTrainingNavigation(Entity<SquadLeaderTrackerComponent> ent)
    {
        if (!TryComp<CMUTrainingNavigationComponent>(ent, out var training))
            return false;
        if (training.Coordinates is { } coordinates &&
            _transform.GetMapCoordinates(ent.Owner).MapId == coordinates.MapId)
            _alerts.ShowAlert(ent.Owner, training.Instructor ? "CMUTrainingInstructor" : "CMUTrainingRecruit",
                _tracker.GetAlertSeverity(ent.Owner, coordinates));
        else
            _alerts.ClearAlertCategory((ent.Owner, null), SquadTrackerCategory);
        return true;
    }
    public void ClearCMUTrainingNavigation(EntityUid owner)
    {
        _alerts.ClearAlertCategory((owner, null), SquadTrackerCategory);
        if (TryComp<SquadLeaderTrackerComponent>(owner, out var tracker))
            tracker.UpdateAt = default;
    }
}
