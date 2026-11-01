using Robust.Shared.GameStates;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Marks a damaged drone spawned by a robotics repair order so the console can
/// verify that this exact unit was repaired and returned.
/// </summary>
[RegisterComponent]
public sealed partial class CMURoboticsOrderTargetComponent : Component
{
    [DataField]
    public int OrderId;
}
