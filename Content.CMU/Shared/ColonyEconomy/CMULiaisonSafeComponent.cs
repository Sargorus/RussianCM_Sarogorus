using Content.Shared._RMC14.Dialog;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

[RegisterComponent]
public sealed partial class CMULiaisonSafeComponent : Component
{
    /// <summary>
    /// The safe's PIN. An empty string means the safe has no PIN and works as a
    /// normal open/close storage.
    /// </summary>
    public string Pin = string.Empty;

    /// <summary>
    /// Destruction threshold in damage points, applied over the inherited
    /// locker threshold.
    /// </summary>
    [DataField]
    public int HitPoints = 2000;

    /// <summary>
    /// Actor that entered the current PIN and may now choose a new one.
    /// </summary>
    public EntityUid? PinChangeUser;

    /// <summary>
    /// Whether <see cref="PinChangeUser"/> successfully verified the current PIN.
    /// </summary>
    public bool PinChangeVerified;
}

[Serializable, NetSerializable]
public enum CMULiaisonSafePinAction : byte
{
    Unlock,
    Set,
    VerifyCurrent,
}

[Serializable, NetSerializable]
public sealed record CMULiaisonSafePinEvent(
    NetEntity Actor,
    CMULiaisonSafePinAction Action,
    string Message = "") : DialogInputEvent(Message);
