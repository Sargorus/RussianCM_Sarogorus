using Content.Shared._RMC14.Dialog;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

[RegisterComponent]
public sealed partial class CMULiaisonSafeComponent : Component
{
    /// <summary>
    /// The safe's PIN. Zero until a corporate liaison spawns, so the safe remains
    /// access-only when nobody can receive the code.
    /// </summary>
    public int Pin;

    public EntityUid? PendingUser;
}

[RegisterComponent]
public sealed partial class CMULiaisonPinComponent : Component
{
    public int Pin;
}

[Serializable, NetSerializable]
public sealed record CMULiaisonSafePinEvent(string Message = "") : DialogInputEvent(Message);
