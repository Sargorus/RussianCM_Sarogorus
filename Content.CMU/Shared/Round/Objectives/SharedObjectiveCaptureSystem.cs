using System.Linq;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;

namespace Content.Shared.CMU14.Round.Objectives;

public sealed partial class SharedObjectiveCaptureSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CaptureObjectiveComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CaptureObjectiveComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, CaptureObjectiveComponent comp, ExaminedEvent args)
    {
        if (string.IsNullOrEmpty(comp.ControllerDisplayName))
        {
            args.PushMarkup(Loc.GetString("cmu-capture-objective-examine-uncontrolled"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("cmu-capture-objective-examine-controlled",
                ("faction", comp.ControllerDisplayName)));

            if (!string.IsNullOrEmpty(comp.ControllerPlatoonName))
            {
                args.PushMarkup(Loc.GetString("cmu-capture-objective-examine-raised-by",
                    ("platoon", comp.ControllerPlatoonName)));
            }
        }

        if (!TryComp<CMUObjectiveComponent>(uid, out var objComp) || !objComp.Active || comp.PointIncrementTime <= 0)
        {
            args.PushMarkup(Loc.GetString("cmu-capture-objective-examine-not-objective"));
            return;
        }

        var hasObjective = ExaminerFactionHasObjective(args.Examiner, objComp);

        args.PushMarkup(Loc.GetString(hasObjective
            ? "cmu-capture-objective-examine-is-objective"
            : "cmu-capture-objective-examine-not-objective"));

        if (!hasObjective)
            return;

        args.PushMarkup(Loc.GetString("cmu-capture-objective-examine-points-time",
            ("time", FormatPointsTime(comp.PointIncrementTime))));

        if (!string.IsNullOrEmpty(comp.CurrentController) && IsObjectiveFaction(comp.CurrentController, objComp))
        {
            args.PushMarkup(Loc.GetString("cmu-capture-objective-examine-until-increment",
                ("time", FormatPointsTime(comp.TimeUntilNextIncrement))));
        }
    }

    private bool ExaminerFactionHasObjective(EntityUid examiner, CMUObjectiveComponent objComp)
    {
        if (objComp.Factions.Count == 0)
            return true;

        if (examiner == EntityUid.Invalid || !TryComp<NpcFactionMemberComponent>(examiner, out var npcFaction))
            return false;

        foreach (var faction in npcFaction.Factions)
        {
            var key = faction.ToString().ToLowerInvariant() switch { "auweyu" => "weyu", var id => id };
            if (IsObjectiveFaction(key, objComp))
                return true;
        }

        return false;
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

    private string FormatPointsTime(float seconds)
    {
        var total = Math.Max(0, (int) Math.Ceiling(seconds));
        return total < 60
            ? Loc.GetString("cmu-capture-objective-duration-seconds", ("seconds", total))
            : Loc.GetString("cmu-capture-objective-duration-minutes-seconds",
                ("minutes", total / 60), ("seconds", total % 60));
    }

    private void OnInteractHand(EntityUid uid, CaptureObjectiveComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;
        if (comp.ActionState != CaptureObjectiveComponent.FlagActionState.Idle)
        {
            args.Handled = true;
            return;
        }
        if (!TryComp<NpcFactionMemberComponent>(args.User, out var npcFaction) || npcFaction.Factions.Count == 0)
        {
            args.Handled = true;
            return;
        }
        var userFactions = npcFaction.Factions
            .Select(f => f.ToString().ToLowerInvariant() switch { "auweyu" => "weyu", var id => id }) // WeYu roles carry the npcFaction id AUWeYu; the objective faction key is weyu
            .ToList();
        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            args.Handled = true;
            var startedEvent = new CaptureHoistFlagStartedEvent(args.User, comp.CurrentController);
            RaiseLocalEvent(uid, startedEvent);
            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, comp.HoistTime, new CaptureHoistFlagDoAfterEvent { Faction = comp.CurrentController }, uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true
            };
            _doAfter.TryStartDoAfter(doAfterArgs);
            return;
        }
        string? allowed = null;
        foreach (var fac in new[] { "govfor", "opfor", "clf", "weyu" })
        {
            if (userFactions.Contains(fac))
            {
                allowed = fac;
                break;
            }
        }
        if (allowed == null)
        {
            args.Handled = true;
            return;
        }
        args.Handled = true;
        var startedRaiseEvent = new CaptureHoistFlagStartedEvent(args.User, allowed);
        RaiseLocalEvent(uid, startedRaiseEvent);
        var doAfterRaiseArgs = new DoAfterArgs(EntityManager, args.User, comp.HoistTime, new CaptureHoistFlagDoAfterEvent { Faction = allowed }, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAfterRaiseArgs);
    }
}
