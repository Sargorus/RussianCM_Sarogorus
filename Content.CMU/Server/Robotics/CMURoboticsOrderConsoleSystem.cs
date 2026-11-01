using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Requisitions;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Generates robotics work orders, delivers damaged android batches to cargo,
/// and validates/pays repair and build contracts when the cargo platform is lowered.
/// </summary>
public sealed partial class CMURoboticsOrderConsoleSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RequisitionsSystem _requisitions = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMURoboticsOrderConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMURoboticsOrderConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<RequisitionsElevatorComponent, CMURequisitionsElevatorLoweringAttemptEvent>(
            OnElevatorLoweringAttempt);

        Subs.BuiEvents<CMURoboticsOrderConsoleComponent>(CMURoboticsOrderConsoleUiKey.Key, subs =>
        {
            subs.Event<CMURoboticsAcceptOrderMessage>(OnAcceptOrder);
            subs.Event<CMURoboticsCancelOrderMessage>(OnCancelOrder);
            subs.Event<CMURoboticsRefreshOrdersMessage>(OnRefreshOrders);
        });
    }

    private void OnMapInit(Entity<CMURoboticsOrderConsoleComponent> ent, ref MapInitEvent args)
    {
        FillOrders(ent);
    }

    private void OnUiOpened(Entity<CMURoboticsOrderConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        FillOrders(ent);
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

        var elevator = FindCorporateElevator(ent.Owner);
        if (elevator == null)
        {
            ent.Comp.Status = Loc.GetString("cmu-robotics-console-no-platform");
            UpdateUi(ent);
            return;
        }

        order.Elevator = elevator;

        if (order.Type == CMURoboticsOrderType.Repair && !TrySpawnRepairCrate(order))
        {
            ent.Comp.Status = Loc.GetString("cmu-robotics-console-repair-spawn-failed");
            UpdateUi(ent);
            return;
        }

        ent.Comp.AvailableOrders.Remove(order);
        ent.Comp.ActiveOrder = order;
        ent.Comp.Status = Loc.GetString(
            order.Type == CMURoboticsOrderType.Repair
                ? "cmu-robotics-console-accepted-repair"
                : "cmu-robotics-console-accepted-build",
            ("target", order.TargetName),
            ("amount", order.Amount),
            ("reward", order.Reward));
        UpdateUi(ent);
    }

    private void OnElevatorLoweringAttempt(
        Entity<RequisitionsElevatorComponent> ent,
        ref CMURequisitionsElevatorLoweringAttemptEvent args)
    {
        var completions = new List<(Entity<CMURoboticsOrderConsoleComponent> Console, CMURoboticsOrder Order)>();

        var query = EntityQueryEnumerator<CMURoboticsOrderConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (console.ActiveOrder is not { } order || order.Elevator != ent.Owner)
                continue;

            if (!ValidateOnPlatform(ent.Owner, order, out var reason))
            {
                console.Status = Loc.GetString(
                    "cmu-robotics-console-rejected",
                    ("reason", reason));
                UpdateUi((consoleUid, console));
                args.Cancelled = true;
                return;
            }

            completions.Add(((consoleUid, console), order));
        }

        foreach (var (console, order) in completions)
        {
            PayCorporate(order.Reward);
            console.Comp.ActiveOrder = null;
            console.Comp.Status = Loc.GetString(
                "cmu-robotics-console-delivered",
                ("target", order.TargetName),
                ("reward", order.Reward));
            FillOrders(console);
            UpdateUi(console);
        }
    }

    private bool ValidateOnPlatform(EntityUid elevator, CMURoboticsOrder order, out string reason)
    {
        var entities = _lookup.GetEntitiesIntersecting(elevator);

        if (order.Type == CMURoboticsOrderType.Repair)
        {
            var targets = entities
                .Where(entity =>
                    TryComp<CMURoboticsOrderTargetComponent>(entity, out var marker) &&
                    marker.OrderId == order.Id)
                .ToList();

            if (targets.Count < order.Amount)
            {
                reason = Loc.GetString(
                    "cmu-robotics-console-reason-missing-units",
                    ("amount", order.Amount));
                return false;
            }

            foreach (var target in targets.Take(order.Amount))
            {
                if (!ValidateDelivery(target, order, out reason))
                    return false;
            }

            reason = string.Empty;
            return true;
        }

        var built = entities.FirstOrDefault(entity =>
            HasComp<CMUDroneAndroidComponent>(entity) &&
            MetaData(entity).EntityPrototype?.ID == order.Target.Id);
        if (built == default)
        {
            reason = Loc.GetString("cmu-robotics-console-reason-wrong");
            return false;
        }

        return ValidateDelivery(built, order, out reason);
    }

    private void OnCancelOrder(Entity<CMURoboticsOrderConsoleComponent> ent, ref CMURoboticsCancelOrderMessage args)
    {
        if (ent.Comp.ActiveOrder is not { } order)
            return;

        CancelOrderTargets(order);

        ent.Comp.ActiveOrder = null;
        ent.Comp.Status = Loc.GetString("cmu-robotics-console-cancelled");
        FillOrders(ent);
        UpdateUi(ent);
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

            var order = new CMURoboticsOrder
            {
                Id = ent.Comp.NextOrderId++,
                Type = build ? CMURoboticsOrderType.Build : CMURoboticsOrderType.Repair,
                Target = target.Prototype,
                RequiredModule = requiredModule,
                Reward = target.Reward + (requiredModule != null ? 150 : 0) + _random.Next(0, 101),
                Amount = build ? 1 : 3,
                TargetName = GetPrototypeName(target.Prototype),
            };

            ent.Comp.AvailableOrders.Add(order);
        }
    }

    private bool TrySpawnRepairCrate(CMURoboticsOrder order)
    {
        var prototype = order.Target.Id switch
        {
            "CMUDroneAndroidCombat" => "CMURoboticsRepairCrateCombat",
            "CMUWorkingJoeRepair" => "CMURoboticsRepairCrateWorkingJoe",
            _ => "CMURoboticsRepairCrateUnarmed",
        };

        if (!_prototypes.HasIndex<EntityPrototype>(prototype))
            return false;

        var crate = Spawn(prototype, MapCoordinates.Nullspace);
        var crateComp = EnsureComp<CMURoboticsRepairCrateComponent>(crate);
        crateComp.OrderId = order.Id;
        order.RepairCrate = crate;
        return true;
    }

    private EntityUid? FindCorporateElevator(EntityUid console)
    {
        var map = _transform.GetMapId(console);
        var origin = _transform.GetWorldPosition(console);
        EntityUid? closest = null;
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

            closest = uid;
            closestDistance = distance;
        }

        return closest;
    }

    private void CancelOrderTargets(CMURoboticsOrder order)
    {
        if (order.RepairCrate is { } crate && !TerminatingOrDeleted(crate))
            QueueDel(crate);

        var query = EntityQueryEnumerator<CMURoboticsOrderTargetComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (marker.OrderId == order.Id && !TerminatingOrDeleted(uid))
                QueueDel(uid);
        }
    }

    private bool ValidateDelivery(EntityUid drone, CMURoboticsOrder order, out string reason)
    {
        if (!_mobState.IsAlive(drone))
        {
            reason = Loc.GetString("cmu-robotics-console-reason-broken");
            return false;
        }

        if (order.Type == CMURoboticsOrderType.Repair)
        {
            if (!HasComp<DamageableComponent>(drone) ||
                _damageable.GetTotalDamage(drone) > FixedPoint2.New(5))
            {
                reason = Loc.GetString("cmu-robotics-console-reason-not-repaired");
                return false;
            }

            if (!HasAllLimbs(drone))
            {
                reason = Loc.GetString("cmu-robotics-console-reason-limbs");
                return false;
            }
        }

        if (order.RequiredModule is { } required)
        {
            if (!TryComp<CMUDroneAndroidComponent>(drone, out var android) ||
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

    private bool HasAllLimbs(EntityUid drone)
    {
        if (!TryComp<BodyComponent>(drone, out var body))
            return false;

        var arms = _body.GetBodyChildrenOfType(drone, BodyPartType.Arm, body).Count();
        var legs = _body.GetBodyChildrenOfType(drone, BodyPartType.Leg, body).Count();
        return arms >= 2 && legs >= 2;
    }

    private void PayCorporate(int amount)
    {
        _requisitions.AddCorporateFunds(amount);
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
        return new CMURoboticsOrderInfo(
            order.Id,
            order.Type,
            order.TargetName,
            order.RequiredModule is { } module ? GetPrototypeName(module) : null,
            order.Reward,
            order.Amount);
    }

    private string GetPrototypeName(EntProtoId prototype)
    {
        if (Loc.TryGetString($"ent-{prototype.Id}", out var entityName))
            return entityName;

        var name = _prototypes.Index<EntityPrototype>(prototype).Name;
        return Loc.TryGetString(name, out var localized) ? localized : name;
    }
}
