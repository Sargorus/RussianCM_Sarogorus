using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts;

/// <summary>
/// Identifies which synthetic species a limb belongs to. Surgery uses this to
/// keep drone android, Working Joe, prosthetic and ordinary synthetic limbs from
/// being attached to the wrong body.
/// </summary>
[RegisterComponent]
public sealed partial class CMULimbFamilyComponent : Component
{
    [DataField]
    public CMULimbFamily Family = CMULimbFamily.Other;
}

[Serializable, NetSerializable]
public enum CMULimbFamily : byte
{
    Other,
    Human,
    Synth,
    Prosthetic,
    WorkingJoe,
    DroneAndroid,
    Yautja,
}
