using System.Numerics;
using Content.Shared.CMU14.Robotics;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared._RMC14.Synth;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Applies the CMU medical bot's field treatment to Weyland-Yutani humans.
/// The treatment is placed on the patient after a successful injection and
/// continues while they remain still: three seconds of stillness, then a
/// suture-equivalent brute and regenerative-mesh-equivalent burn heal every
/// two seconds. No item is consumed and the bot does not need to stay nearby.
/// </summary>
public sealed partial class CMUMedibotHealingSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUMedibotTreatmentComponent, DamageableComponent, TransformComponent>();
        while (query.MoveNext(out var patient, out var treatment, out var damageable, out var xform))
        {
            if (!IsValidPatient(patient) || !HasHealableDamage(patient, damageable))
            {
                RemComp<CMUMedibotTreatmentComponent>(patient);
                continue;
            }

            if (TryComp<PhysicsComponent>(patient, out var body) &&
                body.LinearVelocity.LengthSquared() > treatment.MaxPatientSpeed * treatment.MaxPatientSpeed)
            {
                treatment.Position = xform.LocalPosition;
                treatment.NextHeal = now + treatment.StartDelay;
                continue;
            }

            if ((xform.LocalPosition - treatment.Position).LengthSquared() > 0.01f)
            {
                treatment.Position = xform.LocalPosition;
                treatment.NextHeal = now + treatment.StartDelay;
                continue;
            }

            if (now < treatment.NextHeal)
                continue;

            treatment.NextHeal = now + treatment.Interval;
            ApplyHealing(patient, treatment);
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

    private void ApplyHealing(EntityUid patient, CMUMedibotTreatmentComponent treatment)
    {
        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = -treatment.BruteHeal,
                ["Piercing"] = -treatment.BruteHeal,
                ["Slash"] = -treatment.BruteHeal,
                ["Heat"] = -treatment.BurnHeal,
                ["Cold"] = -treatment.BurnHeal,
                ["Shock"] = -treatment.BurnHeal,
                ["Caustic"] = -treatment.BurnHeal,
            },
        };

        _damageable.TryChangeDamage(patient, damage, ignoreResistances: true, origin: treatment.Healer);
    }
}
