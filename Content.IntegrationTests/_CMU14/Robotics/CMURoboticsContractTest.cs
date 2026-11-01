using System;
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Robotics;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Requisitions;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Lock;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Robotics;

[TestFixture]
public sealed class CMURoboticsContractTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task DroneOnlyAcceptsDroneAndroidLimbs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var drone = SEntMan.SpawnEntity("CMUDroneAndroidUnarmed", map.GridCoords);
            var droneArm = SEntMan.SpawnEntity("CMUPartDroneAndroidLeftArm", map.GridCoords);
            var joeArm = SEntMan.SpawnEntity("CMUPartWorkingJoeLeftArm", map.GridCoords);
            var synthArm = SEntMan.SpawnEntity("CMUPartSynthLeftArm", map.GridCoords);
            var prostheticArm = SEntMan.SpawnEntity("CMUPartRoboticLeftArm", map.GridCoords);
            var joe = SEntMan.SpawnEntity("CMUWorkingJoeRepair", map.GridCoords);

            Assert.That(CMULimbCompatibility.CanAttach(SEntMan, drone, droneArm), Is.True);
            Assert.That(CMULimbCompatibility.CanAttach(SEntMan, drone, joeArm), Is.False);
            Assert.That(CMULimbCompatibility.CanAttach(SEntMan, drone, synthArm), Is.False);
            Assert.That(CMULimbCompatibility.CanAttach(SEntMan, drone, prostheticArm), Is.False);

            Assert.That(CMULimbCompatibility.CanAttach(SEntMan, joe, joeArm), Is.True);
            Assert.That(CMULimbCompatibility.CanAttach(SEntMan, joe, synthArm), Is.True);
            Assert.That(CMULimbCompatibility.CanAttach(SEntMan, joe, droneArm), Is.False);

            Assert.That(CMULimbCompatibility.IsWorkingJoeLimb(SEntMan, joeArm), Is.True);
            Assert.That(CMULimbCompatibility.IsWorkingJoeLimb(SEntMan, droneArm), Is.False);
        });
    }

    [Test]
    public async Task DroneAssemblyPartsAreSurgicalLimbsAndOrderMetadataIsCurrent()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var drone = SEntMan.SpawnEntity("CMUDroneAndroidUnarmed", map.GridCoords);
            var assemblyArm = SEntMan.SpawnEntity("CMUDroneLeftArmAssembly", map.GridCoords);
            var console = SEntMan.SpawnEntity("CMURoboticsOrderConsole", map.GridCoords);
            var slip = SEntMan.SpawnEntity("CMURoboticsOrderSlip", map.GridCoords);

            Assert.That(SEntMan.HasComponent<CMUDroneAssemblyPartComponent>(assemblyArm), Is.True,
                "the printable drone arm must still fit a drone frame");
            Assert.That(SEntMan.HasComponent<BodyPartComponent>(assemblyArm), Is.True,
                "the printable drone arm must also be a surgical body part");
            Assert.That(CMULimbCompatibility.IsDroneLimb(SEntMan, assemblyArm), Is.True);
            Assert.That(CMULimbCompatibility.CanAttach(SEntMan, drone, assemblyArm), Is.True);

            Assert.That(
                SEntMan.GetComponent<CMURoboticsOrderConsoleComponent>(console).OrderDuration,
                Is.EqualTo(TimeSpan.FromMinutes(15)));
            Assert.That(SEntMan.HasComponent<PaperComponent>(slip), Is.True,
                "the order slip must use a CMU/RMC paper prototype");
        });
    }

    [Test]
    public async Task TransportCrateHoldsOneSynthetic()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var crate = SEntMan.SpawnEntity("CMUCrateSyntheticTransport", map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMURoboticsTransportCrateComponent>(crate), Is.True);

            var storage = SEntMan.GetComponent<EntityStorageComponent>(crate);
            Assert.That(storage.Capacity, Is.EqualTo(1));
            Assert.That(storage.MaxSize, Is.GreaterThanOrEqualTo(1f));

            var system = Server.System<CMURoboticsTransportCrateSystem>();
            var drone = SEntMan.SpawnEntity("CMUDroneAndroidUnarmed", map.GridCoords);
            Assert.That(SEntMan.HasComponent<SynthComponent>(drone), Is.True, "drone must have Synth");
            Assert.That(storage.Open, Is.False, "transport crate must start closed");
            Assert.That(storage.Contents.ContainedEntities.Count, Is.EqualTo(0), "crate must start empty");
            Assert.That(SEntMan.GetComponent<LockComponent>(crate).Locked, Is.False, "crate must start unlocked");
            Assert.That(SEntMan.HasComponent<MobStateComponent>(drone), Is.True, "drone must have MobState");
            Assert.That(storage.Blacklist, Is.Null, "crate storage must not have a blacklist");
            var droneAabb = Server.System<EntityLookupSystem>().GetAABBNoContainer(drone, default, 0);
            Assert.That(droneAabb.Size.X, Is.LessThanOrEqualTo(storage.MaxSize),
                $"drone AABB width {droneAabb.Size.X} must fit max size {storage.MaxSize}");
            Assert.That(droneAabb.Size.Y, Is.LessThanOrEqualTo(storage.MaxSize),
                $"drone AABB height {droneAabb.Size.Y} must fit max size {storage.MaxSize}");
            var insertAttempt = new InsertIntoEntityStorageAttemptEvent(storage.Contents, drone);
            SEntMan.EventBus.RaiseLocalEvent(drone, ref insertAttempt);
            Assert.That(insertAttempt.Cancelled, Is.False, "InsertIntoEntityStorageAttemptEvent must not cancel");
            var crateAttempt = new EntityStorageInsertedIntoAttemptEvent(storage.Contents, drone);
            SEntMan.EventBus.RaiseLocalEvent(crate, ref crateAttempt);
            Assert.That(crateAttempt.Cancelled, Is.False, "EntityStorageInsertedIntoAttemptEvent must not cancel");
            Assert.That(
                Server.System<SharedEntityStorageSystem>().CanInsert(drone, crate, storage),
                Is.True,
                "EntityStorage must accept the drone");
            Assert.That(system.CanStoreSynthetic(crate, drone), Is.True, "crate system must accept the drone");
            Assert.That(
                system.TryStoreSynthetic(crate, EntityUid.Invalid, drone),
                Is.True,
                "a drone must be draggable into the transport crate");
            Assert.That(storage.Contents.Contains(drone), Is.True);

            var second = SEntMan.SpawnEntity("CMUDroneAndroidUnarmed", map.GridCoords);
            Assert.That(
                system.TryStoreSynthetic(crate, EntityUid.Invalid, second),
                Is.False,
                "the transport crate must not exceed its one-unit capacity");

            var slip = SEntMan.SpawnEntity("CMURoboticsOrderSlip", map.GridCoords);
            SEntMan.GetComponent<CMURoboticsOrderSlipComponent>(slip).OrderId = 42;

            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(
                slots.TryInsert(crate, "paper_label", slip, null),
                Is.True,
                "the transport crate label slot must accept the robotics order slip");

            var labels = Server.System<LabelSystem>();
            Assert.That(labels.TryGetLabel<CMURoboticsOrderSlipComponent>(crate, out var label), Is.True);
            Assert.That(label!.Value.Comp.OrderId, Is.EqualTo(42));
        });
    }

    [Test]
    public async Task RepairOrderQueuesThreeCratesWithMarkedDamagedUnits()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var console = SEntMan.SpawnEntity("CMURoboticsOrderConsole", map.GridCoords);
            var elevator = SEntMan.SpawnEntity(
                "AU14CorporateASRSElevator",
                map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));

            var consoleComp = SEntMan.GetComponent<CMURoboticsOrderConsoleComponent>(console);
            var elevatorComp = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);

            var order = new CMURoboticsOrder
            {
                Id = 987,
                Type = CMURoboticsOrderType.Repair,
                Target = "CMUDroneAndroidUnarmed",
                Amount = 3,
                TargetName = "test drone",
                Reward = 1000,
            };
            consoleComp.AvailableOrders.Clear();
            consoleComp.AvailableOrders.Add(order);

            var message = new CMURoboticsAcceptOrderMessage(order.Id)
            {
                Actor = EntityUid.Invalid,
                UiKey = CMURoboticsOrderConsoleUiKey.Key,
            };
            SEntMan.EventBus.RaiseLocalEvent(console, message);

            Assert.That(consoleComp.ActiveOrder, Is.Not.Null);
            Assert.That(elevatorComp.Orders.Count, Is.EqualTo(3));
            Assert.That(consoleComp.ActiveOrder!.FirstOrderIndex, Is.EqualTo(0));
            Assert.That(consoleComp.ActiveOrder.CrateCount, Is.EqualTo(3));

            var timing = Server.ResolveDependency<IGameTiming>();
            elevatorComp.Mode = RequisitionsElevatorMode.Raising;
            elevatorComp.NextMode = null;
            elevatorComp.Busy = true;
            elevatorComp.RaiseDelay = TimeSpan.Zero;
            elevatorComp.LowerDelay = TimeSpan.Zero;
            elevatorComp.ToggledAt = timing.CurTime - TimeSpan.FromSeconds(1);

            var requisitions = Server.System<RequisitionsSystem>();
            var steps = 0;
            while (elevatorComp.Mode != RequisitionsElevatorMode.Raised && steps++ < 100)
                requisitions.Update(0f);

            Assert.That(elevatorComp.Mode, Is.EqualTo(RequisitionsElevatorMode.Raised));

            var crates = 0;
            var query = SEntMan.EntityQueryEnumerator<CMURoboticsTransportCrateComponent, EntityStorageComponent>();
            while (query.MoveNext(out _, out _, out var storage))
            {
                crates++;
                Assert.That(storage.Contents.ContainedEntities.Count, Is.EqualTo(1),
                    "a transport crate must arrive with one synthetic inside");

                var contained = storage.Contents.ContainedEntities[0];
                Assert.That(SEntMan.HasComponent<CMURoboticsOrderTargetComponent>(contained), Is.True);
                Assert.That(
                    SEntMan.GetComponent<CMURoboticsOrderTargetComponent>(contained).OrderId,
                    Is.EqualTo(order.Id));
            }

            Assert.That(crates, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task RepairOrderPaysWhenPaperIsAttachedToLoadedCrate()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var elevator = SEntMan.SpawnEntity("AU14CorporateASRSElevator", map.GridCoords);
            var console = SEntMan.SpawnEntity("CMURoboticsOrderConsole", map.GridCoords);
            var crate = SEntMan.SpawnEntity("CMUCrateSyntheticTransport", map.GridCoords);
            var drone = SEntMan.SpawnEntity("CMUDroneAndroidUnarmed", map.GridCoords);

            var marker = SEntMan.AddComponent<CMURoboticsOrderTargetComponent>(drone);
            marker.OrderId = 555;

            var storage = Server.System<SharedEntityStorageSystem>();
            Assert.That(storage.Insert(drone, crate), Is.True);

            var slip = SEntMan.SpawnEntity("CMURoboticsOrderSlip", map.GridCoords);
            SEntMan.GetComponent<CMURoboticsOrderSlipComponent>(slip).OrderId = 555;
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryInsert(crate, "paper_label", slip, null), Is.True);

            var consoleComp = SEntMan.GetComponent<CMURoboticsOrderConsoleComponent>(console);
            consoleComp.ActiveOrder = new CMURoboticsOrder
            {
                Id = 555,
                Type = CMURoboticsOrderType.Repair,
                Target = "CMUDroneAndroidUnarmed",
                Amount = 1,
                TargetName = "test drone",
                Reward = 1000,
                Elevator = elevator,
            };

            var attempt = new CMURequisitionsElevatorLoweringAttemptEvent(elevator);
            SEntMan.EventBus.RaiseLocalEvent(elevator, ref attempt);

            Assert.That(consoleComp.ActiveOrder, Is.Null);
            Assert.That(SEntMan.EntityExists(drone), Is.True,
                "submitting a contract must not delete the repaired unit");

            var accounts = SEntMan.EntityQueryEnumerator<RequisitionsAccountComponent>();
            var paid = false;
            while (accounts.MoveNext(out _, out var account))
            {
                if (string.Equals(account.Faction, "corporate", StringComparison.OrdinalIgnoreCase) &&
                    account.Balance >= 1000)
                {
                    paid = true;
                }
            }

            Assert.That(paid, Is.True, "a valid submission must pay the corporate account");
        });
    }

    [Test]
    public async Task ElevatorQueueRefusesOverCapacityOrders()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var elevator = SEntMan.SpawnEntity("AU14CorporateASRSElevator", map.GridCoords);
            var component = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            var system = Server.System<RequisitionsSystem>();
            var entity = (elevator, component);

            var capacity = system.GetElevatorRemainingOrderSlots(entity);
            Assert.That(capacity, Is.GreaterThan(0));

            var entries = new List<RequisitionsEntry>();
            for (var i = 0; i < capacity; i++)
                entries.Add(new RequisitionsEntry { Crate = "CMUCrateSyntheticTransport" });

            Assert.That(system.TryAddElevatorOrders(entity, entries), Is.True);
            Assert.That(system.GetElevatorRemainingOrderSlots(entity), Is.EqualTo(0));

            var extra = new List<RequisitionsEntry>
            {
                new() { Crate = "CMUCrateSyntheticTransport" },
            };
            Assert.That(system.TryAddElevatorOrders(entity, extra), Is.False);
            Assert.That(component.Orders.Count, Is.EqualTo(capacity));
        });
    }
}
