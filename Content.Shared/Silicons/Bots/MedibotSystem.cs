using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.Silicons.Bots;

/// <summary>
/// Handles emagging medibots and provides api.
/// </summary>
public sealed partial class MedibotSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmaggableMedibotComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<MedibotComponent, UserActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<MedibotComponent, MedibotInjectDoAfterEvent>(OnInject);
    }

    private void OnEmagged(EntityUid uid, EmaggableMedibotComponent comp, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        if (!TryComp<MedibotComponent>(uid, out var medibot))
            return;

        foreach (var (state, treatment) in comp.Replacements)
        {
            medibot.Treatments[state] = treatment;
        }

        args.Handled = true;
    }

    private void OnInteract(Entity<MedibotComponent> medibot, ref UserActivateInWorldEvent args)
    {
        if (!CheckInjectable(medibot!, args.Target, true)) return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, 2f, new MedibotInjectDoAfterEvent(), args.User, args.Target)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
        });
    }

    private void OnInject(EntityUid uid, MedibotComponent comp, ref MedibotInjectDoAfterEvent args)
    {
        if (args.Cancelled) return;

        if (args.Target is { } target)
            TryInject(uid, target);
    }

    /// <summary>
    /// Get a treatment for a given mob state.
    /// </summary>
    /// <remarks>
    /// This only exists because allowing other execute would allow modifying the dictionary, and Read access does not cover TryGetValue.
    /// </remarks>
    public bool TryGetTreatment(MedibotComponent comp, MobState state, [NotNullWhen(true)] out MedibotTreatment? treatment)
    {
        return comp.Treatments.TryGetValue(state, out treatment);
    }

    /// <summary>
    /// Checks if the target can be injected.
    /// </summary>
    public bool CheckInjectable(Entity<MedibotComponent?> medibot, EntityUid target, bool manual = false)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)) return false;

        if (!HasUsableBeaker(medibot.Owner))
            return false;

        if (TryComp<CMUMedibotReservationComponent>(target, out var reservation) &&
            reservation.Bot != medibot.Owner &&
            _timing.CurTime < reservation.Expires)
        {
            return false;
        }

        if (HasComp<NPCRecentlyInjectedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("medibot-recently-injected"), medibot, medibot);
            return false;
        }

        if (!TryComp<MobStateComponent>(target, out var mobState)) return false;
        if (!TryComp<DamageableComponent>(target, out var damageable)) return false;
        if (!_solutionContainer.TryGetInjectableSolution(target, out _, out _)) return false;

        if (mobState.CurrentState != MobState.Alive && mobState.CurrentState != MobState.Critical)
        {
            _popup.PopupEntity(Loc.GetString("medibot-target-dead"), medibot, medibot);
            return false;
        }

        var total = _damageable.GetTotalDamage((target, damageable));
        if (total == 0 && !HasComp<EmaggedComponent>(medibot))
        {
            _popup.PopupEntity(Loc.GetString("medibot-target-healthy"), medibot, medibot);
            return false;
        }

        if (!TryGetTreatment(medibot.Comp, mobState.CurrentState, out var treatment) || !manual) return false;

        return true;
    }

    /// <summary>
    /// Whether a CMU medibot has a beaker with something in it. Vanilla
    /// medibots keep their built-in treatments and always return true.
    /// </summary>
    public bool HasUsableBeaker(EntityUid medibot)
    {
        if (!TryComp<CMUMedibotComponent>(medibot, out var component))
            return true;

        if (_itemSlots.GetItemOrNull(medibot, component.BeakerSlotId) is not { } beaker)
            return false;

        return _solutionContainer.TryGetSolution(beaker, "beaker", out _, out var solution) &&
               solution.Volume > FixedPoint2.Zero;
    }

    /// <summary>
    /// Tries to inject the target.
    /// </summary>
    public bool TryInject(Entity<MedibotComponent?> medibot, EntityUid target)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)) return false;

        if (!_interaction.InRangeUnobstructed(medibot.Owner, target)) return false;

        if (!TryComp<MobStateComponent>(target, out var mobState)) return false;
        if (!TryGetTreatment(medibot.Comp, mobState.CurrentState, out var treatment)) return false;
        if (!_solutionContainer.TryGetInjectableSolution(target, out var injectable, out _)) return false;

        if (TryComp<CMUMedibotComponent>(medibot.Owner, out var cmu))
        {
            if (!TryInjectFromBeaker(medibot.Owner, target, cmu, injectable.Value))
                return false;
        }
        else
        {
            _solutionContainer.TryAddReagent(injectable.Value, treatment.Reagent, treatment.Quantity, out _);
        }

        _popup.PopupEntity(Loc.GetString("injector-component-feel-prick-message"), target, target);
        _popup.PopupEntity(Loc.GetString("medibot-target-injected"), medibot, medibot);

        _audio.PlayPredicted(medibot.Comp.InjectSound, medibot, medibot);

        // Keep every medibot, including the CMU one, from immediately treating the same patient again.
        RemComp<CMUMedibotReservationComponent>(target);
        var recentlyInjected = EnsureComp<NPCRecentlyInjectedComponent>(target);
        recentlyInjected.RemoveTime = TimeSpan.FromMinutes(1);
        recentlyInjected.Accumulator = 0;

        BeginFieldTreatment(medibot.Owner, target);

        return true;
    }

    /// <summary>
    /// Whether the patient has damage the CMU field treatment can handle.
    /// </summary>
    public bool HasFieldTreatableDamage(EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return false;

        var groups = _damageable.GetDamagePerGroup((target, damageable));
        return groups.TryGetValue("Brute", out var brute) && brute > FixedPoint2.Zero ||
               groups.TryGetValue("Burn", out var burn) && burn > FixedPoint2.Zero;
    }

    /// <summary>
    /// Starts or refreshes the CMU field treatment on a patient.
    /// </summary>
    public void BeginFieldTreatment(EntityUid medibot, EntityUid target)
    {
        if (!TryComp<CMUMedibotHealingComponent>(medibot, out var healing))
            return;

        var fieldTreatment = EnsureComp<CMUMedibotTreatmentComponent>(target);
        fieldTreatment.Healer = medibot;
        fieldTreatment.Position = Transform(target).LocalPosition;
        fieldTreatment.StartDelay = healing.StartDelay;
        fieldTreatment.Interval = healing.Interval;
        fieldTreatment.NextHeal = _timing.CurTime + healing.StartDelay;
        fieldTreatment.BruteHeal = healing.BruteHeal;
        fieldTreatment.BurnHeal = healing.BurnHeal;
        fieldTreatment.MaxPatientSpeed = healing.MaxPatientSpeed;
    }

    private bool TryInjectFromBeaker(
        EntityUid medibot,
        EntityUid target,
        CMUMedibotComponent component,
        Entity<SolutionComponent> injectable)
    {
        if (!ShouldInjectFromBeaker(component, target))
            return false;

        if (_itemSlots.GetItemOrNull(medibot, component.BeakerSlotId) is not { } beaker)
            return false;

        if (!_solutionContainer.TryGetSolution(beaker, "beaker", out var beakerSolution, out var solution))
            return false;

        var amount = FixedPoint2.Min(component.InjectAmount, solution.Volume);
        if (amount <= FixedPoint2.Zero)
            return false;

        var removed = _solutionContainer.SplitSolution(beakerSolution.Value, amount);
        if (removed.Volume <= FixedPoint2.Zero)
            return false;

        return _solutionContainer.TryAddSolution(injectable, removed);
    }

    public bool ShouldInjectFromBeaker(CMUMedibotComponent component, EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return false;

        var groups = _damageable.GetDamagePerGroup((target, damageable));
        var hasBrute = groups.TryGetValue("Brute", out var brute) && brute > FixedPoint2.Zero;
        var hasBurn = groups.TryGetValue("Burn", out var burn) && burn > FixedPoint2.Zero;

        if (component.InjectOnBrute && hasBrute || component.InjectOnBurn && hasBurn)
            return true;

        return component.InjectOnCritical &&
               TryComp<MobStateComponent>(target, out var mobState) &&
               mobState.CurrentState == MobState.Critical;
    }
}

[Serializable, NetSerializable]
public sealed partial class MedibotInjectDoAfterEvent : SimpleDoAfterEvent { }
