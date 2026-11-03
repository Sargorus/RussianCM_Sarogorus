using Content.Server.Access.Components;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Dialog;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>
/// Gives the liaison safe its reinforced health and lets a character carrying
/// the corporate liaison ID card set a PIN for that specific safe.
/// </summary>
public sealed partial class CMULiaisonSafeSystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> LiaisonJob = "AU14JobCivilianCorporateLiaison";

    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMULiaisonSafeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMULiaisonSafeComponent, ActivateInWorldEvent>(OnActivate, before: [typeof(LockSystem)]);
        SubscribeLocalEvent<CMULiaisonSafeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMULiaisonSafeComponent, CMULiaisonSafePinEvent>(OnPinEntered);
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

        // A safe without a PIN behaves as a normal locker.
        ent.Comp.Pin = string.Empty;
        ent.Comp.PinProtected = false;
        ClearPinChange(ent.Comp);
        if (TryComp<LockComponent>(ent.Owner, out var lockComp))
            _lock.Unlock(ent.Owner, null, lockComp);
    }

    private void OnActivate(Entity<CMULiaisonSafeComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled
            || !args.Complex
            || string.IsNullOrEmpty(ent.Comp.Pin))
            return;

        args.Handled = true;

        // An open safe closes on a normal left click. A closed safe asks for the PIN.
        if (TryComp<EntityStorageComponent>(ent.Owner, out var storage) && storage.Open)
        {
            _storage.TryCloseStorage(ent.Owner, args.User);
            return;
        }

        OpenPinDialog(ent, args.User, CMULiaisonSafePinAction.Unlock);
    }

    private void OnGetVerbs(Entity<CMULiaisonSafeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        var user = args.User;
        var hasPin = !string.IsNullOrEmpty(ent.Comp.Pin);

        if (HasLiaisonIdCard(user))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(hasPin
                    ? "cmu-liaison-safe-change-pin-verb"
                    : "cmu-liaison-safe-set-pin-verb"),
                Priority = 3,
                Act = () => OpenPinDialog(
                    ent,
                    user,
                    hasPin
                        ? CMULiaisonSafePinAction.VerifyCurrent
                        : CMULiaisonSafePinAction.Set),
            });
        }

        if (!hasPin || !TryComp<LockComponent>(ent.Owner, out var lockComp))
            return;

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
                    OpenPinDialog(ent, user, CMULiaisonSafePinAction.Unlock);
                else
                    LockSafe(ent, user, lockComp);
            },
        });
    }

    private void OpenPinDialog(
        Entity<CMULiaisonSafeComponent> ent,
        EntityUid user,
        CMULiaisonSafePinAction action)
    {
        if (action != CMULiaisonSafePinAction.Set)
            ClearPinChange(ent.Comp);

        var (title, prompt) = action switch
        {
            CMULiaisonSafePinAction.Set => (
                "cmu-liaison-safe-set-pin-title",
                "cmu-liaison-safe-set-pin-prompt"),
            CMULiaisonSafePinAction.VerifyCurrent => (
                "cmu-liaison-safe-current-pin-title",
                "cmu-liaison-safe-current-pin-prompt"),
            _ => (
                "cmu-liaison-safe-pin-title",
                "cmu-liaison-safe-pin-prompt"),
        };

        _dialog.OpenInput(
            ent.Owner,
            user,
            Loc.GetString(prompt),
            new CMULiaisonSafePinEvent(GetNetEntity(user), action),
            characterLimit: 4,
            minCharacterLimit: 4,
            title: Loc.GetString(title));
    }

    private void LockSafe(Entity<CMULiaisonSafeComponent> ent, EntityUid user, LockComponent lockComp)
    {
        _storage.TryCloseStorage(ent.Owner, user);
        _lock.Lock(ent.Owner, user, lockComp);
        _popup.PopupEntity(Loc.GetString("cmu-liaison-safe-locked"), ent, user);
    }

    private void OnPinEntered(Entity<CMULiaisonSafeComponent> ent, ref CMULiaisonSafePinEvent args)
    {
        if (!TryGetEntity(args.Actor, out var actor)
            || actor is not { } user
            || TerminatingOrDeleted(user))
            return;

        // If the dialog still exists, its owner is the only actor allowed to answer.
        // DialogSystem closes the dialog before raising the input event, so the
        // actor carried by the event is the authoritative check.
        if (TryComp<DialogComponent>(ent.Owner, out var dialog) && dialog.Actor != user)
            return;

        if (args.Action == CMULiaisonSafePinAction.Set)
        {
            SetPin(ent, user, args.Message);
            return;
        }

        if (args.Action == CMULiaisonSafePinAction.VerifyCurrent)
        {
            VerifyCurrentPin(ent, user, args.Message);
            return;
        }

        if (string.IsNullOrEmpty(ent.Comp.Pin))
            return;

        if (args.Message != ent.Comp.Pin)
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

    private void VerifyCurrentPin(Entity<CMULiaisonSafeComponent> ent, EntityUid user, string pin)
    {
        if (!HasLiaisonIdCard(user))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-liaison-safe-pin-no-card"),
                ent,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (string.IsNullOrEmpty(ent.Comp.Pin))
            return;

        if (pin != ent.Comp.Pin)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-liaison-safe-pin-wrong"),
                ent,
                user,
                PopupType.SmallCaution);
            return;
        }

        ent.Comp.PinChangeUser = user;
        ent.Comp.PinChangeVerified = true;
        OpenPinDialog(ent, user, CMULiaisonSafePinAction.Set);
    }

    private void SetPin(Entity<CMULiaisonSafeComponent> ent, EntityUid user, string pin)
    {
        if (!HasLiaisonIdCard(user))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-liaison-safe-pin-no-card"),
                ent,
                user,
                PopupType.SmallCaution);
            return;
        }

        var changing = !string.IsNullOrEmpty(ent.Comp.Pin);
        if (changing
            && (!ent.Comp.PinChangeVerified || ent.Comp.PinChangeUser != user))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-liaison-safe-pin-current-required"),
                ent,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (!IsValidPin(pin))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-liaison-safe-pin-invalid"),
                ent,
                user,
                PopupType.SmallCaution);
            return;
        }

        ent.Comp.Pin = pin;
        ent.Comp.PinProtected = true;
        ClearPinChange(ent.Comp);
        Dirty(ent.Owner, ent.Comp);
        _popup.PopupEntity(Loc.GetString("cmu-liaison-safe-pin-set"), ent, user);
    }

    private static void ClearPinChange(CMULiaisonSafeComponent component)
    {
        component.PinChangeUser = null;
        component.PinChangeVerified = false;
    }

    private bool HasLiaisonIdCard(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (IsLiaisonIdCard(held))
                return true;
        }

        return _inventory.TryGetSlotEntity(user, "id", out var slot)
            && slot is { } card
            && IsLiaisonIdCard(card);
    }

    private bool IsLiaisonIdCard(EntityUid item)
    {
        if (!_idCard.TryGetIdCard(item, out var idCard))
            return false;

        return TryComp<PresetIdCardComponent>(idCard.Owner, out var preset)
            && preset.JobName == LiaisonJob;
    }

    private static bool IsValidPin(string pin)
    {
        if (pin.Length != 4)
            return false;

        foreach (var character in pin)
        {
            if (!char.IsAsciiDigit(character))
                return false;
        }

        return true;
    }
}
