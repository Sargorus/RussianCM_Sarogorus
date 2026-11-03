using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Configuration;
using Content.Shared.CMU14.DroneOperator;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.DroneOperator;

public sealed class CMUDroneConfigurationWindow : DefaultWindow
{
    public event Action<string, HashSet<string>, HashSet<string>>? OnApply;

    private readonly OptionButton _faction;
    private readonly BoxContainer _channels;
    private readonly BoxContainer _access;
    private readonly Label _accessHint;
    private readonly Button _apply;

    private readonly Dictionary<string, CheckBox> _channelChecks = new();
    private readonly Dictionary<string, CheckBox> _accessChecks = new();
    private List<CMUConfigurationOption> _factions = new();
    private string _selectedFaction = string.Empty;

    public CMUDroneConfigurationWindow()
    {
        Title = Loc.GetString("cmu-drone-config-title");
        MinSize = new Vector2(440, 500);
        SetSize = new Vector2(500, 620);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };
        Contents.AddChild(root);

        root.AddChild(new Label { Text = Loc.GetString("cmu-drone-config-faction") });
        _faction = new OptionButton();
        _faction.OnItemSelected += args =>
        {
            if (args.Id >= 0 && args.Id < _factions.Count)
                _selectedFaction = _factions[args.Id].Id;
        };
        root.AddChild(_faction);

        root.AddChild(new Label { Text = Loc.GetString("cmu-drone-config-channels") });
        _channels = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var channelScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        channelScroll.AddChild(_channels);
        root.AddChild(channelScroll);

        _accessHint = new Label
        {
            Text = Loc.GetString("cmu-drone-config-no-id"),
            Visible = false,
        };
        root.AddChild(_accessHint);

        root.AddChild(new Label { Text = Loc.GetString("cmu-drone-config-access") });
        _access = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var accessScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        accessScroll.AddChild(_access);
        root.AddChild(accessScroll);

        _apply = new Button { Text = Loc.GetString("cmu-drone-config-apply") };
        _apply.OnPressed += _ =>
        {
            var channels = _channelChecks.Where(pair => pair.Value.Pressed).Select(pair => pair.Key).ToHashSet();
            var access = _accessChecks.Where(pair => pair.Value.Pressed).Select(pair => pair.Key).ToHashSet();
            OnApply?.Invoke(_selectedFaction, channels, access);
        };
        root.AddChild(_apply);
    }

    public void UpdateState(CMUDroneConfigurationBuiState state)
    {
        _factions = state.Factions;
        _faction.Clear();
        foreach (var faction in _factions)
            _faction.AddItem(Loc.GetString(faction.Name));

        var factionIndex = _factions.FindIndex(faction => faction.Id == _selectedFaction);
        if (factionIndex < 0)
            factionIndex = _factions.FindIndex(faction => faction.Id == state.CurrentFaction);
        if (factionIndex < 0 && _factions.Count > 0)
            factionIndex = 0;

        if (factionIndex >= 0)
        {
            _faction.SelectId(factionIndex);
            _selectedFaction = _factions[factionIndex].Id;
        }
        else
        {
            _selectedFaction = string.Empty;
        }

        _channels.RemoveAllChildren();
        _channelChecks.Clear();
        foreach (var channel in state.Channels)
        {
            var check = new CheckBox
            {
                Text = channel.Name,
                Pressed = state.SelectedChannels.Contains(channel.Id),
            };
            _channelChecks[channel.Id] = check;
            _channels.AddChild(check);
        }

        _access.RemoveAllChildren();
        _accessChecks.Clear();
        foreach (var access in state.Access)
        {
            var check = new CheckBox
            {
                Text = access.Name,
                Pressed = state.SelectedAccess.Contains(access.Id),
            };
            _accessChecks[access.Id] = check;
            _access.AddChild(check);
        }

        _accessHint.Visible = !state.HasIdCard;
    }
}
