using Content.Shared.CMU14.DroneOperator;
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
    public List<CMUDroneConfigurationOption> Factions { get; }
    public HashSet<string> SelectedFactions { get; }

    public CMUMedibotConfigurationBuiState(
        int injectAmount,
        bool injectOnBrute,
        bool injectOnBurn,
        bool injectOnCritical,
        bool hasBeaker,
        string beakerText,
        List<CMUDroneConfigurationOption> factions,
        HashSet<string> selectedFactions)
    {
        InjectAmount = injectAmount;
        InjectOnBrute = injectOnBrute;
        InjectOnBurn = injectOnBurn;
        InjectOnCritical = injectOnCritical;
        HasBeaker = hasBeaker;
        BeakerText = beakerText;
        Factions = factions;
        SelectedFactions = selectedFactions;
    }
}

[Serializable, NetSerializable]
public sealed class CMUMedibotConfigurationApplyMsg : BoundUserInterfaceMessage
{
    public int InjectAmount { get; }
    public bool InjectOnBrute { get; }
    public bool InjectOnBurn { get; }
    public bool InjectOnCritical { get; }
    public HashSet<string> Factions { get; }

    public CMUMedibotConfigurationApplyMsg(
        int injectAmount,
        bool injectOnBrute,
        bool injectOnBurn,
        bool injectOnCritical,
        HashSet<string> factions)
    {
        InjectAmount = injectAmount;
        InjectOnBrute = injectOnBrute;
        InjectOnBurn = injectOnBurn;
        InjectOnCritical = injectOnCritical;
        Factions = factions;
    }
}
