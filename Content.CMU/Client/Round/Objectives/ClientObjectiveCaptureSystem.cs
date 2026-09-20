using Content.Shared.CMU14.Round.Objectives.Type;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.CMU14.Round.Objectives;

public sealed partial class ClientObjectiveCaptureSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CaptureObjectiveComponent, AfterAutoHandleStateEvent>(OnCaptureObjectiveState);
    }

    private void OnCaptureObjectiveState(Entity<CaptureObjectiveComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateFlagSpriteState(ent);
    }

    private void UpdateFlagSpriteState(Entity<CaptureObjectiveComponent> ent)
    {
        if (string.IsNullOrEmpty(ent.Comp.CurrentSpriteState))
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // The flag sprite is a single layer defined by the wall flag prototypes.
        _sprite.LayerSetRsiState((ent, sprite), 0, ent.Comp.CurrentSpriteState);
    }
}
