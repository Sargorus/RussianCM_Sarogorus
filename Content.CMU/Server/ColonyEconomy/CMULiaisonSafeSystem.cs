using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
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
    private const int SafeHitPoints = 2000;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMULiaisonSafeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMULiaisonSafeComponent, ActivateInWorldEvent>(OnActivate, before: [typeof(LockSystem)]);
        SubscribeLocalEvent<CMULiaisonSafeComponent, CMULiaisonSafePinEvent>(OnPinEntered);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnMapInit(Entity<CMULiaisonSafeComponent> ent, ref MapInitEvent args)
    {
        // Replace the inherited 100-damage closet threshold with the reinforced safe threshold.
        RemComp<DestructibleComponent>(ent.Owner);
        var destructible = AddComp<DestructibleComponent>(ent.Owner);
        _destructible.AddThreshold(
            (ent.Owner, destructible),
            new DamageThreshold
            {
                Trigger = new DamageTrigger { Damage = FixedPoint2.New(SafeHitPoints) },
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
            null);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId != LiaisonJob)
            return;

        var pin = 0;
        var query = EntityQueryEnumerator<CMULiaisonSafeComponent>();
        while (query.MoveNext(out _, out var existing))
        {
            if (existing.Pin <= 0)
                continue;

            pin = existing.Pin;
            break;
        }

        if (pin <= 0)
            pin = _random.Next(1000, 10000);

        var assigned = false;
        query = EntityQueryEnumerator<CMULiaisonSafeComponent>();
        while (query.MoveNext(out _, out var safe))
        {
            safe.Pin = pin;
            assigned = true;
        }

        if (assigned)
            EnsureComp<CMULiaisonPinComponent>(args.Mob).Pin = pin;
    }

    private void OnActivate(Entity<CMULiaisonSafeComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || ent.Comp.Pin <= 0)
            return;

        if (!TryComp<LockComponent>(ent.Owner, out var lockComp) || !lockComp.Locked)
            return;

        if (!_lock.HasUserAccess((ent.Owner, lockComp), args.User, quiet: true))
            return;

        ent.Comp.PendingUser = args.User;
        args.Handled = true;

        _dialog.OpenInput(
            ent.Owner,
            args.User,
            Loc.GetString("cmu-liaison-safe-pin-prompt"),
            new CMULiaisonSafePinEvent(),
            characterLimit: 4,
            minCharacterLimit: 4,
            title: Loc.GetString("cmu-liaison-safe-pin-title"));
    }

    private void OnPinEntered(Entity<CMULiaisonSafeComponent> ent, ref CMULiaisonSafePinEvent args)
    {
        var user = ent.Comp.PendingUser;
        ent.Comp.PendingUser = null;

        if (user is not { } actor || TerminatingOrDeleted(actor))
            return;

        if (args.Message != ent.Comp.Pin.ToString())
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-liaison-safe-pin-wrong"),
                ent,
                actor,
                PopupType.SmallCaution);
            return;
        }

        if (TryComp<LockComponent>(ent.Owner, out var lockComp))
            _lock.Unlock(ent.Owner, actor, lockComp);

        _popup.PopupEntity(Loc.GetString("cmu-liaison-safe-pin-accepted"), ent, actor);
        _storage.TryOpenStorage(actor, ent.Owner);
    }
}
