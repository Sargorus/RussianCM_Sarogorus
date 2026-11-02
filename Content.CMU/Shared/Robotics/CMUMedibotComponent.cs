using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Robotics;

/// <summary>
/// Makes a medibot draw its injection from a beaker item slot and lets a drone
/// configuration key tune the dose and the damage types that trigger it.
/// </summary>
[RegisterComponent]
public sealed partial class CMUMedibotComponent : Component
{
    [DataField]
    public string BeakerSlotId = "cmu-medibot-beaker";

    [DataField]
    public FixedPoint2 InjectAmount = 15;

    [DataField]
    public bool InjectOnBrute = true;

    [DataField]
    public bool InjectOnBurn = true;

    [DataField]
    public bool InjectOnCritical = true;

    public EntityUid? ConfigUser;

    public EntityUid? ConfigKey;
}
