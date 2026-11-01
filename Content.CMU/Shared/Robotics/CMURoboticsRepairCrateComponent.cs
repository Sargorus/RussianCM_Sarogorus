using Robust.Shared.ViewVariables;

namespace Content.Shared.CMU14.Robotics;

/// <summary>
/// A crate delivered for a robotics repair order. Its contents are the damaged
/// androids; when it is opened, they are marked as the order's targets.
/// </summary>
[RegisterComponent]
public sealed partial class CMURoboticsRepairCrateComponent : Component
{
    [DataField]
    public int OrderId;

    [ViewVariables]
    public bool Marked;
}
