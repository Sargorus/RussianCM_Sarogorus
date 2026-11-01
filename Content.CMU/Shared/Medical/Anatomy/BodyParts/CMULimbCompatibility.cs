using System;
using Content.Shared.CMU14.DroneOperator;
using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts;

/// <summary>
/// Prototype-based limb compatibility used by surgery and robotics contracts.
/// Drone android limbs are only valid on drone androids; every other body
/// rejects them. This keeps the drone, Working Joe, synth and human limb sets
/// separate without a runtime family component.
/// </summary>
public static class CMULimbCompatibility
{
    public const string DroneLimbPrefix = "CMUPartDroneAndroid";
    public const string WorkingJoeLimbPrefix = "CMUPartWorkingJoe";

    public static bool IsDroneLimb(IEntityManager entMan, EntityUid limb)
    {
        var id = entMan.GetComponent<MetaDataComponent>(limb).EntityPrototype?.ID;
        return id?.StartsWith(DroneLimbPrefix, StringComparison.Ordinal) == true;
    }

    public static bool IsWorkingJoeLimb(IEntityManager entMan, EntityUid limb)
    {
        var id = entMan.GetComponent<MetaDataComponent>(limb).EntityPrototype?.ID;
        return id?.StartsWith(WorkingJoeLimbPrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Whether <paramref name="limb"/> may be attached to <paramref name="body"/>.
    /// Drone bodies accept only drone android limbs. Non-drone bodies accept any
    /// limb except drone android parts, preserving the old surgery behavior for
    /// humans, synths and Working Joes.
    /// </summary>
    public static bool CanAttach(IEntityManager entMan, EntityUid body, EntityUid limb)
    {
        if (entMan.HasComponent<CMUDroneAndroidComponent>(body))
            return IsDroneLimb(entMan, limb);

        return !IsDroneLimb(entMan, limb);
    }
}
