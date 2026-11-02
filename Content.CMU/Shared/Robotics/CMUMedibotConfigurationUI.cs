using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Robotics;

[Serializable, NetSerializable]
public enum CMUMedibotConfigurationUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUMedibotConfigurationBuiState : BoundUserInterfaceState
{
    public int InjectAmount { get; }
    public bool InjectOnBrute { get; }
    public bool InjectOnBurn { get; }
    public bool InjectOnCritical { get; }
    public bool HasBeaker { get; }
    public string BeakerText { get; }

    public CMUMedibotConfigurationBuiState(
        int injectAmount,
        bool injectOnBrute,
        bool injectOnBurn,
        bool injectOnCritical,
        bool hasBeaker,
        string beakerText)
    {
        InjectAmount = injectAmount;
        InjectOnBrute = injectOnBrute;
        InjectOnBurn = injectOnBurn;
        InjectOnCritical = injectOnCritical;
        HasBeaker = hasBeaker;
        BeakerText = beakerText;
    }
}

[Serializable, NetSerializable]
public sealed class CMUMedibotConfigurationApplyMsg : BoundUserInterfaceMessage
{
    public int InjectAmount { get; }
    public bool InjectOnBrute { get; }
    public bool InjectOnBurn { get; }
    public bool InjectOnCritical { get; }

    public CMUMedibotConfigurationApplyMsg(
        int injectAmount,
        bool injectOnBrute,
        bool injectOnBurn,
        bool injectOnCritical)
    {
        InjectAmount = injectAmount;
        InjectOnBrute = injectOnBrute;
        InjectOnBurn = injectOnBurn;
        InjectOnCritical = injectOnCritical;
    }
}
