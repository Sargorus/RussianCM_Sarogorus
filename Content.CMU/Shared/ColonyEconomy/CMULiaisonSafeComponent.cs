using Content.Shared._RMC14.Dialog;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

[RegisterComponent]
public sealed partial class CMULiaisonSafeComponent : Component
{
    /// <summary>
    /// Round-scoped safe PIN, shared by every liaison safe and handed to the
    /// corporate liaison when that role spawns.
    /// </summary>
    public int Pin;

    /// <summary>
    /// Destruction threshold in damage points, applied over the inherited
    /// locker threshold.
    /// </summary>
    [DataField]
    public int HitPoints = 2000;
}

[RegisterComponent]
public sealed partial class CMULiaisonPinComponent : Component
{
    public int Pin;
}

[Serializable, NetSerializable]
public sealed record CMULiaisonSafePinEvent(NetEntity Actor, string Message = "") : DialogInputEvent(Message);
