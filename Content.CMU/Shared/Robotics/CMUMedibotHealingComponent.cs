using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Robotics;

/// <summary>
/// Field-treatment parameters for the CMU medical bot. A successful injection
/// copies these values onto the patient's <see cref="CMUMedibotTreatmentComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUMedibotHealingComponent : Component
{
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
