using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.Popups;
using Content.Shared.CMU14.Round.Objectives;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Round.Objectives.Type;

public sealed partial class ObjCaptureSystem : ObjectiveSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;

    private readonly Dictionary<EntityUid, float> _timeSinceLastIncrement = new();
    private readonly Dictionary<EntityUid, float> _lastSlashDamage = new();
    private static readonly string[] HoistAllowedFactions = ["govfor", "opfor", "clf", "weyu"];

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-capture");
        SubscribeLocalEvent<CaptureObjectiveComponent, CaptureHoistFlagStartedEvent>(OnFlagHoistStarted);
        SubscribeLocalEvent<CaptureObjectiveComponent, CaptureHoistFlagDoAfterEvent>(OnHoistFlagDoAfter);
        SubscribeLocalEvent<CaptureObjectiveComponent, ObjectiveResetEvent>(OnReset);
    }

    public override void Shutdown()
    {
        _timeSinceLastIncrement.Clear();
        _lastSlashDamage.Clear();
        base.Shutdown();
    }

    private void OnReset(EntityUid uid, CaptureObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.CurrentController = string.Empty;
        comp.ControllerDisplayName = string.Empty;
        comp.ControllerPlatoonName = string.Empty;
        comp.TimesIncremented = 0;
        comp.TimesIncrementedPerFaction.Clear();
        comp.ActionState = CaptureObjectiveComponent.FlagActionState.Idle;
        comp.ActionUser = null;
        comp.ActionUserFaction = null;
        SetRemainingTime(uid, comp, 0f);
        Dirty(uid, comp);
    }

    private void SetRemainingTime(EntityUid uid, CaptureObjectiveComponent comp, float remaining)
    {
        if (Math.Abs(comp.TimeUntilNextIncrement - remaining) < 0.01f)
            return;

        comp.TimeUntilNextIncrement = remaining;
        Dirty(uid, comp);
    }

    private static string ResolveFlagSpriteState(string controller, string govforFlag, string opforFlag)
    {
        return controller.ToLowerInvariant() switch
        {
            "" => CaptureObjectiveComponent.NeutralFlagState,
            "govfor" => string.IsNullOrEmpty(govforFlag) ? CaptureObjectiveComponent.NeutralFlagState : govforFlag,
            "opfor" => string.IsNullOrEmpty(opforFlag) ? CaptureObjectiveComponent.NeutralFlagState : opforFlag,
            "clf" => "clfflag",
            _ => string.Empty,
        };
    }

    private string GetFactionDisplayName(string faction)
    {
        return faction.ToLowerInvariant() switch
        {
            "govfor" => Loc.GetString("cmu-capture-objective-faction-govfor"),
            "opfor" => Loc.GetString("cmu-capture-objective-faction-opfor"),
            "clf" => Loc.GetString("cmu-capture-objective-faction-clf"),
            "weyu" => Loc.GetString("cmu-capture-objective-faction-weyu"),
            _ => faction,
        };
    }

    private string GetFactionSubunitName(string faction)
    {
        return faction.ToLowerInvariant() switch
        {
            "govfor" => _platoonSpawnRuleSystem.RoundGovforPlatoon?.Name ?? string.Empty,
            "opfor" => _platoonSpawnRuleSystem.RoundOpforPlatoon?.Name ?? string.Empty,
            _ => string.Empty,
        };
    }

    private void OnFlagHoistStarted(EntityUid uid, CaptureObjectiveComponent comp, CaptureHoistFlagStartedEvent args)
    {
        if (comp.ActionState != CaptureObjectiveComponent.FlagActionState.Idle)
        {
            _popup.PopupEntity(
                Loc.GetString(comp.ActionState == CaptureObjectiveComponent.FlagActionState.Hoisting
                    ? "cmu-capture-objective-already-hoisting"
                    : "cmu-capture-objective-already-lowering"),
                uid,
                args.User,
                PopupType.Medium);
            return;
        }

        var userFactions = new List<string>();
        if (args.User != EntityUid.Invalid && TryComp(args.User, out NpcFactionMemberComponent? factionComp))
            userFactions.AddRange(factionComp.Factions.Select(f => f.ToString().ToLowerInvariant() switch { "auweyu" => "weyu", var id => id })); // WeYu roles carry the npcFaction id AUWeYu; the objective faction key is weyu

        var hoistingFaction = args.Faction.ToLowerInvariant();
        if (!userFactions.Contains(hoistingFaction))
            userFactions.Add(hoistingFaction);

        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            comp.ActionState = CaptureObjectiveComponent.FlagActionState.Lowering;
            comp.ActionUser = args.User;
            comp.ActionUserFaction = comp.CurrentController;
            _popup.PopupEntity(Loc.GetString("cmu-capture-objective-begin-lowering"), uid, args.User, PopupType.Medium);
            return;
        }

        string? allowed = null;
        foreach (var fac in HoistAllowedFactions)
        {
            if (userFactions.Contains(fac))
            {
                allowed = fac;
                break;
            }
        }

        if (allowed == null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-capture-objective-faction-cannot-raise"), uid, args.User, PopupType.Medium);
            return;
        }

        comp.ActionState = CaptureObjectiveComponent.FlagActionState.Hoisting;
        comp.ActionUser = args.User;
        comp.ActionUserFaction = allowed;

        _popup.PopupEntity(
            Loc.GetString("cmu-capture-objective-begin-raising", ("faction", GetFactionDisplayName(allowed))),
            uid,
            args.User,
            PopupType.Medium);
    }

    private void OnHoistFlagDoAfter(EntityUid uid, CaptureObjectiveComponent comp, CaptureHoistFlagDoAfterEvent args)
    {
        comp.ActionState = CaptureObjectiveComponent.FlagActionState.Idle;
        comp.ActionUser = null;
        comp.ActionUserFaction = null;

        if (args.Cancelled)
            return;

        var popupUser = args.User != EntityUid.Invalid ? args.User : uid;

        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            comp.CurrentController = string.Empty;
            comp.ControllerDisplayName = string.Empty;
            comp.ControllerPlatoonName = string.Empty;
            _timeSinceLastIncrement.Remove(uid);
            SetRemainingTime(uid, comp, 0f);
            Dirty(uid, comp);
            _popup.PopupEntity(Loc.GetString("cmu-capture-objective-lowered"), uid, popupUser, PopupType.Medium);
        }
        else
        {
            comp.CurrentController = args.Faction;
            comp.ControllerDisplayName = GetFactionDisplayName(comp.CurrentController);
            comp.ControllerPlatoonName = GetFactionSubunitName(comp.CurrentController);
            _timeSinceLastIncrement[uid] = 0f;
            SetRemainingTime(uid, comp, comp.PointIncrementTime);
            Dirty(uid, comp);
            _popup.PopupEntity(
                Loc.GetString("cmu-capture-objective-raised", ("faction", comp.ControllerDisplayName)),
                uid,
                popupUser,
                PopupType.Medium);
        }
    }

    public override void Update(float frameTime)
    {
        var govforPlatoon = _platoonSpawnRuleSystem.RoundGovforPlatoon;
        var opforPlatoon = _platoonSpawnRuleSystem.RoundOpforPlatoon;
        var govforFlag = govforPlatoon?.PlatoonFlag ?? "uaflag";
        var opforFlag = opforPlatoon?.PlatoonFlag ?? "uaflag_worn";
        if (!string.IsNullOrEmpty(govforFlag) && govforFlag == opforFlag)
            opforFlag = "uaflag_worn";

        var query = EntityQueryEnumerator<CaptureObjectiveComponent, CMUObjectiveComponent>();
        while (query.MoveNext(out var uid, out var comp, out var objComp))
        {
            if (TryComp(uid, out DamageableComponent? damageable))
            {
                float currentSlash = 0f;
                var damage = _damageable.GetAllDamage((uid, damageable));
                if (damage.DamageDict.TryGetValue("Slash", out var slash))
                    currentSlash = slash.Float();
                _lastSlashDamage.TryGetValue(uid, out float lastSlash);
                float delta = currentSlash - lastSlash;
                if (delta > 0f)
                {
                    comp.FlagHealth -= delta;
                    if (comp.FlagHealth <= 0f)
                    {
                        comp.FlagHealth = comp.FlagInitialHealth;
                        if (!string.IsNullOrEmpty(comp.CurrentController))
                        {
                            comp.CurrentController = string.Empty;
                            comp.ControllerDisplayName = string.Empty;
                            comp.ControllerPlatoonName = string.Empty;
                            SetRemainingTime(uid, comp, 0f);
                            Dirty(uid, comp);
                            _popup.PopupEntity(Loc.GetString("cmu-capture-objective-damage-lowered"), uid, PopupType.Medium);
                        }
                    }
                }
                _lastSlashDamage[uid] = currentSlash;
            }

            comp.GovforFlagState = govforFlag;
            comp.OpforFlagState = opforFlag;

            var spriteState = ResolveFlagSpriteState(comp.CurrentController, govforFlag, opforFlag);
            if (comp.CurrentSpriteState != spriteState)
            {
                comp.CurrentSpriteState = spriteState;
                Dirty(uid, comp);
            }

            if (!objComp.Active)
                continue;
            if (comp.MaxHoldTimes > 0 && comp.TimesIncremented >= comp.MaxHoldTimes)
                continue;
            if (comp is { OnceOnly: true, TimesIncremented: > 0 })
                continue;
            if (string.IsNullOrEmpty(comp.CurrentController))
                continue;
            if (!IsObjectiveFaction(comp.CurrentController, objComp))
                continue;

            _timeSinceLastIncrement.TryAdd(uid, 0f);
            _timeSinceLastIncrement[uid] += frameTime;

            var remaining = Math.Max(0, comp.PointIncrementTime - _timeSinceLastIncrement[uid]);
            if (Math.Ceiling(comp.TimeUntilNextIncrement) != Math.Ceiling(remaining))
            {
                comp.TimeUntilNextIncrement = remaining;
                Dirty(uid, comp);
            }

            if (!(_timeSinceLastIncrement[uid] >= comp.PointIncrementTime))
                continue;

            _timeSinceLastIncrement[uid] = 0f;
            comp.TimesIncremented++;

            var factionKey = comp.CurrentController.ToLowerInvariant();
            comp.TimesIncrementedPerFaction.TryAdd(factionKey, 0);
            comp.TimesIncrementedPerFaction[factionKey]++;

            ObjCtrl.AwardPointsToFaction(comp.CurrentController, objComp);

            if (comp is { OnceOnly: true, TimesIncremented: > 0 })
            {
                ObjCtrl.CompleteObjectiveForFaction(uid, objComp, comp.CurrentController, sawmill: _logs);
                continue;
            }

            if (comp.MaxHoldTimes > 0 && comp.TimesIncremented >= comp.MaxHoldTimes)
                ObjCtrl.CompleteObjectiveForFaction(uid, objComp, comp.CurrentController, sawmill: _logs);
        }
    }

    private static bool IsObjectiveFaction(string faction, CMUObjectiveComponent objComp)
    {
        if (string.IsNullOrEmpty(faction))
            return false;

        if (objComp.Factions.Count == 0)
            return true;

        var key = faction.ToLowerInvariant();
        return objComp.Factions.Any(f => f.ToLowerInvariant() == key);
    }
}
