using System;
using System.Collections.Generic;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Applies randomized missing limbs and burn/brute damage used by repair-order
/// androids. Runs after the initial body has been assembled.
/// </summary>
public sealed partial class CMURoboticsDamagedBodySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
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
        if (!TryComp<BodyComponent>(ent.Owner, out var body))
            return;

        SeverRandomLimbs(ent, body);

        var burn = _random.Next(
            Math.Min(ent.Comp.MinBurnDamage, ent.Comp.MaxBurnDamage),
            Math.Max(ent.Comp.MinBurnDamage, ent.Comp.MaxBurnDamage) + 1);
        var brute = _random.Next(
            Math.Min(ent.Comp.MinBruteDamage, ent.Comp.MaxBruteDamage),
            Math.Max(ent.Comp.MinBruteDamage, ent.Comp.MaxBruteDamage) + 1);

        var heat = _prototypes.Index<DamageTypePrototype>("Heat");
        _damageable.TryChangeDamage(
            ent.Owner,
            new DamageSpecifier(heat, FixedPoint2.New(burn)),
            ignoreResistances: true);

        var blunt = _prototypes.Index<DamageTypePrototype>("Blunt");
        _damageable.TryChangeDamage(
            ent.Owner,
            new DamageSpecifier(blunt, FixedPoint2.New(brute)),
            ignoreResistances: true);
    }

    private void SeverRandomLimbs(Entity<CMURoboticsDamagedBodyComponent> ent, BodyComponent body)
    {
        var limbs = new List<(EntityUid Part, BodyPartType Type, BodyPartSymmetry Symmetry)>();
        foreach (var type in ent.Comp.SeverableParts)
        {
            foreach (var part in _body.GetBodyChildrenOfType(ent.Owner, type, body))
                limbs.Add((part.Id, type, part.Component.Symmetry));
        }

        if (limbs.Count == 0)
            return;

        _random.Shuffle(limbs);
        var min = Math.Max(0, ent.Comp.MinSeveredLimbs);
        var max = Math.Max(min, ent.Comp.MaxSeveredLimbs);
        var count = Math.Min(_random.Next(min, max + 1), limbs.Count);

        for (var i = 0; i < count; i++)
        {
            var limb = limbs[i];
            var ev = new BodyPartSeverAttemptEvent(ent.Owner, limb.Part, limb.Type, Surgical: true);
            RaiseLocalEvent(limb.Part, ref ev);

            if (ev.Succeeded &&
                ev.DetachedBody is { } detached &&
                !TerminatingOrDeleted(detached))
            {
                QueueDel(detached);
            }
        }
    }
}
