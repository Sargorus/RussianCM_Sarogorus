using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.DroneOperator;

[Serializable, NetSerializable]
public enum CMUDroneConfigurationUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUDroneConfigurationOption
{
    public string Id { get; }
    public string Name { get; }

    public CMUDroneConfigurationOption(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class CMUDroneConfigurationBuiState : BoundUserInterfaceState
{
    public List<CMUDroneConfigurationOption> Factions { get; }
    public string CurrentFaction { get; }
    public List<CMUDroneConfigurationOption> Channels { get; }
    public HashSet<string> SelectedChannels { get; }
    public List<CMUDroneConfigurationOption> Access { get; }
    public HashSet<string> SelectedAccess { get; }
    public bool HasIdCard { get; }

    public CMUDroneConfigurationBuiState(
        List<CMUDroneConfigurationOption> factions,
        string currentFaction,
        List<CMUDroneConfigurationOption> channels,
        HashSet<string> selectedChannels,
        List<CMUDroneConfigurationOption> access,
        HashSet<string> selectedAccess,
        bool hasIdCard)
    {
        Factions = factions;
        CurrentFaction = currentFaction;
        Channels = channels;
        SelectedChannels = selectedChannels;
        Access = access;
        SelectedAccess = selectedAccess;
        HasIdCard = hasIdCard;
    }
}

[Serializable, NetSerializable]
public sealed class CMUDroneConfigurationApplyMsg : BoundUserInterfaceMessage
{
    public string Faction { get; }
    public HashSet<string> Channels { get; }
    public HashSet<string> Access { get; }

    public CMUDroneConfigurationApplyMsg(string faction, HashSet<string> channels, HashSet<string> access)
    {
        Faction = faction;
        Channels = channels;
        Access = access;
    }
}
