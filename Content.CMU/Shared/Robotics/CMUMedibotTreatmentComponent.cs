using System.Numerics;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Robotics;

/// <summary>
/// Field-treatment state placed on a patient by a CMU medical bot. The patient
/// must stay still for the initial delay, then receives periodic suture/mesh
/// healing without consuming items and without requiring the bot to stay close.
/// </summary>
[RegisterComponent]
public sealed partial class CMUMedibotTreatmentComponent : Component
{
    public EntityUid? Healer;
    public Vector2 Position;
    public TimeSpan StartDelay = TimeSpan.FromSeconds(3);
    public TimeSpan Interval = TimeSpan.FromSeconds(2);
    public TimeSpan NextHeal;
    public FixedPoint2 BruteHeal = 10;
    public FixedPoint2 BurnHeal = 10;
    public float MaxPatientSpeed = 0.15f;
}
