using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Decals;
using Content.Server.NPC.HTN;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Decals;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.NPC.Components;
using Content.Shared.Silicons.Bots;
using Content.Shared.UserInterface;
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
            Assert.That(prototypes.HasIndex<EntityPrototype>("CMUCleanBot"), Is.True);
            var pack = prototypes.Index<LatheRecipePackPrototype>("CMURoboticsFabricatorRecipes");
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsCleanBot"));
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsHugBot"));
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsMedibot"));

            var bot = SEntMan.SpawnEntity("CMUMedibot", map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUMedibotHealingComponent>(bot), Is.True);
            Assert.That(SEntMan.HasComponent<CMUMedibotComponent>(bot), Is.True);
            Assert.That(SEntMan.HasComponent<MedibotComponent>(bot), Is.True);
            Assert.That(SEntMan.GetComponent<MedibotComponent>(bot).Treatments.Keys,
                Does.Contain(Content.Shared.Mobs.MobState.Alive));

            // Keep the bot in place so this test measures only the passive field treatment.
            SEntMan.GetComponent<HTNComponent>(bot).Enabled = false;

            patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            SEntMan.EnsureComponent<NpcFactionMemberComponent>(patient).Factions.Add("AUWeYu");
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

        await Pair.RunTicksSync(120);

        await Server.WaitAssertion(() =>
        {
            var damageable = SComp<DamageableComponent>(patient);
            var total = SEntMan.System<DamageableSystem>().GetTotalDamage((patient, damageable)).Float();
            Assert.That(total, Is.LessThan(60f),
                "A still Weyland-Yutani patient next to the CMU medibot must receive suture and regenerative-mesh healing.");
        });
    }

    [Test]
    public async Task MedibotInjectsBeakerDoseAndKeyChangesSettings()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bot = SEntMan.SpawnEntity("CMUMedibot", map.GridCoords.Offset(new(1, 0)));
            var patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(2, 0)));
            SEntMan.EnsureComponent<NpcFactionMemberComponent>(patient).Factions.Add("AUWeYu");

            var damage = new DamageSpecifier
            {
                DamageDict =
                {
                    ["Blunt"] = 20,
                },
            };
            SEntMan.System<DamageableSystem>().TryChangeDamage(patient, damage, ignoreResistances: true);

            var medibot = SComp<MedibotComponent>(bot);
            Assert.That(SEntMan.System<MedibotSystem>().TryInject((bot, medibot), patient), Is.True,
                "A medibot with its starting tricordrazine beaker must be able to inject a brute-damaged patient.");

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetInjectableSolution(patient, out var injectable, out _), Is.True);
            Assert.That(injectable!.Value.Comp.Solution.GetTotalPrototypeQuantity("CMTricordrazine"),
                Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(SEntMan.System<MedibotSystem>().CheckInjectable((bot, medibot), patient, manual: true), Is.False,
                "A patient must not be serviced by another medibot immediately after an injection.");

            var key = SEntMan.SpawnEntity("CMUDroneConfigurationKey", map.GridCoords.Offset(new(-1, 0)));
            var hands = SEntMan.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, key), Is.True);

            var afterInteract = new AfterInteractEvent(
                user,
                key,
                bot,
                SEntMan.GetComponent<TransformComponent>(bot).Coordinates,
                true);
            SEntMan.EventBus.RaiseLocalEvent(key, afterInteract);
            Assert.That(afterInteract.Handled, Is.True);

            var medibotComponent = SComp<CMUMedibotComponent>(bot);
            Assert.That(medibotComponent.ConfigUser, Is.EqualTo(user));

            SEntMan.System<SharedUserInterfaceSystem>().RaiseUiMessage(
                bot,
                CMUMedibotConfigurationUi.Key,
                new CMUMedibotConfigurationApplyMsg(25, false, true, false)
                {
                    Actor = user,
                });

            Assert.That(medibotComponent.InjectAmount.Int(), Is.EqualTo(25));
            Assert.That(medibotComponent.InjectOnBrute, Is.False);
            Assert.That(medibotComponent.InjectOnBurn, Is.True);
            Assert.That(medibotComponent.InjectOnCritical, Is.False);
        });
    }

    [Test]
    public async Task CleanbotRemovesBloodDecal()
    {
        var map = await Pair.CreateTestMap();
        DecalIndex blood = default;

        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnEntity("CMUCleanBot", map.GridCoords);
            var decals = SEntMan.System<DecalSystem>();
            Assert.That(decals.TryAddDecal(
                "CMUBloodDroplet1",
                map.GridCoords,
                out blood,
                cleanable: true), Is.True);
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var decals = SEntMan.System<DecalSystem>();
            Assert.That(decals.ContainsDecal(map.Grid.Owner, blood), Is.False,
                "The CMU cleanbot must walk to and remove cleanable blood decals.");
        });
    }
}
