using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Decals;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Decals;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Silicons.Bots;
using Content.Shared.UserInterface;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Medical.CPR;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Robotics;

[TestFixture]
public sealed class CMURoboticsBotsTest : GameTest
{
    [Test]
    public async Task FabricatorBuildsBotsWithoutStartingBeaker()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.HasIndex<EntityPrototype>("CMUCleanBot"), Is.True);
            Assert.That(prototypes.HasIndex<EntityPrototype>("CMUHugBot"), Is.True);

            var pack = prototypes.Index<LatheRecipePackPrototype>("CMURoboticsFabricatorRecipes");
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsCleanBot"));
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsHugBot"));
            Assert.That(pack.Recipes.Select(recipe => recipe.Id), Does.Contain("CMURoboticsMedibot"));

            var bot = SEntMan.SpawnEntity("CMUMedibot", map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUMedibotComponent>(bot), Is.True);
            Assert.That(SEntMan.HasComponent<MedibotComponent>(bot), Is.True);
            Assert.That(SEntMan.HasComponent<MarineComponent>(bot), Is.True);
            Assert.That(SEntMan.HasComponent<HandsComponent>(bot), Is.True);
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(bot, "cmu-medibot-beaker"), Is.Null,
                "The fabricator must produce the medical bot without a beaker.");
        });
    }

    [Test]
    public async Task MedibotInjectsAnyBeakerAndKeyChangesSettings()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bot = SEntMan.SpawnEntity("CMUMedibot", map.GridCoords.Offset(new(1, 0)));
            var patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(2, 0)));
            SEntMan.EnsureComponent<NpcFactionMemberComponent>(patient).Factions.Add("AUWeYu");

            InsertTricordBeaker(bot, user, map.GridCoords);

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
                "A medibot with an inserted beaker must be able to inject a brute-damaged patient.");

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
                new CMUMedibotConfigurationApplyMsg(25, false, true, false, new HashSet<string> { "GOVFOR" })
                {
                    Actor = user,
                });

            Assert.That(medibotComponent.InjectAmount.Int(), Is.EqualTo(25));
            Assert.That(medibotComponent.InjectOnBrute, Is.False);
            Assert.That(medibotComponent.InjectOnBurn, Is.True);
            Assert.That(medibotComponent.InjectOnCritical, Is.False);
            Assert.That(medibotComponent.TreatFactions, Is.EquivalentTo(new[] { "GOVFOR" }));
        });
    }

    [Test]
    public async Task MedibotCanStartCPROnIntactCorpse()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var bot = SEntMan.SpawnEntity("CMUMedibot", map.GridCoords);
            var corpse = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            SEntMan.EnsureComponent<NpcFactionMemberComponent>(corpse).Factions.Add("AUWeYu");

            var damage = new DamageSpecifier
            {
                DamageDict =
                {
                    ["Blunt"] = 1000,
                },
            };
            SEntMan.System<DamageableSystem>().TryChangeDamage(corpse, damage, ignoreResistances: true);

            Assert.That(SComp<MobStateComponent>(corpse).CurrentState, Is.EqualTo(MobState.Dead));
            Assert.That(SEntMan.System<CPRSystem>().TryStartCPR(bot, corpse), Is.True,
                "The medical bot must be able to start CPR on an intact Weyland-Yutani corpse.");
        });
    }

    [Test]
    public async Task CleanbotRemovesBloodDecal()
    {
        var map = await Pair.CreateTestMap();
        DecalIndex blood = default;
        DecalIndex footprint = default;

        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnEntity("CMUCleanBot", map.GridCoords);
            var decals = SEntMan.System<DecalSystem>();
            Assert.That(decals.TryAddDecal(
                "CMUBloodDroplet1",
                map.GridCoords,
                out blood,
                cleanable: true), Is.True);
            Assert.That(decals.TryAddDecal(
                "FootprintShoes",
                map.GridCoords,
                out footprint,
                cleanable: true), Is.True);
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var decals = SEntMan.System<DecalSystem>();
            Assert.That(decals.ContainsDecal(map.Grid.Owner, blood), Is.False,
                "The cleaner bot must walk to and remove cleanable blood decals.");
            Assert.That(decals.ContainsDecal(map.Grid.Owner, footprint), Is.False,
                "The cleaner bot must also remove bloody boot footprint decals.");
        });
    }

    private void InsertTricordBeaker(EntityUid bot, EntityUid user, EntityCoordinates coordinates)
    {
        var beaker = SEntMan.SpawnEntity("Beaker", coordinates);
        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(beaker, "beaker", out var beakerSolution, out _), Is.True);
        solutions.TryAddReagent(beakerSolution!.Value, "CMTricordrazine", FixedPoint2.New(60), out _);

        Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(bot, "cmu-medibot-beaker", beaker, user), Is.True,
            "Any dispenser-compatible beaker must fit into the medical bot.");
    }
}
