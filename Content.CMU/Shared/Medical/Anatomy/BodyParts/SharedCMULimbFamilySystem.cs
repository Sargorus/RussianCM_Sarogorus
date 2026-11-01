using System;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Humanoid;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts;

/// <summary>
/// Resolves the species family of a body and its detached limbs so surgery can
/// reject cross-species attachments before they reach the body.
/// </summary>
public sealed partial class SharedCMULimbFamilySystem : EntitySystem
{
    public CMULimbFamily GetBodyFamily(EntityUid body)
    {
        if (HasComp<CMUDroneAndroidComponent>(body))
            return CMULimbFamily.DroneAndroid;

        if (TryComp<HumanoidProfileComponent>(body, out var profile))
        {
            if (profile.Species.Id == "WorkingJoe")
                return CMULimbFamily.WorkingJoe;
            if (profile.Species.Id == "DroneAndroid")
                return CMULimbFamily.DroneAndroid;
            if (profile.Species.Id == "Yautja")
                return CMULimbFamily.Yautja;
        }

        if (HasComp<SynthComponent>(body))
            return CMULimbFamily.Synth;

        return CMULimbFamily.Human;
    }

    public CMULimbFamily GetLimbFamily(EntityUid limb)
    {
        if (TryComp<CMULimbFamilyComponent>(limb, out var family))
            return family.Family;

        var id = MetaData(limb).EntityPrototype?.ID;
        if (id == null)
            return CMULimbFamily.Other;

        if (id.StartsWith("CMUPartWorkingJoe", StringComparison.Ordinal))
            return CMULimbFamily.WorkingJoe;
        if (id.StartsWith("CMUPartDroneAndroid", StringComparison.Ordinal))
            return CMULimbFamily.DroneAndroid;
        if (id.StartsWith("CMUPartSynth", StringComparison.Ordinal))
            return CMULimbFamily.Synth;
        if (id.StartsWith("CMUPartRobotic", StringComparison.Ordinal))
            return CMULimbFamily.Prosthetic;
        if (id.StartsWith("CMUPartHuman", StringComparison.Ordinal))
            return CMULimbFamily.Human;
        if (id.StartsWith("CMUPartYautja", StringComparison.Ordinal))
            return CMULimbFamily.Yautja;

        return CMULimbFamily.Other;
    }

    /// <summary>
    /// Whether <paramref name="limb"/> may be attached to <paramref name="body"/>.
    /// Drones and Working Joes only accept their own limbs; ordinary synths and
    /// humans also accept their original family and robotic prosthetics.
    /// </summary>
    public bool CanAcceptLimb(EntityUid body, EntityUid limb)
    {
        var bodyFamily = GetBodyFamily(body);
        var limbFamily = GetLimbFamily(limb);
        return bodyFamily switch
        {
            CMULimbFamily.DroneAndroid => limbFamily == CMULimbFamily.DroneAndroid,
            CMULimbFamily.WorkingJoe => limbFamily == CMULimbFamily.WorkingJoe,
            CMULimbFamily.Synth => limbFamily is CMULimbFamily.Synth
                or CMULimbFamily.Human
                or CMULimbFamily.Prosthetic,
            CMULimbFamily.Human => limbFamily is CMULimbFamily.Human
                or CMULimbFamily.Synth
                or CMULimbFamily.Prosthetic
                or CMULimbFamily.Other,
            CMULimbFamily.Yautja => limbFamily is CMULimbFamily.Yautja
                or CMULimbFamily.Prosthetic
                or CMULimbFamily.Other,
            _ => true,
        };
    }
}
