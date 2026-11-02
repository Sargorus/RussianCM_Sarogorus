using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Crate;

public sealed partial class CrateOpenableSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CrateOpenableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CrateOpenableComponent, AttackedEvent>(OnAttacked);
    }

    private void OnInteractUsing(Entity<CrateOpenableComponent> ent, ref InteractUsingEvent args)
    {
        if (EntityManager.IsQueuedForDeletion(ent))
            return;

        if (!_tool.HasQuality(args.Used, ent.Comp.Tool))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.WrongToolPopup), ent, args.User, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        Open(ent, args.User);
    }

    /// <summary>
    /// A crowbar hit should unpack the crate as well as a use-interaction.
    /// Cargo crates often sit closed on the platform, where attacking is the
    /// natural way to apply the pry bar.
    /// </summary>
    private void OnAttacked(Entity<CrateOpenableComponent> ent, ref AttackedEvent args)
    {
        if (EntityManager.IsQueuedForDeletion(ent) || !_tool.HasQuality(args.Used, ent.Comp.Tool))
            return;

        Open(ent, args.User);
    }

    private void Open(Entity<CrateOpenableComponent> ent, EntityUid user)
    {
        _audio.PlayPredicted(ent.Comp.Sound, _transform.GetMoverCoordinates(ent), user);

        if (_net.IsClient)
            return;

        QueueDel(ent);

        var spawns = EntitySpawnCollection.GetSpawns(ent.Comp.Spawn, _random);
        foreach (var spawn in spawns)
        {
            // cmu edit start: let the server swap what the crate holds, e.g. for the ship's platoon rations
            var ev = new Content.Shared.CMU14.Crate.CMUCrateOpenableSpawnEvent(spawn);
            RaiseLocalEvent(ent, ref ev);
            TrySpawnNextTo(ev.Prototype, ent, out _);
            // cmu edit end
        }
    }
}
