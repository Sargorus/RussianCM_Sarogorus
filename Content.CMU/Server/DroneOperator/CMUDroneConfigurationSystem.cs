using System.Linq;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server.CMU14.DroneOperator;

/// <summary>
/// A Weyland-Yutani configuration key that rewrites a drone's faction, radio channels and access tags.
/// </summary>
public sealed partial class CMUDroneConfigurationSystem : EntitySystem
{
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private GunIFFSystem _iff = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private NpcFactionSystem _npcFactions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IComponentFactory _factory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUDroneConfigurationKeyComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ActorComponent, DidEquipHandEvent>(OnDidEquipHand);
        SubscribeLocalEvent<ActorComponent, DidUnequipHandEvent>(OnDidUnequipHand);

        Subs.BuiEvents<CMUDroneAndroidComponent>(CMUDroneConfigurationUi.Key, subs =>
        {
            subs.Event<CMUDroneConfigurationApplyMsg>(OnApply);
        });
        Subs.BuiEvents<CMUMedibotComponent>(CMUMedibotConfigurationUi.Key, subs =>
        {
            subs.Event<CMUMedibotConfigurationApplyMsg>(OnMedibotApply);
        });
    }

    private void OnDidEquipHand(Entity<ActorComponent> ent, ref DidEquipHandEvent args)
        => RefreshOpenSession(ent.Owner);

    private void OnDidUnequipHand(Entity<ActorComponent> ent, ref DidUnequipHandEvent args)
        => RefreshOpenSession(ent.Owner);

    private void RefreshOpenSession(EntityUid user)
    {
        var query = EntityQueryEnumerator<CMUDroneConfigurationSessionComponent>();
        while (query.MoveNext(out var drone, out var session))
        {
            if (session.User != user)
                continue;

            session.AvailableChannels = GetAvailableChannels(user);
            session.HasIdCard = TryFindHeldIdCard(user, session.Key, out var idCard);
            session.AvailableAccess = idCard is { } card && TryComp<AccessComponent>(card, out var access)
                ? new HashSet<ProtoId<AccessLevelPrototype>>(access.Tags)
                : new HashSet<ProtoId<AccessLevelPrototype>>();

            UpdateUi(drone, session);
        }
    }

    private void OnAfterInteract(Entity<CMUDroneConfigurationKeyComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            args.Target is not { } target ||
            !IsHolding(args.User, ent.Owner))
        {
            return;
        }

        if (TryComp<CMUMedibotComponent>(target, out var medibot))
        {
            args.Handled = true;
            medibot.ConfigUser = args.User;
            medibot.ConfigKey = ent.Owner;
            UpdateMedibotUi(target, medibot);
            _ui.TryOpenUi(target, CMUMedibotConfigurationUi.Key, args.User);
            return;
        }

        if (!HasComp<CMUDroneAndroidComponent>(target))
            return;

        args.Handled = true;

        var user = args.User;
        var session = EnsureComp<CMUDroneConfigurationSessionComponent>(target);
        session.Key = ent.Owner;
        session.User = user;
        session.AvailableChannels = GetAvailableChannels(user);
        session.HasIdCard = TryFindHeldIdCard(user, ent.Owner, out var idCard);
        session.AvailableAccess = idCard is { } card && TryComp<AccessComponent>(card, out var access)
            ? new HashSet<ProtoId<AccessLevelPrototype>>(access.Tags)
            : new HashSet<ProtoId<AccessLevelPrototype>>();

        UpdateUi(target, session);
        _ui.TryOpenUi(target, CMUDroneConfigurationUi.Key, user);
    }

    private void OnApply(Entity<CMUDroneAndroidComponent> ent, ref CMUDroneConfigurationApplyMsg msg)
    {
        if (!TryComp<CMUDroneConfigurationSessionComponent>(ent, out var session) ||
            session.User != msg.Actor ||
            !IsHolding(session.User, session.Key))
        {
            return;
        }

        if (!_proto.TryIndex<NpcFactionPrototype>(msg.Faction, out var faction) ||
            faction.Abstract)
        {
            return;
        }

        _npcFactions.ClearFactions(ent.Owner);
        _npcFactions.AddFaction(ent.Owner, msg.Faction);

        if (TryFindIffFaction(msg.Faction, out var iff))
            _iff.SetUserFaction(ent.Owner, iff);
        else
            _iff.ClearUserFactions(ent.Owner);

        ent.Comp.IffConfigured = true;

        // Re-read the operator's current credentials at apply time so a card or headset
        // dropped after the window opened cannot grant stale channels or access.
        var availableChannels = GetAvailableChannels(session.User);
        session.AvailableChannels = availableChannels;
        session.HasIdCard = TryFindHeldIdCard(session.User, session.Key, out var idCard);
        session.AvailableAccess = idCard is { } card && TryComp<AccessComponent>(card, out var cardAccess)
            ? new HashSet<ProtoId<AccessLevelPrototype>>(cardAccess.Tags)
            : new HashSet<ProtoId<AccessLevelPrototype>>();

        var channels = new HashSet<ProtoId<RadioChannelPrototype>>();
        foreach (var id in msg.Channels)
        {
            if (availableChannels.Contains(id))
                channels.Add(id);
        }

        EnsureComp<IntrinsicRadioReceiverComponent>(ent);
        EnsureComp<IntrinsicRadioTransmitterComponent>(ent).Channels = channels;
        EnsureComp<ActiveRadioComponent>(ent).Channels = channels;

        var access = new HashSet<ProtoId<AccessLevelPrototype>>();
        foreach (var id in msg.Access)
        {
            if (session.AvailableAccess.Contains(id))
                access.Add(id);
        }

        var accessComp = EnsureComp<AccessComponent>(ent);
        _access.TrySetTags(ent.Owner, access, accessComp);

        UpdateUi(ent.Owner, session);
        _popup.PopupEntity(Loc.GetString("cmu-drone-config-applied"), ent, session.User);
    }

    private void OnMedibotApply(Entity<CMUMedibotComponent> ent, ref CMUMedibotConfigurationApplyMsg msg)
    {
        if (ent.Comp.ConfigUser != msg.Actor ||
            ent.Comp.ConfigKey is not { } key ||
            !IsHolding(msg.Actor, key))
        {
            return;
        }

        ent.Comp.InjectAmount = FixedPoint2.New(Math.Clamp(msg.InjectAmount, 1, 50));
        ent.Comp.InjectOnBrute = msg.InjectOnBrute;
        ent.Comp.InjectOnBurn = msg.InjectOnBurn;
        ent.Comp.InjectOnCritical = msg.InjectOnCritical;

        var factions = new HashSet<string>();
        foreach (var id in msg.Factions)
        {
            if (_proto.TryIndex<NpcFactionPrototype>(id, out var faction) && !faction.Abstract)
                factions.Add(faction.ID);
        }
        ent.Comp.TreatFactions = factions;

        UpdateMedibotUi(ent.Owner, ent.Comp);
        _popup.PopupEntity(Loc.GetString("cmu-medibot-config-applied"), ent, msg.Actor);
    }

    private void UpdateMedibotUi(EntityUid medibot, CMUMedibotComponent component)
    {
        var beaker = _itemSlots.GetItemOrNull(medibot, component.BeakerSlotId);
        var hasBeaker = false;
        string beakerText;

        if (beaker is { } beakerUid &&
            _solutions.TryGetSolution(beakerUid, "beaker", out _, out var solution))
        {
            hasBeaker = true;
            beakerText = Loc.GetString("cmu-medibot-config-beaker", ("volume", solution.Volume));
        }
        else
        {
            beakerText = Loc.GetString("cmu-medibot-config-no-beaker");
        }

        var factions = new List<CMUDroneConfigurationOption>();
        foreach (var faction in _proto.EnumeratePrototypes<NpcFactionPrototype>())
        {
            if (faction.Abstract)
                continue;

            var name = string.IsNullOrEmpty(faction.Name)
                ? faction.ID
                : Loc.GetString(faction.Name);
            factions.Add(new CMUDroneConfigurationOption(faction.ID, name));
        }
        factions.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        var state = new CMUMedibotConfigurationBuiState(
            component.InjectAmount.Int(),
            component.InjectOnBrute,
            component.InjectOnBurn,
            component.InjectOnCritical,
            hasBeaker,
            beakerText,
            factions,
            component.TreatFactions);

        _ui.SetUiState(medibot, CMUMedibotConfigurationUi.Key, state);
    }

    private void UpdateUi(EntityUid drone, CMUDroneConfigurationSessionComponent session)
    {
        var factions = BuildFactionOptions(session.User);

        var currentFaction = string.Empty;
        if (TryComp<NpcFactionMemberComponent>(drone, out var member))
            currentFaction = member.Factions.FirstOrDefault().Id ?? string.Empty;

        var channels = new List<CMUDroneConfigurationOption>();
        foreach (var channelId in session.AvailableChannels)
        {
            if (!_proto.TryIndex(channelId, out RadioChannelPrototype? channel))
                continue;

            channels.Add(new CMUDroneConfigurationOption(channelId.Id, channel.LocalizedName));
        }
        channels.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        var selectedChannels = new HashSet<string>();
        if (TryComp<ActiveRadioComponent>(drone, out var active))
            selectedChannels.UnionWith(active.Channels.Select(channel => channel.Id));
        if (TryComp<IntrinsicRadioTransmitterComponent>(drone, out var transmitter))
            selectedChannels.UnionWith(transmitter.Channels.Select(channel => channel.Id));

        var access = new List<CMUDroneConfigurationOption>();
        foreach (var accessId in session.AvailableAccess)
        {
            if (!_proto.TryIndex(accessId, out AccessLevelPrototype? level))
                continue;

            access.Add(new CMUDroneConfigurationOption(accessId.Id, level.GetAccessLevelName()));
        }
        access.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        var selectedAccess = new HashSet<string>();
        if (TryComp<AccessComponent>(drone, out var accessComp))
            selectedAccess.UnionWith(accessComp.Tags.Select(tag => tag.Id));

        var state = new CMUDroneConfigurationBuiState(
            factions,
            currentFaction,
            channels,
            selectedChannels,
            access,
            selectedAccess,
            session.HasIdCard);

        _ui.SetUiState(drone, CMUDroneConfigurationUi.Key, state);
    }

    private List<CMUDroneConfigurationOption> BuildFactionOptions(EntityUid user)
    {
        var factions = new List<CMUDroneConfigurationOption>();
        var seen = new HashSet<string>();

        AddFactionOption(factions, seen, "AUWeYu");
        AddFactionOption(factions, seen, "GOVFOR");
        AddFactionOption(factions, seen, "OPFOR");
        AddFactionOption(factions, seen, "AUColonist");

        if (TryComp<NpcFactionMemberComponent>(user, out var member))
        {
            foreach (var faction in member.Factions)
                AddFactionOption(factions, seen, faction.Id);
        }

        return factions;
    }

    private void AddFactionOption(
        List<CMUDroneConfigurationOption> factions,
        HashSet<string> seen,
        string id)
    {
        if (!seen.Add(id) || !_proto.TryIndex<NpcFactionPrototype>(id, out var faction))
            return;

        var name = string.IsNullOrEmpty(faction.Name)
            ? faction.ID
            : Loc.GetString(faction.Name);
        factions.Add(new CMUDroneConfigurationOption(faction.ID, name));
    }

    private bool TryFindIffFaction(string npcFaction, out EntProtoId<IFFFactionComponent> faction)
    {
        if (SharedSentryTargetingSystem.SentryFactionToIff.TryGetValue(npcFaction, out faction))
            return true;

        if (TryGetIffPrototype(npcFaction, out faction))
            return true;

        if (TryGetIffPrototype($"Faction{npcFaction}", out faction))
            return true;

        faction = default;
        return false;
    }

    private bool TryGetIffPrototype(string id, out EntProtoId<IFFFactionComponent> faction)
    {
        faction = default;
        if (!_proto.TryIndex<EntityPrototype>(id, out var proto) ||
            !proto.TryGetComponent<IFFFactionComponent>(out _, _factory))
        {
            return false;
        }

        faction = id;
        return true;
    }

    private HashSet<ProtoId<RadioChannelPrototype>> GetAvailableChannels(EntityUid user)
    {
        var channels = new HashSet<ProtoId<RadioChannelPrototype>>();

        if (TryComp<WearingHeadsetComponent>(user, out var wearing) &&
            TryComp<EncryptionKeyHolderComponent>(wearing.Headset, out var keys))
        {
            channels.UnionWith(keys.Channels);
        }

        if (TryComp<IntrinsicRadioTransmitterComponent>(user, out var intrinsic))
            channels.UnionWith(intrinsic.Channels);

        if (TryComp<ActiveRadioComponent>(user, out var active))
            channels.UnionWith(active.Channels);

        return channels;
    }

    private bool TryFindHeldIdCard(EntityUid user, EntityUid key, out EntityUid idCard)
    {
        idCard = default;

        if (!TryComp(user, out HandsComponent? hands))
            return false;

        foreach (var hand in _hands.EnumerateHands((user, hands)))
        {
            if (!_hands.TryGetHeldItem((user, hands), hand, out var held) ||
                held is not { } heldUid ||
                heldUid == key ||
                !HasComp<IdCardComponent>(heldUid) ||
                !HasComp<AccessComponent>(heldUid))
            {
                continue;
            }

            idCard = heldUid;
            return true;
        }

        return false;
    }

    private bool IsHolding(EntityUid user, EntityUid item)
    {
        if (!TryComp(user, out HandsComponent? hands))
            return false;

        foreach (var hand in _hands.EnumerateHands((user, hands)))
        {
            if (_hands.TryGetHeldItem((user, hands), hand, out var held) && held == item)
                return true;
        }

        return false;
    }
}
