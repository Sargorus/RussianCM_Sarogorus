using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Robotics;

[Serializable, NetSerializable]
public enum CMURoboticsOrderConsoleUiKey
{
    Key,
}

[Serializable, NetSerializable]
public enum CMURoboticsOrderType : byte
{
    Build,
    Repair,
}

[Serializable, NetSerializable]
public sealed class CMURoboticsOrderInfo
{
    public int Id { get; }
    public CMURoboticsOrderType Type { get; }
    public string TargetName { get; }
    public string? RequiredModuleName { get; }
    public int Reward { get; }
    public int Amount { get; }
    public int SecondsRemaining { get; }

    public CMURoboticsOrderInfo(
        int id,
        CMURoboticsOrderType type,
        string targetName,
        string? requiredModuleName,
        int reward,
        int amount,
        int secondsRemaining)
    {
        Id = id;
        Type = type;
        TargetName = targetName;
        RequiredModuleName = requiredModuleName;
        Reward = reward;
        Amount = amount;
        SecondsRemaining = secondsRemaining;
    }
}

[Serializable, NetSerializable]
public sealed class CMURoboticsOrderConsoleState : BoundUserInterfaceState
{
    public List<CMURoboticsOrderInfo> Available { get; }
    public CMURoboticsOrderInfo? Active { get; }
    public string Status { get; }

    public CMURoboticsOrderConsoleState(
        List<CMURoboticsOrderInfo> available,
        CMURoboticsOrderInfo? active,
        string status)
    {
        Available = available;
        Active = active;
        Status = status;
    }
}

[Serializable, NetSerializable]
public sealed class CMURoboticsAcceptOrderMessage(int id) : BoundUserInterfaceMessage
{
    public int Id { get; } = id;
}

[Serializable, NetSerializable]
public sealed class CMURoboticsCancelOrderMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMURoboticsPrintSlipMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMURoboticsRefreshOrdersMessage : BoundUserInterfaceMessage;
