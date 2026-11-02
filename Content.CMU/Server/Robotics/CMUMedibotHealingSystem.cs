using Content.Shared.CMU14.Robotics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Applies the CMU medical bot's passive field treatment. A nearby patient who
/// stays still receives suture-equivalent brute healing and mesh-equivalent
/// burn healing. No consumable item is used.
/// </summary>
public sealed partial class CMUMedibotHealingSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUMedibotHealingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var healing, out var xform))
        {
            if (now < healing.NextHeal)
                continue;

            healing.NextHeal = now + healing.Interval;

            foreach (var target in _lookup.GetEntitiesInRange<DamageableComponent>(xform.Coordinates, healing.Range))
            {
                if (target.Owner == uid ||
                    !TryComp<MobStateComponent>(target.Owner, out var mobState) ||
                    mobState.CurrentState is not (MobState.Alive or MobState.Critical) ||
                    !_interaction.InRangeUnobstructed(uid, target.Owner))
                {
                    continue;
                }

                // The patient must stay still for the field treatment to continue.
                if (TryComp<PhysicsComponent>(target.Owner, out var body) &&
                    body.LinearVelocity.LengthSquared() > healing.MaxPatientSpeed * healing.MaxPatientSpeed)
                {
                    continue;
                }

                var damage = new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Blunt"] = -healing.BruteHeal,
                        ["Piercing"] = -healing.BruteHeal,
                        ["Slash"] = -healing.BruteHeal,
                        ["Heat"] = -healing.BurnHeal,
                        ["Cold"] = -healing.BurnHeal,
                        ["Shock"] = -healing.BurnHeal,
                        ["Caustic"] = -healing.BurnHeal,
                    },
                };

                _damageable.TryChangeDamage(target.Owner, damage, ignoreResistances: true, origin: uid);
            }
        }
    }
}
