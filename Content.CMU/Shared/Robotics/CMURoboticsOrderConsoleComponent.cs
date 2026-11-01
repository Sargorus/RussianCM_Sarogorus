using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.CMU14.Robotics;

/// <summary>
/// A corporate cargo terminal that offers robotics work orders. Build orders
/// ask for a specific android model; repair orders deliver a crate of damaged
/// units to the corporate cargo elevator. Finished units are delivered by
/// placing them on the raised cargo platform and lowering it.
/// </summary>
[RegisterComponent]
public sealed partial class CMURoboticsOrderConsoleComponent : Component
{
    [DataField]
    public float DeliveryRadius = 2f;

    [DataField]
    public int MaxAvailableOrders = 3;

    [DataField]
    public List<CMURoboticsOrderTarget> BuildTargets = new();

    [DataField]
    public List<CMURoboticsOrderTarget> RepairTargets = new();

    [ViewVariables]
    public List<CMURoboticsOrder> AvailableOrders = new();

    [ViewVariables]
    public CMURoboticsOrder? ActiveOrder;

    [ViewVariables]
    public string Status = string.Empty;

    [ViewVariables]
    public int NextOrderId = 1;
}

[DataDefinition]
public sealed partial class CMURoboticsOrderTarget
{
    [DataField]
    public EntProtoId Prototype = string.Empty;

    [DataField]
    public int Reward = 300;

    /// <summary>
    /// Optional android modules the customer wants installed. One is picked at
    /// random when the order is generated.
    /// </summary>
    [DataField]
    public List<EntProtoId> RequiredModules = new();
}

public sealed class CMURoboticsOrder
{
    public int Id;
    public CMURoboticsOrderType Type;
    public EntProtoId Target = string.Empty;
    public EntProtoId? RequiredModule;
    public int Reward;
    public int Amount = 1;
    public string TargetName = string.Empty;
    public EntityUid? Elevator;
    public EntityUid? RepairCrate;
}
