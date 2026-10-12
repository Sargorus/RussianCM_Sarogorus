using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Radio.Components;
using Content.Server._RMC14.Language.Systems; // CMU14
using Content.Shared.Speech.EntitySystems;
using Content.Server.Players;
using Content.Shared.CMU14.Threats.Mobs.Xeno;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Mentor.ImaginaryFriend;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14;
using ManageHiveComponent = Content.Shared._RMC14.Xenonids.ManageHive.ManageHiveComponent;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Players;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Replays;
using Robust.Shared.Network;
using Robust.Server.GameObjects;
using CultistComponent = Content.Shared.CMU14.Threats.Mobs.Cultist.CultistComponent;
using HasKnowledgeOfXenoLanguageComponent = Content.Shared.CMU14.Threats.Mobs.Xeno.HasKnowledgeOfXenoLanguageComponent;

namespace Content.Server._RMC14.Chat.Chat;

public sealed partial class CMChatSystem : SharedCMChatSystem
{

    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ReplacementAccentSystem _wordreplacement = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private LanguageSystem _language = default!; // CMU14


    private static readonly ProtoId<ReplacementAccentPrototype> ChatSanitize = "CMChatSanitize";
    private static readonly ProtoId<ReplacementAccentPrototype> MarineChatSanitize = "CMChatSanitizeMarine";
    private static readonly ProtoId<ReplacementAccentPrototype> XenoChatSanitize = "CMChatSanitizeXeno";
    private readonly HashSet<ICommonSession> _toRemove = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImaginaryFriendComponent, ChatMessageAfterGetRecipients>(OnImaginaryFriendGetRecipients);
        SubscribeLocalEvent<XenoComponent, ChatMessageAfterGetRecipients>(OnXenoAfterGetRecipients);
    }

    private void OnImaginaryFriendGetRecipients(Entity<ImaginaryFriendComponent> ent, ref ChatMessageAfterGetRecipients args)
    {
        _toRemove.Clear();

        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer)
                continue;

            if (session.AttachedEntity != ent.Comp.Imaginer)
                _toRemove.Add(session);
        }

        foreach (var session in _toRemove)
            args.Recipients.Remove(session);
    }

    private void OnXenoAfterGetRecipients(Entity<XenoComponent> ent, ref ChatMessageAfterGetRecipients args)
    {
        if (args.Purpose != ChatRecipientPurpose.Speech)
            return;

        _toRemove.Clear();
        var hive = _hive.GetHive(ent.Owner);
        var hivebroken = IsHivebrokenXeno(ent.Owner);
        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer)
                continue;

            if (CanHearXenoSpeech(ent.Owner, session.AttachedEntity, hivebroken, hive))
                continue;

            _toRemove.Add(session);
        }

        foreach (var session in _toRemove)
        {
            args.Recipients.Remove(session);
        }
    }

    private bool CanHearXenoSpeech(
        EntityUid source,
        EntityUid? listener,
        bool hivebroken,
        Entity<HiveComponent>? hive)
    {
        // CMU14: language grants admit their listeners through this filter, but the hivebroken
        // allowlist wins. Converted xenos speak Common, so an unguarded shortcut leaks
        // hivebroken chat to ordinary humans.
        if (listener is { } hearer
            && !hivebroken
            && _language.CanUnderstand(hearer, _chatSystem.GetCurrentLanguageForSpeech(source)))
            return true;

        if (!hivebroken)
        {
            return HasComp<XenoComponent>(listener) ||
                   HasComp<HasKnowledgeOfXenoLanguageComponent>(listener) ||
                   (HasComp<ManageHiveComponent>(source) && hive is not null && hive.Value.Comp.Corrupted);
        }

        return HasComp<XenoComponent>(listener) ||
               HasComp<HasKnowledgeOfXenoLanguageComponent>(listener) ||
               HasComp<YautjaComponent>(listener) ||
               HasComp<YautjaThrallComponent>(listener) ||
               HasComp<YautjaHivebrokenXenoComponent>(listener);
    }

    public override string SanitizeMessageReplaceWords(EntityUid source, string msg)
    {
        msg = NormalizeLocalizedRadioKey(source, msg);
        msg = _wordreplacement.ApplyReplacements(msg, ChatSanitize);

        var factionSanitize = HasComp<XenoComponent>(source) && !UsesHumanChatSanitize(source)
            ? XenoChatSanitize
            : MarineChatSanitize;
        msg = _wordreplacement.ApplyReplacements(msg, factionSanitize);

        return msg;
    }

    public string NormalizeLocalizedRadioKey(EntityUid source, string msg)
    {
        if (msg.Length < 2)
            return msg;

        var prefix = msg[0];
        if (prefix != SharedChatSystem.RadioChannelPrefix &&
            prefix != SharedChatSystem.RadioChannelAltPrefix)
            return msg;

        var keycode = char.ToLowerInvariant(msg[1]);
        RadioChannelPrototype? channel = null;

        var resolved = TryComp(source, out WearingHeadsetComponent? wearing) &&
                       TryResolveHeadsetRadioChannel(wearing.Headset, prefix, keycode, out channel);

        if (!resolved && TryComp(source, out IntrinsicRadioTransmitterComponent? intrinsic))
            resolved = TryResolveRadioChannels(intrinsic.Channels, prefix, keycode, out channel);

        if (!resolved || channel == null)
            return msg;

        var canonicalKeycode = char.ToLowerInvariant(channel.KeyCode);
        if (canonicalKeycode == keycode)
            return msg;

        return $"{prefix}{canonicalKeycode}{msg[2..]}";
    }

    private bool TryResolveHeadsetRadioChannel(
        EntityUid headset,
        char prefix,
        char keycode,
        out RadioChannelPrototype? channel)
    {
        channel = null;

        if (!TryComp(headset, out EncryptionKeyHolderComponent? keys))
            return false;

        return TryResolveRadioChannels(keys.Channels, prefix, keycode, out channel);
    }

    private bool TryResolveRadioChannels(
        IEnumerable<ProtoId<RadioChannelPrototype>> channels,
        char prefix,
        char keycode,
        out RadioChannelPrototype? channel)
    {
        channel = null;

        if (prefix == SharedChatSystem.RadioChannelAltPrefix)
            prefix = SharedChatSystem.RadioChannelPrefix;

        var normalizedKeycode = char.ToLowerInvariant(keycode);

        // Prefer the RuCM alias over a canonical key from another channel when both are present.
        foreach (var id in channels)
        {
            var candidate = _proto.Index<RadioChannelPrototype>(id);
            if (candidate.RadioPrefix != prefix ||
                candidate.LocalizedKeyCode == '\0' ||
                char.ToLowerInvariant(candidate.LocalizedKeyCode) != normalizedKeycode)
                continue;

            channel = candidate;
            return true;
        }

        foreach (var id in channels)
        {
            var candidate = _proto.Index<RadioChannelPrototype>(id);
            if (candidate.RadioPrefix != prefix ||
                char.ToLowerInvariant(candidate.KeyCode) != normalizedKeycode)
                continue;

            channel = candidate;
            return true;
        }

        return false;
    }

    private bool UsesHumanChatSanitize(EntityUid source)
    {
        return IsHivebrokenXeno(source) ||
               _hive.GetHive(source) is { Comp.Corrupted: true };
    }

    public override void ChatMessageToOne(
        ChatChannel channel,
        string message,
        string wrappedMessage,
        EntityUid source,
        bool hideChat,
        INetChannel client,
        Color? colorOverride = null,
        bool recordReplay = false,
        string? audioPath = null,
        float audioVolume = 0,
        NetUserId? author = null)
    {
        _chatManager.ChatMessageToOne(
            channel,
            message,
            wrappedMessage,
            source,
            hideChat,
            client,
            colorOverride,
            recordReplay,
            audioPath,
            audioVolume,
            author
        );
    }

    public override void ChatMessageToMany(
        string message,
        string wrappedMessage,
        Filter filter,
        ChatChannel channel,
        EntityUid source = default,
        bool hideChat = false,
        Color? colorOverride = null,
        bool recordReplay = false,
        string? audioPath = null,
        float audioVolume = 0,
        NetUserId? author = null)
    {
        _chatManager.ChatMessageToManyFiltered(
            filter,
            channel,
            message,
            wrappedMessage,
            source,
            hideChat,
            recordReplay,
            colorOverride,
            audioPath,
            audioVolume
        );
    }

    public override void Emote(
        EntityUid source,
        string message,
        string? nameOverride = null,
        bool checkRadioPrefix = true,
        bool ignoreActionBlocker = false)
    {
        _chatSystem.TrySendInGameICMessage(
            source,
            message,
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            false,
            null,
            null,
            nameOverride,
            checkRadioPrefix,
            ignoreActionBlocker
        );
    }

    public List<string>? TryMultiBroadcast(EntityUid source, string message)
    {
        if (string.IsNullOrEmpty(message) || message.Length < 2)
            return null;

        if (!HasComp<InventoryComponent>(source))
            return null;

        var time = _timing.CurTime;
        Entity<HeadsetMultiBroadcastComponent>? headset = null;
        var ears = _inventory.GetSlotEnumerator(source, SlotFlags.EARS);
        while (ears.MoveNext(out var ear))
        {
            if (ear.ContainedEntity is not { } contained)
                continue;

            if (TryComp(contained, out HeadsetMultiBroadcastComponent? headsetComp))
            {
                headset = (contained, headsetComp);
                break;
            }
        }

        if (headset == null)
            return null;

        var validPrefixes = new List<string>();
        var prefixLength = 0;
        var sharedPrefix = message[0];

        if (sharedPrefix != SharedChatSystem.RadioChannelPrefix &&
            sharedPrefix != SharedChatSystem.RadioChannelAltPrefix)
            return null;

        for (var i = 1; i < message.Length; i++)
        {
            var keycode = char.ToLowerInvariant(message[i]);
            if (char.IsWhiteSpace(keycode))
            {
                prefixLength = i;
                break;
            }

            if (!TryResolveHeadsetRadioChannel(headset.Value, sharedPrefix, keycode, out var channel) ||
                channel == null)
            {
                prefixLength = i;
                break;
            }

            validPrefixes.Add($"{sharedPrefix}{char.ToLowerInvariant(channel.KeyCode)}");
            prefixLength = i + 1;
        }

        var count = Math.Min(validPrefixes.Count, headset.Value.Comp.Maximum);
        validPrefixes = validPrefixes.Take(count).ToList();

        if (validPrefixes.Count < 2)
            return null;

        var messages = new List<string>(validPrefixes.Count);
        var messageBody = message[prefixLength..];

        for (var idx = 0; idx < validPrefixes.Count; idx++)
            messages.Add($"{validPrefixes[idx]}{messageBody}");

        if (messages.Count < 2)
            return null;

        var timeLeft = headset.Value.Comp.Last + headset.Value.Comp.Cooldown - time;
        if (headset.Value.Comp.Last != null &&
            timeLeft != null &&
            timeLeft.Value > TimeSpan.Zero)
        {
            _popup.PopupEntity(
                $"You've used the multi-broadcast system too recently, wait {timeLeft.Value.TotalSeconds:F0} more seconds.",
                source,
                source,
                PopupType.MediumCaution
            );

            messages.Clear();
            return messages;
        }

        headset.Value.Comp.Last = time;
        Dirty(headset.Value);
        return messages;
    }
}
