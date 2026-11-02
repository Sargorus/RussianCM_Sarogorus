using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Robotics;

/// <summary>
/// Passive field treatment for the CMU medical bot. While a patient stays still
/// nearby, the bot applies suture-equivalent brute healing and mesh-equivalent
/// burn healing without consuming any item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUMedibotHealingComponent : Component
{
    [DataField]
    public float Range = 3f;

    [DataField]
    public TimeSpan StartDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(2);

    [DataField]
    public FixedPoint2 BruteHeal = 10;

    [DataField]
    public FixedPoint2 BurnHeal = 10;

    [DataField]
    public float MaxPatientSpeed = 0.15f;
}
