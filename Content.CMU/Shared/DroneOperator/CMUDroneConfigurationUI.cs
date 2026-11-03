using Content.Shared.CMU14.Configuration;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.DroneOperator;

[Serializable, NetSerializable]
public enum CMUDroneConfigurationUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUDroneConfigurationBuiState : BoundUserInterfaceState
{
    public List<CMUConfigurationOption> Factions { get; }
    public string CurrentFaction { get; }
    public List<CMUConfigurationOption> Channels { get; }
    public HashSet<string> SelectedChannels { get; }
    public List<CMUConfigurationOption> Access { get; }
    public HashSet<string> SelectedAccess { get; }
    public bool HasIdCard { get; }

    public CMUDroneConfigurationBuiState(
        List<CMUConfigurationOption> factions,
        string currentFaction,
        List<CMUConfigurationOption> channels,
        HashSet<string> selectedChannels,
        List<CMUConfigurationOption> access,
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
