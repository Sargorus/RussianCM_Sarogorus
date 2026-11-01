using System.Collections.Generic;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Robotics;

/// <summary>
/// Spawns an android in a repair-order state: some limbs are missing and the
/// frame has both brute damage and burn damage. The roboticist must attach new
/// limbs and repair the damage before the unit can be delivered.
/// </summary>
[RegisterComponent]
public sealed partial class CMURoboticsDamagedBodyComponent : Component
{
    [DataField]
    public List<BodyPartType> SeverParts = new()
    {
        BodyPartType.Arm,
        BodyPartType.Leg,
    };

    [DataField]
    public FixedPoint2 BurnDamage = FixedPoint2.New(45);

    [DataField]
    public FixedPoint2 BruteDamage = FixedPoint2.New(25);
}
