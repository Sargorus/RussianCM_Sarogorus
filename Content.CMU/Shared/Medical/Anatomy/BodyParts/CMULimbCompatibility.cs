using System;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.DroneOperator;
using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts;

/// <summary>
/// Prototype- and component-based limb compatibility used by surgery and
/// robotics contracts. Drone android limbs are only valid on drone androids;
/// every other body rejects them. Fabricator-printed frame arms and legs carry
/// both <see cref="CMUDroneAssemblyPartComponent"/> and a body part, so the
/// same item can be installed in a frame or attached surgically. This keeps
/// the drone, Working Joe, synth and human limb sets separate without a runtime
/// family component.
/// </summary>
public static class CMULimbCompatibility
{
    public const string DroneLimbPrefix = "CMUPartDroneAndroid";
    public const string WorkingJoeLimbPrefix = "CMUPartWorkingJoe";

    public static bool IsDroneLimb(IEntityManager entMan, EntityUid limb)
    {
        var id = entMan.GetComponent<MetaDataComponent>(limb).EntityPrototype?.ID;
        if (id?.StartsWith(DroneLimbPrefix, StringComparison.Ordinal) == true)
            return true;

        // The fabricator prints the frame assembly parts. Those same items are
        // body parts, so they can repair a drone without a second limb set.
        return entMan.HasComponent<CMUDroneAssemblyPartComponent>(limb) &&
               entMan.HasComponent<BodyPartComponent>(limb);
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
