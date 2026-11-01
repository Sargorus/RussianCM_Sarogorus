using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.Requisitions;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Requisitions;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Storage.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Generates robotics work orders, queues synthetic transport crates in cargo,
/// and evaluates repair and build contracts when the corporate platform lowers.
/// </summary>
public sealed partial class CMURoboticsOrderConsoleSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RequisitionsSystem _requisitions = default!;
    [Dependency] private CorporateConsoleSystem _corporateConsole = default!;
    [Dependency] private CMURoboticsDamagedBodySystem _damagedBody = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private LabelSystem _labels = default!;

    private const int RepairRewardPerDamage = 1;
    private const int RepairRewardPerMissingLimb = 20;
    private const float RepairFailureFineMultiplier = 0.25f;

    private static readonly EntProtoId TransportCrate = "CMUCrateSyntheticTransport";

    private static readonly BodyPartType[] ExternalLimbTypes =
    {
        BodyPartType.Arm,
        BodyPartType.Hand,
        BodyPartType.Leg,
        BodyPartType.Foot,
    };

    private static int _nextOrderId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMURoboticsOrderConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMURoboticsOrderConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<RequisitionsElevatorComponent, CMURequisitionsElevatorLoweringAttemptEvent>(
            OnElevatorLoweringAttempt);
        SubscribeLocalEvent<CMURequisitionsCrateDeliveredEvent>(OnCrateDelivered);

        Subs.BuiEvents<CMURoboticsOrderConsoleComponent>(CMURoboticsOrderConsoleUiKey.Key, subs =>
        {
            subs.Event<CMURoboticsAcceptOrderMessage>(OnAcceptOrder);
            subs.Event<CMURoboticsCancelOrderMessage>(OnCancelOrder);
            subs.Event<CMURoboticsPrintSlipMessage>(OnPrintSlip);
            subs.Event<CMURoboticsRefreshOrdersMessage>(OnRefreshOrders);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMURoboticsOrderConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.ActiveOrder is not { } order)
                continue;

            if (order.Deadline != default && now >= order.Deadline)
            {
                FailOrder((uid, console), order, fine: true, reason: null);
                continue;
            }

            if (now < console.NextUiUpdate ||
                !_ui.IsUiOpen(uid, CMURoboticsOrderConsoleUiKey.Key))
            {
                continue;
            }

            console.NextUiUpdate = now + TimeSpan.FromSeconds(1);
            UpdateUi((uid, console));
        }
    }

    private void OnMapInit(Entity<CMURoboticsOrderConsoleComponent> ent, ref MapInitEvent args)
    {
        FillOrders(ent);
    }

    private void OnUiOpened(Entity<CMURoboticsOrderConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        FillOrders(ent);
        ent.Comp.NextUiUpdate = _timing.CurTime;
        UpdateUi(ent);
    }

    private void OnAcceptOrder(Entity<CMURoboticsOrderConsoleComponent> ent, ref CMURoboticsAcceptOrderMessage args)
    {
        if (ent.Comp.ActiveOrder != null)
        {
            ent.Comp.Status = Loc.GetString("cmu-robotics-console-active-exists");
            UpdateUi(ent);
            return;
        }

        var requestedId = args.Id;
        var order = ent.Comp.AvailableOrders.FirstOrDefault(o => o.Id == requestedId);
        if (order == null)
            return;

        if (FindCorporateElevator(ent.Owner) is not { } elevator)
        {
            ent.Comp.Status = Loc.GetString("cmu-robotics-console-no-platform");
            UpdateUi(ent);
            return;
        }

        var entries = BuildOrderEntries(order);
        var freeSlots = _requisitions.GetElevatorRemainingOrderSlots(elevator);
        if (!_requisitions.TryAddElevatorOrders(elevator, entries))
        {
            ent.Comp.Status = Loc.GetString(
                "cmu-robotics-console-no-queue-space",
                ("needed", entries.Count),
                ("free", freeSlots));
            UpdateUi(ent);
            return;
        }

        order.Elevator = elevator.Owner;
        order.FirstOrderIndex = elevator.Comp.Orders.Count - entries.Count;
        order.CrateCount = entries.Count;
        order.Deadline = _timing.CurTime + ent.Comp.OrderDuration;

        ent.Comp.AvailableOrders.Remove(order);
        ent.Comp.ActiveOrder = order;
        ent.Comp.Status = Loc.GetString(
            order.Type == CMURoboticsOrderType.Repair
                ? "cmu-robotics-console-accepted-repair"
                : "cmu-robotics-console-accepted-build",
            ("target", order.TargetName),
            ("amount", order.Amount),
            ("crates", entries.Count),
            ("reward", order.Reward));
        ent.Comp.NextUiUpdate = _timing.CurTime;
        PrintSlip(ent, order, args.Actor);
        UpdateUi(ent);
    }

    private List<RequisitionsEntry> BuildOrderEntries(CMURoboticsOrder order)
    {
        var count = order.Type == CMURoboticsOrderType.Repair
            ? Math.Max(1, order.Amount)
            : 1;

        var entries = new List<RequisitionsEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = new RequisitionsEntry
            {
                Crate = TransportCrate,
            };

            if (order.Type == CMURoboticsOrderType.Repair)
                entry.Entities.Add(GetRepairPrototype(order.Target));

            entries.Add(entry);
        }

        return entries;
    }

    private static EntProtoId GetRepairPrototype(EntProtoId target)
    {
        return target.Id switch
        {
            "CMUDroneAndroidCombat" => "CMUDroneAndroidCombatRepairOrder",
            "CMUDroneAndroidUnarmed" => "CMUDroneAndroidUnarmedRepairOrder",
            "CMUWorkingJoeRepair" => "CMUWorkingJoeRepairOrder",
            _ => "CMUDroneAndroidUnarmedRepairOrder",
        };
    }

    private void OnCrateDelivered(ref CMURequisitionsCrateDeliveredEvent args)
    {
        var query = EntityQueryEnumerator<CMURoboticsOrderConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (console.ActiveOrder is not { } order ||
                order.Elevator != args.Elevator.Owner ||
                args.Index < order.FirstOrderIndex ||
                args.Index >= order.FirstOrderIndex + order.CrateCount)
            {
                continue;
            }

            MarkDeliveredCrate((consoleUid, console), order, args.Crate);
        }
    }

    private void MarkDeliveredCrate(
        Entity<CMURoboticsOrderConsoleComponent> console,
        CMURoboticsOrder order,
        EntityUid crate)
    {
        if (!HasComp<CMURoboticsTransportCrateComponent>(crate))
            return;

        var crateComp = EnsureComp<CMURoboticsTransportCrateComponent>(crate);
        crateComp.OrderId = order.Id;

        if (!TryComp<EntityStorageComponent>(crate, out var storage))
            return;

        var containedEntities = new List<EntityUid>(storage.Contents.ContainedEntities);
        foreach (var contained in containedEntities)
        {
            if (!HasComp<CMURoboticsDamagedBodyComponent>(contained))
                continue;

            var marker = EnsureComp<CMURoboticsOrderTargetComponent>(contained);
            marker.OrderId = order.Id;

            if (order.Type != CMURoboticsOrderType.Repair)
                continue;

            _damagedBody.ApplyRepairProfile(
                contained,
                order.RepairMissingLimbs,
                order.RepairBurnDamage,
                order.RepairBruteDamage);

            marker.InitialDamage = FixedPoint2.New(order.RepairBurnDamage + order.RepairBruteDamage);
            marker.InitialMissingLimbs = order.RepairMissingLimbs;
        }
    }

    private string BuildSlipContent(CMURoboticsOrder order, int minutes)
    {
        var type = Loc.GetString(order.Type == CMURoboticsOrderType.Build
            ? "cmu-robotics-console-type-build"
            : "cmu-robotics-console-type-repair");
        var module = order.RequiredModule is { } required
            ? GetPrototypeName(required)
            : Loc.GetString("cmu-robotics-slip-module-none");
        var package = Loc.GetString(order.Type == CMURoboticsOrderType.Build
            ? "cmu-robotics-slip-package-build"
            : "cmu-robotics-slip-package-repair");

        return Loc.GetString(
            "cmu-robotics-slip-content",
            ("id", order.Id),
            ("type", type),
            ("target", order.TargetName),
            ("module", module),
            ("amount", order.Amount),
            ("package", package),
            ("reward", order.Reward),
            ("minutes", minutes))
            // Fluent keeps the two characters "\n" in this single-line value;
            // the paper renderer expects real line breaks.
            .Replace("\\n", "\n");
    }

    private void OnElevatorLoweringAttempt(
        Entity<RequisitionsElevatorComponent> ent,
        ref CMURequisitionsElevatorLoweringAttemptEvent args)
    {
        var query = EntityQueryEnumerator<CMURoboticsOrderConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (console.ActiveOrder is not { } order || order.Elevator != ent.Owner)
                continue;

            var success = TryEvaluateSubmission(ent.Owner, order, out var attempted, out var reason);
            if (!attempted)
                continue;

            var consoleEnt = (consoleUid, console);
            if (!success)
            {
                FailOrder(consoleEnt, order, fine: false, reason: reason);
                continue;
            }

            PayLiaison(order.Reward);
            DeleteOrderSlips(order.Id);
            console.ActiveOrder = null;
            console.Status = Loc.GetString(
                "cmu-robotics-console-delivered",
                ("target", order.TargetName),
                ("reward", order.Reward));
            FillOrders(consoleEnt);
            UpdateUi(consoleEnt);
        }
    }

    private bool TryEvaluateSubmission(
        EntityUid elevator,
        CMURoboticsOrder order,
        out bool attempted,
        out string reason)
    {
        attempted = false;
        reason = string.Empty;

        var crates = GetOrderCrates(elevator, order);
        var looseSlip = HasLooseOrderSlip(elevator, order.Id);
        var hasAnyLabel = looseSlip || crates.Any(crate => crate.HasOrderLabel);
        var labeledWithUnit = crates.Any(crate => crate.HasOrderLabel && crate.Units.Count > 0);
        var unitCount = crates.Sum(crate => crate.Units.Count);

        attempted = hasAnyLabel || unitCount > 0;
        if (!attempted)
            return false;

        if (!hasAnyLabel)
        {
            reason = Loc.GetString("cmu-robotics-console-reason-no-slip");
            return false;
        }

        if (!labeledWithUnit)
        {
            reason = Loc.GetString("cmu-robotics-console-reason-slip-not-on-crate");
            return false;
        }

        if (unitCount < order.Amount)
        {
            reason = Loc.GetString(
                "cmu-robotics-console-reason-missing-units",
                ("amount", order.Amount));
            return false;
        }

        if (order.Type == CMURoboticsOrderType.Build &&
            crates.Any(crate => crate.Units.Count > 0 && !crate.HasControlTablet))
        {
            reason = Loc.GetString("cmu-robotics-console-reason-tablet");
            return false;
        }

        foreach (var crate in crates)
        {
            foreach (var unit in crate.Units)
            {
                if (!ValidateDelivery(unit, order, out reason))
                    return false;
            }
        }

        return true;
    }

    private List<OrderCrateInfo> GetOrderCrates(EntityUid elevator, CMURoboticsOrder order)
    {
        var result = new List<OrderCrateInfo>();
        foreach (var entity in _lookup.GetEntitiesIntersecting(elevator))
        {
            if (!HasComp<CMURoboticsTransportCrateComponent>(entity))
                continue;

            var hasLabel = _labels.TryGetLabel<CMURoboticsOrderSlipComponent>(entity, out var label) &&
                           label.Value.Comp.OrderId == order.Id;

            var units = new List<EntityUid>();
            var hasControlTablet = false;
            if (TryComp<EntityStorageComponent>(entity, out var storage))
            {
                foreach (var contained in storage.Contents.ContainedEntities)
                {
                    if (HasComp<CMUDroneControlTabletComponent>(contained))
                        hasControlTablet = true;

                    if (IsOrderUnit(contained, order))
                        units.Add(contained);
                }
            }

            result.Add(new OrderCrateInfo(entity, units, hasLabel, hasControlTablet));
        }

        return result;
    }

    private bool IsOrderUnit(EntityUid entity, CMURoboticsOrder order)
    {
        if (order.Type == CMURoboticsOrderType.Repair)
        {
            return TryComp<CMURoboticsOrderTargetComponent>(entity, out var marker) &&
                   marker.OrderId == order.Id;
        }

        return HasComp<CMUDroneAndroidComponent>(entity) &&
               MetaData(entity).EntityPrototype?.ID == order.Target.Id;
    }

    private bool HasLooseOrderSlip(EntityUid elevator, int orderId)
    {
        foreach (var entity in _lookup.GetEntitiesIntersecting(elevator))
        {
            if (TryComp<CMURoboticsOrderSlipComponent>(entity, out var slip) &&
                slip.OrderId == orderId)
            {
                return true;
            }
        }

        return false;
    }

    private void OnCancelOrder(Entity<CMURoboticsOrderConsoleComponent> ent, ref CMURoboticsCancelOrderMessage args)
    {
        if (ent.Comp.ActiveOrder is not { } order)
            return;

        // Cancelling a contract must not delete customer units or transport
        // crates. Only the paperwork is removed.
        DeleteOrderSlips(order.Id);

        ent.Comp.ActiveOrder = null;
        ent.Comp.Status = Loc.GetString("cmu-robotics-console-cancelled");
        FillOrders(ent);
        UpdateUi(ent);
    }

    private void OnPrintSlip(Entity<CMURoboticsOrderConsoleComponent> ent, ref CMURoboticsPrintSlipMessage args)
    {
        if (ent.Comp.ActiveOrder is not { } order)
            return;

        PrintSlip(ent, order, args.Actor);
        ent.Comp.Status = Loc.GetString("cmu-robotics-console-slip-printed");
        UpdateUi(ent);
    }

    private void PrintSlip(Entity<CMURoboticsOrderConsoleComponent> ent, CMURoboticsOrder order, EntityUid? actor)
    {
        var slip = Spawn(ent.Comp.SlipPrototype, Transform(ent.Owner).Coordinates);
        var slipComp = EnsureComp<CMURoboticsOrderSlipComponent>(slip);
        slipComp.OrderId = order.Id;
        slipComp.TargetName = order.TargetName;
        slipComp.Amount = order.Amount;
        slipComp.Reward = order.Reward;
        slipComp.Content = BuildSlipContent(order, (int) ent.Comp.OrderDuration.TotalMinutes);

        if (TryComp<PaperComponent>(slip, out var paper))
        {
            _meta.SetEntityName(slip, Loc.GetString("cmu-robotics-slip-name", ("id", order.Id)));
            _paper.SetContent((slip, paper), slipComp.Content);
        }

        if (actor is { } user && !TerminatingOrDeleted(user))
            _hands.TryPickupAnyHand(user, slip);
    }

    private void OnRefreshOrders(Entity<CMURoboticsOrderConsoleComponent> ent, ref CMURoboticsRefreshOrdersMessage args)
    {
        if (ent.Comp.ActiveOrder != null)
        {
            ent.Comp.Status = Loc.GetString("cmu-robotics-console-refresh-active");
            UpdateUi(ent);
            return;
        }

        ent.Comp.AvailableOrders.Clear();
        FillOrders(ent);
        ent.Comp.Status = Loc.GetString("cmu-robotics-console-refreshed");
        UpdateUi(ent);
    }

    private void FillOrders(Entity<CMURoboticsOrderConsoleComponent> ent)
    {
        while (ent.Comp.AvailableOrders.Count < ent.Comp.MaxAvailableOrders)
        {
            var build = ent.Comp.AvailableOrders.Count == 0 || _random.Prob(0.5f);
            var pool = build ? ent.Comp.BuildTargets : ent.Comp.RepairTargets;

            if (pool.Count == 0)
            {
                build = true;
                pool = ent.Comp.BuildTargets;
            }

            if (pool.Count == 0)
                break;

            var target = _random.Pick(pool);
            EntProtoId? requiredModule = null;
            if (target.RequiredModules.Count > 0 && (!build || _random.Prob(0.5f)))
                requiredModule = _random.Pick(target.RequiredModules);

            var amount = build ? 1 : 3;
            var reward = target.Reward + (requiredModule != null ? 150 : 0) + _random.Next(0, 101);
            var repairBurn = 0;
            var repairBrute = 0;
            var repairMissing = 0;

            if (!build &&
                _prototypes.TryIndex<EntityPrototype>(GetRepairPrototype(target.Prototype), out var repairPrototype) &&
                repairPrototype.TryComp<CMURoboticsDamagedBodyComponent>(out var damaged, _compFactory))
            {
                repairMissing = RollRange(damaged.MinSeveredLimbs, damaged.MaxSeveredLimbs);
                repairBurn = RollRange(damaged.MinBurnDamage, damaged.MaxBurnDamage);
                repairBrute = RollRange(damaged.MinBruteDamage, damaged.MaxBruteDamage);

                var unitBonus = (int) Math.Ceiling((repairBurn + repairBrute) * (double) RepairRewardPerDamage) +
                                repairMissing * RepairRewardPerMissingLimb;
                reward += unitBonus * amount;
            }

            var order = new CMURoboticsOrder
            {
                Id = _nextOrderId++,
                Type = build ? CMURoboticsOrderType.Build : CMURoboticsOrderType.Repair,
                Target = target.Prototype,
                RequiredModule = requiredModule,
                Reward = reward,
                Amount = amount,
                TargetName = string.IsNullOrEmpty(target.Name)
                    ? GetPrototypeName(target.Prototype)
                    : Loc.GetString(target.Name),
                RepairBurnDamage = repairBurn,
                RepairBruteDamage = repairBrute,
                RepairMissingLimbs = repairMissing,
            };

            ent.Comp.AvailableOrders.Add(order);
        }
    }

    private int RollRange(int min, int max)
    {
        var low = Math.Min(min, max);
        var high = Math.Max(min, max);
        return _random.Next(low, high + 1);
    }

    private Entity<RequisitionsElevatorComponent>? FindCorporateElevator(EntityUid console)
    {
        var map = _transform.GetMapId(console);
        var origin = _transform.GetWorldPosition(console);
        Entity<RequisitionsElevatorComponent>? closest = null;
        var closestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<RequisitionsElevatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var elevator, out var xform))
        {
            if (xform.MapID != map ||
                !string.Equals(elevator.Faction, "corporate", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var distance = (origin - _transform.GetWorldPosition(uid)).LengthSquared();
            if (distance >= closestDistance)
                continue;

            closest = (uid, elevator);
            closestDistance = distance;
        }

        return closest;
    }

    private void FailOrder(
        Entity<CMURoboticsOrderConsoleComponent> console,
        CMURoboticsOrder order,
        bool fine,
        string? reason)
    {
        var fineAmount = 0;
        if (fine)
        {
            fineAmount = (int) Math.Ceiling(order.Reward * RepairFailureFineMultiplier);
            _corporateConsole.AddToCorporateBudget(-fineAmount);
        }

        // A failed or expired contract does not delete its customer units.
        DeleteOrderSlips(order.Id);
        console.Comp.ActiveOrder = null;
        console.Comp.Status = reason == null
            ? Loc.GetString("cmu-robotics-console-failed-timeout", ("fine", fineAmount))
            : Loc.GetString("cmu-robotics-console-rejected", ("reason", reason));
        FillOrders(console);
        UpdateUi(console);
    }

    private void DeleteOrderSlips(int orderId)
    {
        var query = EntityQueryEnumerator<CMURoboticsOrderSlipComponent>();
        while (query.MoveNext(out var uid, out var slip))
        {
            if (slip.OrderId == orderId && !TerminatingOrDeleted(uid))
                QueueDel(uid);
        }
    }

    private bool ValidateDelivery(EntityUid unit, CMURoboticsOrder order, out string reason)
    {
        if (!_mobState.IsAlive(unit))
        {
            reason = Loc.GetString("cmu-robotics-console-reason-broken");
            return false;
        }

        if (!HasComp<DamageableComponent>(unit) ||
            _damageable.GetTotalDamage(unit) > FixedPoint2.New(5))
        {
            reason = Loc.GetString(
                order.Type == CMURoboticsOrderType.Repair
                    ? "cmu-robotics-console-reason-not-repaired"
                    : "cmu-robotics-console-reason-broken");
            return false;
        }

        if (!HasAllLimbs(unit, order, out reason))
            return false;

        if (order.RequiredModule is { } required)
        {
            if (!TryComp<CMUDroneAndroidComponent>(unit, out var android) ||
                android.InstalledModule is not { } module ||
                MetaData(module).EntityPrototype?.ID != required.Id)
            {
                reason = Loc.GetString(
                    "cmu-robotics-console-reason-module",
                    ("module", GetPrototypeName(required)));
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private bool HasAllLimbs(EntityUid unit, CMURoboticsOrder order, out string reason)
    {
        if (!TryComp<BodyComponent>(unit, out var body))
        {
            reason = Loc.GetString("cmu-robotics-console-reason-limbs");
            return false;
        }

        var workingJoe = order.Target.Id.StartsWith("CMUWorkingJoe", StringComparison.Ordinal);
        foreach (var type in ExternalLimbTypes)
        {
            var count = 0;
            foreach (var part in _body.GetBodyChildrenOfType(unit, type, body))
            {
                count++;
                var valid = workingJoe
                    ? CMULimbCompatibility.IsWorkingJoeLimb(EntityManager, part.Id)
                    : CMULimbCompatibility.IsDroneLimb(EntityManager, part.Id);
                if (!valid)
                {
                    reason = Loc.GetString("cmu-robotics-console-reason-wrong-limb");
                    return false;
                }
            }

            if (count < 2)
            {
                reason = Loc.GetString("cmu-robotics-console-reason-limbs");
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private void PayLiaison(int amount)
    {
        _corporateConsole.AddToCorporateBudget(amount);
    }

    private void UpdateUi(Entity<CMURoboticsOrderConsoleComponent> ent)
    {
        var available = ent.Comp.AvailableOrders
            .Select(ToInfo)
            .ToList();

        var active = ent.Comp.ActiveOrder == null ? null : ToInfo(ent.Comp.ActiveOrder);

        _ui.SetUiState(
            ent.Owner,
            CMURoboticsOrderConsoleUiKey.Key,
            new CMURoboticsOrderConsoleState(available, active, ent.Comp.Status));
    }

    private CMURoboticsOrderInfo ToInfo(CMURoboticsOrder order)
    {
        var seconds = order.Deadline == default
            ? 0
            : Math.Max(0, (int) Math.Ceiling((order.Deadline - _timing.CurTime).TotalSeconds));

        return new CMURoboticsOrderInfo(
            order.Id,
            order.Type,
            order.TargetName,
            order.RequiredModule is { } module ? GetPrototypeName(module) : null,
            order.Reward,
            order.Amount,
            seconds);
    }

    private string GetPrototypeName(EntProtoId prototype)
    {
        if (Loc.TryGetString($"ent-{prototype.Id}", out var entityName))
            return entityName;

        var name = _prototypes.Index<EntityPrototype>(prototype).Name;
        return Loc.TryGetString(name, out var localized) ? localized : name;
    }

    private readonly record struct OrderCrateInfo(
        EntityUid Crate,
        List<EntityUid> Units,
        bool HasOrderLabel,
        bool HasControlTablet);
}
