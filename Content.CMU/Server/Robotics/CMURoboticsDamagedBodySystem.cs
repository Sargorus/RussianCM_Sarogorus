using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Applies the missing limbs and burn/brute damage used by repair-order androids.
/// Runs after the initial body has been assembled.
/// </summary>
public sealed partial class CMURoboticsDamagedBodySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMURoboticsDamagedBodyComponent, MapInitEvent>(
            OnMapInit,
            after: new[] { typeof(InitialBodySystem) });
    }

    private void OnMapInit(Entity<CMURoboticsDamagedBodyComponent> ent, ref MapInitEvent args)
    {
        var heat = _prototypes.Index<DamageTypePrototype>("Heat");
        _damageable.TryChangeDamage(
            ent.Owner,
            new DamageSpecifier(heat, ent.Comp.BurnDamage),
            ignoreResistances: true);

        var blunt = _prototypes.Index<DamageTypePrototype>("Blunt");
        _damageable.TryChangeDamage(
            ent.Owner,
            new DamageSpecifier(blunt, ent.Comp.BruteDamage),
            ignoreResistances: true);

        if (!TryComp<BodyComponent>(ent.Owner, out var body))
            return;

        foreach (var type in ent.Comp.SeverParts)
        {
            var part = _body.GetBodyChildrenOfType(ent.Owner, type, body).FirstOrDefault();
            if (part.Id == default)
                continue;

            var ev = new BodyPartSeverAttemptEvent(ent.Owner, part.Id, type, Surgical: true);
            RaiseLocalEvent(part.Id, ref ev);

            if (ev.Succeeded &&
                ev.DetachedBody is { } detached &&
                !TerminatingOrDeleted(detached))
            {
                QueueDel(detached);
            }
        }
    }
}
