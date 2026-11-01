using Content.Shared.CMU14.Requisitions;
using Content.Shared.Storage.Components;

namespace Content.Server.CMU14.Requisitions;

/// <summary>
/// Deploys the payload of a CMUDeployOnOpen crate when the crate is opened or
/// destroyed, so the product cannot be lost by prying the crate apart.
/// </summary>
public sealed partial class CMUDeployOnOpenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUDeployOnOpenComponent, StorageAfterOpenEvent>(OnStorageOpened);
        SubscribeLocalEvent<CMUDeployOnOpenComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnStorageOpened(Entity<CMUDeployOnOpenComponent> ent, ref StorageAfterOpenEvent args)
    {
        Deploy(ent);
    }

    private void OnTerminating(Entity<CMUDeployOnOpenComponent> ent, ref EntityTerminatingEvent args)
    {
        Deploy(ent);
    }

    private void Deploy(Entity<CMUDeployOnOpenComponent> ent)
    {
        if (ent.Comp.Deployed)
            return;

        ent.Comp.Deployed = true;
        var coordinates = Transform(ent.Owner).Coordinates;

        foreach (var prototype in ent.Comp.Spawn)
            Spawn(prototype, coordinates);

        QueueDel(ent.Owner);
    }
}
