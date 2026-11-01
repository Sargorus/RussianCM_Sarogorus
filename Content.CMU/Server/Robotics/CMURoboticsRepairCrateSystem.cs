using Content.Shared.CMU14.Robotics;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Marks the damaged units from a repair-order crate with the order id so the
/// console can verify them on the cargo elevator. Marking happens before the
/// crate empties its contents, and the empty crate is removed after opening.
/// </summary>
public sealed partial class CMURoboticsRepairCrateSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMURoboticsRepairCrateComponent, StorageBeforeOpenEvent>(OnBeforeOpened);
        SubscribeLocalEvent<CMURoboticsRepairCrateComponent, StorageAfterOpenEvent>(OnOpened);
        SubscribeLocalEvent<CMURoboticsRepairCrateComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnBeforeOpened(Entity<CMURoboticsRepairCrateComponent> ent, ref StorageBeforeOpenEvent args)
    {
        MarkContents(ent);
    }

    private void OnOpened(Entity<CMURoboticsRepairCrateComponent> ent, ref StorageAfterOpenEvent args)
    {
        // Opening ejects the contents. Mark them by proximity as a fallback in
        // case the crate was filled while already open or the container changed.
        MarkNearby(ent);
        QueueDel(ent.Owner);
    }

    private void OnTerminating(Entity<CMURoboticsRepairCrateComponent> ent, ref EntityTerminatingEvent args)
    {
        MarkContents(ent);
    }

    private void MarkContents(Entity<CMURoboticsRepairCrateComponent> ent)
    {
        if (ent.Comp.Marked)
            return;

        ent.Comp.Marked = true;
        if (!TryComp<EntityStorageComponent>(ent.Owner, out var storage))
            return;

        foreach (var contained in storage.Contents.ContainedEntities)
        {
            Mark(contained, ent.Comp.OrderId);
        }
    }

    private void MarkNearby(Entity<CMURoboticsRepairCrateComponent> ent)
    {
        if (ent.Comp.Marked)
            return;

        ent.Comp.Marked = true;
        var coordinates = Transform(ent.Owner).Coordinates;

        foreach (var candidate in _lookup.GetEntitiesInRange(coordinates, 2f))
        {
            Mark(candidate, ent.Comp.OrderId);
        }
    }

    private void Mark(EntityUid candidate, int orderId)
    {
        if (!HasComp<CMURoboticsDamagedBodyComponent>(candidate))
            return;

        var marker = EnsureComp<CMURoboticsOrderTargetComponent>(candidate);
        if (marker.OrderId != 0 && marker.OrderId != orderId)
            return;

        marker.OrderId = orderId;
    }
}
