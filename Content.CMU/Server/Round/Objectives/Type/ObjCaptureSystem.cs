using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.Popups;
using Content.Shared.CMU14.Round.Objectives;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components; // RuMC edit: типы урона по флагу
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Round.Objectives.Type;

public sealed partial class ObjCaptureSystem : ObjectiveSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;

    private readonly Dictionary<EntityUid, float> _timeSinceLastIncrement = new();
    private static readonly string[] HoistAllowedFactions = ["govfor", "opfor", "clf", "weyu"];

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-capture");
        SubscribeLocalEvent<CaptureObjectiveComponent, CaptureHoistFlagStartedEvent>(OnFlagHoistStarted);
        SubscribeLocalEvent<CaptureObjectiveComponent, CaptureHoistFlagDoAfterEvent>(OnHoistFlagDoAfter);
        SubscribeLocalEvent<CaptureObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<CaptureObjectiveComponent, DamageChangedEvent>(OnFlagDamaged);
    }

    public override void Shutdown()
    {
        _timeSinceLastIncrement.Clear();
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
        // RuMC edit start
        // Сбрасываем прочность флага и делаем его неуязвимым (опущен/нейтрален).
        comp.FlagHealth = comp.FlagInitialHealth;
        SetFlagDamageable(uid, false);
        // RuMC edit end
    }

    private void SetRemainingTime(EntityUid uid, CaptureObjectiveComponent comp, float remaining)
    {
        if (Math.Abs(comp.TimeUntilNextIncrement - remaining) < 0.01f)
            return;

        comp.TimeUntilNextIncrement = remaining;
        Dirty(uid, comp);
    }

    // RuMC edit start
    // Флаг уязвим, только пока он поднят и контролируется фракцией.
    // Когда флаг опущен (нейтрален) — у него нет Damageable, поэтому атаки по нему
    // проходят как промах, и урон просто не наносится. Включаем/выключаем компоненты динамически.
    // Нужны ОБА компонента: Damageable — чтобы атака не считалась промахом, а Injurable —
    // чтобы урон действительно применялся (урон сохраняется только через DamageDealtEvent,
    // который подписан на InjurableComponent). Без Injurable урон у флага обнуляется,
    // и клиент показывает "не может нанести урон".
    private void SetFlagDamageable(EntityUid uid, bool enabled)
    {
        if (enabled)
        {
            EnsureComp<DamageableComponent>(uid);
            EnsureComp<InjurableComponent>(uid);
        }
        else
        {
            RemComp<DamageableComponent>(uid);
            RemComp<InjurableComponent>(uid);
        }
    }
    // RuMC edit end

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
            // RuMC edit: флаг опущен — снова неуязвим.
            SetFlagDamageable(uid, false);
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
            // RuMC edit: во время поднятия восстанавливаем прочность флага и делаем его уязвимым.
            comp.FlagHealth = comp.FlagInitialHealth;
            SetFlagDamageable(uid, true);
            _popup.PopupEntity(
                Loc.GetString("cmu-capture-objective-raised", ("faction", comp.ControllerDisplayName)),
                uid,
                popupUser,
                PopupType.Medium);
        }
    }

    private void OnFlagDamaged(EntityUid uid, CaptureObjectiveComponent comp, ref DamageChangedEvent args)
    {
        // RuMC edit: любые типы урона (не только Slash) должны опускать поднятый флаг.
        if (args.DamageDelta is not { } delta || delta.GetTotal() <= 0)
            return;

        comp.FlagHealth -= delta.GetTotal().Float();
        if (comp.FlagHealth > 0f)
            return;

        comp.FlagHealth = comp.FlagInitialHealth;
        if (string.IsNullOrEmpty(comp.CurrentController))
        {
            // Страховка: даже если добили уже нейтральный флаг — он остаётся неуязвимым.
            SetFlagDamageable(uid, false);
            return;
        }

        comp.CurrentController = string.Empty;
        comp.ControllerDisplayName = string.Empty;
        comp.ControllerPlatoonName = string.Empty;
        SetRemainingTime(uid, comp, 0f);
        Dirty(uid, comp);
        // RuMC edit: флаг сбит уроном — снова опущен и неуязвим.
        SetFlagDamageable(uid, false);
        _popup.PopupEntity(Loc.GetString("cmu-capture-objective-damage-lowered"), uid, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        var govforPlatoon = _platoonSpawnRuleSystem.RoundGovforPlatoon;
        var opforPlatoon = _platoonSpawnRuleSystem.RoundOpforPlatoon;
        // RuMC edit start
        // Если у взвода не задан свой флаг (PlatoonFlag пуст), используем дефолт:
        // ГОФОР подымает обычный флаг UA, ОПФОР — порванный (uaflag_worn).
        var govforFlag = string.IsNullOrEmpty(govforPlatoon?.PlatoonFlag) ? "uaflag" : govforPlatoon.PlatoonFlag;
        var opforFlag = string.IsNullOrEmpty(opforPlatoon?.PlatoonFlag) ? "uaflag_worn" : opforPlatoon.PlatoonFlag;
        // RuMC edit end
        if (!string.IsNullOrEmpty(govforFlag) && govforFlag == opforFlag)
            opforFlag = "uaflag_worn";

        var query = EntityQueryEnumerator<CaptureObjectiveComponent, CMUObjectiveComponent>();
        while (query.MoveNext(out var uid, out var comp, out var objComp))
        {
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
