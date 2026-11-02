using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.DroneOperator;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Applies the CMU medical bot's field treatment to Weyland-Yutani humans.
/// A patient must stay still for three seconds; after that the bot applies
/// suture-equivalent brute healing and mesh-equivalent burn healing every two
/// seconds. Only one medibot treats a patient at a time and no item is used.
/// </summary>
public sealed partial class CMUMedibotHealingSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TreatmentState> _treatments = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var active = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<CMUMedibotHealingComponent, TransformComponent>();
        while (query.MoveNext(out var bot, out var healing, out var botXform))
        {
            foreach (var target in _lookup.GetEntitiesInRange<DamageableComponent>(botXform.Coordinates, healing.Range))
            {
                var patient = target.Owner;
                if (patient == bot ||
                    !IsValidPatient(patient) ||
                    !HasHealableDamage(patient, target.Comp) ||
                    !_interaction.InRangeUnobstructed(bot, patient))
                {
                    continue;
                }

                var patientXform = Transform(patient);
                if (TryComp<PhysicsComponent>(patient, out var body) &&
                    body.LinearVelocity.LengthSquared() > healing.MaxPatientSpeed * healing.MaxPatientSpeed)
                {
                    _treatments.Remove(patient);
                    continue;
                }

                if (_treatments.TryGetValue(patient, out var state))
                {
                    if (state.Bot != bot)
                        continue;

                    if ((patientXform.LocalPosition - state.Position).LengthSquared() > 0.01f)
                    {
                        state.Position = patientXform.LocalPosition;
                        state.NextHeal = now + healing.StartDelay;
                        active.Add(patient);
                        continue;
                    }

                    active.Add(patient);
                    if (now < state.NextHeal)
                        continue;

                    state.NextHeal = now + healing.Interval;
                }
                else
                {
                    _treatments[patient] = new TreatmentState
                    {
                        Bot = bot,
                        Position = patientXform.LocalPosition,
                        NextHeal = now + healing.StartDelay,
                    };
                    active.Add(patient);
                    continue;
                }

                ApplyHealing(bot, patient, healing);
            }
        }

        // Drop patients that are no longer near the bot assigned to them.
        foreach (var patient in _treatments.Keys.ToArray())
        {
            if (!active.Contains(patient))
                _treatments.Remove(patient);
        }
    }

    private bool IsValidPatient(EntityUid patient)
    {
        if (!TryComp<MobStateComponent>(patient, out var mobState) ||
            mobState.CurrentState is not (MobState.Alive or MobState.Critical) ||
            !HasComp<HumanoidProfileComponent>(patient) ||
            HasComp<CMUDroneAndroidComponent>(patient) ||
            HasComp<SynthComponent>(patient) ||
            !TryComp<NpcFactionMemberComponent>(patient, out var factions) ||
            !factions.Factions.Contains("AUWeYu"))
        {
            return false;
        }

        return true;
    }

    private bool HasHealableDamage(EntityUid patient, DamageableComponent damageable)
    {
        var groups = _damageable.GetDamagePerGroup((patient, damageable));
        return groups.TryGetValue("Brute", out var brute) && brute > FixedPoint2.Zero ||
               groups.TryGetValue("Burn", out var burn) && burn > FixedPoint2.Zero;
    }

    private void ApplyHealing(EntityUid bot, EntityUid patient, CMUMedibotHealingComponent healing)
    {
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

        _damageable.TryChangeDamage(patient, damage, ignoreResistances: true, origin: bot);
    }

    private sealed class TreatmentState
    {
        public EntityUid Bot;
        public Vector2 Position;
        public TimeSpan NextHeal;
    }
}
