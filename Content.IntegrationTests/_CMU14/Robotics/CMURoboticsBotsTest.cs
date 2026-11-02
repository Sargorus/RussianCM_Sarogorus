using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.HTN;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Silicons.Bots;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Robotics;

[TestFixture]
public sealed class CMURoboticsBotsTest : GameTest
{
    [Test]
    public async Task FabricatorBuildsBotsAndMedibotTreatsStillPatients()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var pack = prototypes.Index<LatheRecipePackPrototype>("CMURoboticsFabricatorRecipes");
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsCleanBot"));
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsHugBot"));
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsMedibot"));

            var bot = SEntMan.SpawnEntity("CMUMedibot", map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUMedibotHealingComponent>(bot), Is.True);
            Assert.That(SEntMan.HasComponent<MedibotComponent>(bot), Is.True);
            Assert.That(SEntMan.GetComponent<MedibotComponent>(bot).Treatments.Keys,
                Does.Contain(Content.Shared.Mobs.MobState.Alive));

            // Keep the bot in place so this test measures only the passive field treatment.
            SEntMan.GetComponent<HTNComponent>(bot).Enabled = false;

            patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var damage = new DamageSpecifier
            {
                DamageDict =
                {
                    ["Blunt"] = 30,
                    ["Heat"] = 30,
                },
            };
            SEntMan.System<DamageableSystem>().TryChangeDamage(patient, damage, ignoreResistances: true);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var damageable = SComp<DamageableComponent>(patient);
            var total = SEntMan.System<DamageableSystem>().GetTotalDamage((patient, damageable)).Float();
            Assert.That(total, Is.LessThan(60f),
                "A still patient next to the CMU medibot must receive suture and regenerative-mesh healing.");
        });
    }
}
