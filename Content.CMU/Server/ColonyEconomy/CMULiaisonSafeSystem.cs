using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.GameTicking.Events;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Dialog;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>
/// Gives the liaison safe its reinforced health and a PIN that is handed to the
/// corporate liaison's character notes when that role spawns.
/// </summary>
public sealed partial class CMULiaisonSafeSystem : EntitySystem
{
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string LiaisonJob = "AU14JobCivilianCorporateLiaison";

    private int _roundPin;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<CMULiaisonSafeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMULiaisonSafeComponent, ActivateInWorldEvent>(OnActivate, before: [typeof(LockSystem)]);
        SubscribeLocalEvent<CMULiaisonSafeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMULiaisonSafeComponent, CMULiaisonSafePinEvent>(OnPinEntered);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        _roundPin = 0;
    }

    private void OnMapInit(Entity<CMULiaisonSafeComponent> ent, ref MapInitEvent args)
    {
        // List-valued YAML inheritance appends the parent's 100-damage closet
        // threshold, so replace the inherited thresholds instead of adding another.
        var destructible = EnsureComp<DestructibleComponent>(ent.Owner);
        _destructible.ReplaceThresholds(
            (ent.Owner, destructible),
            new List<DamageThreshold>
            {
                new()
                {
                    Trigger = new DamageTrigger { Damage = FixedPoint2.New(ent.Comp.HitPoints) },
                    Behaviors = new List<IThresholdBehavior>
                    {
                        new DoActsBehavior { Acts = ThresholdActs.Destruction },
                        new PlaySoundBehavior
                        {
                            Sound = new SoundCollectionSpecifier("MetalBreak")
                            {
                                Params = new AudioParams { Volume = -6 },
                            },
                        },
                    },
                },
            });

        ent.Comp.Pin = GetOrCreatePin();
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId != LiaisonJob)
            return;

        EnsureComp<CMULiaisonPinComponent>(args.Mob).Pin = GetOrCreatePin();
    }

    private int GetOrCreatePin()
    {
        if (_roundPin <= 0)
            _roundPin = _random.Next(1000, 10000);

        return _roundPin;
    }

    private void OnActivate(Entity<CMULiaisonSafeComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;

        // An open safe closes on a normal left click. A closed safe always asks for the PIN.
        if (TryComp<EntityStorageComponent>(ent.Owner, out var storage) && storage.Open)
        {
            _storage.TryCloseStorage(ent.Owner, args.User);
            return;
        }

        OpenPinDialog(ent, args.User);
    }

    private void OnGetVerbs(Entity<CMULiaisonSafeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (!TryComp<LockComponent>(ent.Owner, out var lockComp))
            return;

        var user = args.User;
        var locked = lockComp.Locked;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(locked
                ? "cmu-liaison-safe-unlock-verb"
                : "cmu-liaison-safe-lock-verb"),
            Priority = 2,
            Act = () =>
            {
                if (locked)
                    OpenPinDialog(ent, user);
                else
                    LockSafe(ent, user, lockComp);
            },
        });
    }

    private void OpenPinDialog(Entity<CMULiaisonSafeComponent> ent, EntityUid user)
    {
        _dialog.OpenInput(
            ent.Owner,
            user,
            Loc.GetString("cmu-liaison-safe-pin-prompt"),
            new CMULiaisonSafePinEvent(GetNetEntity(user)),
            characterLimit: 4,
            minCharacterLimit: 4,
            title: Loc.GetString("cmu-liaison-safe-pin-title"));
    }

    private void LockSafe(Entity<CMULiaisonSafeComponent> ent, EntityUid user, LockComponent lockComp)
    {
        _storage.TryCloseStorage(ent.Owner, user);
        _lock.Lock(ent.Owner, user, lockComp);
        _popup.PopupEntity(Loc.GetString("cmu-liaison-safe-locked"), ent, user);
    }

    private void OnPinEntered(Entity<CMULiaisonSafeComponent> ent, ref CMULiaisonSafePinEvent args)
    {
        if (!TryGetEntity(args.Actor, out var actor) ||
            actor is not { } user ||
            TerminatingOrDeleted(user))
        {
            return;
        }

        // If the dialog still exists, its owner is the only actor allowed to answer.
        // DialogSystem closes the dialog before raising the input event, so the
        // actor carried by the event is the authoritative check.
        if (TryComp<DialogComponent>(ent.Owner, out var dialog) &&
            dialog.Actor != user)
        {
            return;
        }

        if (args.Message != ent.Comp.Pin.ToString())
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-liaison-safe-pin-wrong"),
                ent,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (TryComp<LockComponent>(ent.Owner, out var lockComp))
            _lock.Unlock(ent.Owner, user, lockComp);

        _popup.PopupEntity(Loc.GetString("cmu-liaison-safe-pin-accepted"), ent, user);
        _storage.TryOpenStorage(user, ent.Owner);
    }
}
