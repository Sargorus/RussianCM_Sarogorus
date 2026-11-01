using System.Linq;
using System.Numerics;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Robotics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Robotics;

/// <summary>
/// Generates robotics work orders, spawns damaged units for repair jobs, and
/// validates and pays out delivered drones.
/// </summary>
public sealed partial class CMURoboticsOrderConsoleSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly Vector2[] SpawnOffsets =
    {
        new(0, -1),
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(1, -1),
        new(-1, -1),
        new(1, 1),
        new(-1, 1),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMURoboticsOrderConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMURoboticsOrderConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);

        Subs.BuiEvents<CMURoboticsOrderConsoleComponent>(CMURoboticsOrderConsoleUiKey.Key, subs =>
        {
            subs.Event<CMURoboticsAcceptOrderMessage>(OnAcceptOrder);
            subs.Event<CMURoboticsSubmitOrderMessage>(OnSubmitOrder);
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

        if (order.Type == CMURoboticsOrderType.Repair && !TrySpawnRepairUnit(ent, order))
        {
            ent.Comp.Status = Loc.GetString("cmu-robotics-console-repair-spawn-failed");
            UpdateUi(ent);
            return;
        }

        ent.Comp.AvailableOrders.Remove(order);
        ent.Comp.ActiveOrder = order;
        ent.Comp.Status = Loc.GetString(
            "cmu-robotics-console-accepted",
            ("target", order.TargetName),
            ("reward", order.Reward));
        UpdateUi(ent);
    }

    private void OnSubmitOrder(Entity<CMURoboticsOrderConsoleComponent> ent, ref CMURoboticsSubmitOrderMessage args)
    {
        if (ent.Comp.ActiveOrder is not { } order)
        {
            ent.Comp.Status = Loc.GetString("cmu-robotics-console-no-active");
            UpdateUi(ent);
            return;
        }

        var drone = FindDeliveryCandidate(ent, order);
        if (drone == null)
        {
            ent.Comp.Status = HasAnyDroneNearby(ent)
                ? Loc.GetString(
                    "cmu-robotics-console-rejected",
                    ("reason", Loc.GetString("cmu-robotics-console-reason-wrong")))
                : Loc.GetString(
                    order.Type == CMURoboticsOrderType.Repair
                        ? "cmu-robotics-console-no-repair-target"
                        : "cmu-robotics-console-no-build-target");
            UpdateUi(ent);
            return;
        }

        if (!ValidateDelivery(drone.Value, order, out var reason))
        {
            ent.Comp.Status = Loc.GetString(
                "cmu-robotics-console-rejected",
                ("reason", reason));
            UpdateUi(ent);
            return;
        }

        if (!_idCard.TryFindIdCard(args.Actor, out var idCard))
        {
            ent.Comp.Status = Loc.GetString("cmu-robotics-console-no-id");
            UpdateUi(ent);
            return;
        }

        idCard.Comp.AccountBalance += order.Reward;
        Dirty(idCard, idCard.Comp);

        QueueDel(drone.Value);
        ent.Comp.ActiveOrder = null;
        ent.Comp.Status = Loc.GetString(
            "cmu-robotics-console-delivered",
            ("target", order.TargetName),
            ("reward", order.Reward));
        FillOrders(ent);
        UpdateUi(ent);
    }

    private void OnCancelOrder(Entity<CMURoboticsOrderConsoleComponent> ent, ref CMURoboticsCancelOrderMessage args)
    {
        if (ent.Comp.ActiveOrder is not { } order)
            return;

        if (order.RepairEntity is { } repairEntity && !TerminatingOrDeleted(repairEntity))
            QueueDel(repairEntity);

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
            if (target.RequiredModules.Count > 0 && _random.Prob(0.5f))
                requiredModule = _random.Pick(target.RequiredModules);

            var order = new CMURoboticsOrder
            {
                Id = ent.Comp.NextOrderId++,
                Type = build ? CMURoboticsOrderType.Build : CMURoboticsOrderType.Repair,
                Target = target.Prototype,
                RequiredModule = requiredModule,
                Reward = target.Reward + (requiredModule != null ? 150 : 0) + _random.Next(0, 101),
                TargetName = GetPrototypeName(target.Prototype),
            };

            ent.Comp.AvailableOrders.Add(order);
        }
    }

    private bool TrySpawnRepairUnit(Entity<CMURoboticsOrderConsoleComponent> ent, CMURoboticsOrder order)
    {
        var coordinates = Transform(ent).Coordinates;
        EntityUid? spawned = null;

        foreach (var offset in SpawnOffsets)
        {
            spawned = Spawn(order.Target, coordinates.Offset(offset));
            break;
        }

        if (spawned is not { } unit)
            return false;

        var marker = EnsureComp<CMURoboticsOrderTargetComponent>(unit);
        marker.OrderId = order.Id;
        order.RepairEntity = unit;

        if (TryComp<DamageableComponent>(unit, out _))
        {
            var blunt = _prototypes.Index<DamageTypePrototype>("Blunt");
            _damageable.TryChangeDamage(
                unit,
                new DamageSpecifier(blunt, FixedPoint2.New(40)),
                ignoreResistances: true);
        }

        return true;
    }

    private EntityUid? FindDeliveryCandidate(Entity<CMURoboticsOrderConsoleComponent> ent, CMURoboticsOrder order)
    {
        var origin = _transform.GetWorldPosition(ent.Owner);
        EntityUid? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.DeliveryRadius))
        {
            if (!HasComp<CMUDroneAndroidComponent>(candidate))
                continue;

            if (order.Type == CMURoboticsOrderType.Repair)
            {
                if (!TryComp<CMURoboticsOrderTargetComponent>(candidate, out var marker) ||
                    marker.OrderId != order.Id)
                {
                    continue;
                }
            }
            else if (MetaData(candidate).EntityPrototype?.ID != order.Target.Id)
            {
                continue;
            }

            var distance = (origin - _transform.GetWorldPosition(candidate)).LengthSquared();
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = candidate;
        }

        return closest;
    }

    private bool HasAnyDroneNearby(Entity<CMURoboticsOrderConsoleComponent> ent)
    {
        foreach (var candidate in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.DeliveryRadius))
        {
            if (HasComp<CMUDroneAndroidComponent>(candidate))
                return true;
        }

        return false;
    }

    private bool ValidateDelivery(EntityUid drone, CMURoboticsOrder order, out string reason)
    {
        if (!_mobState.IsAlive(drone))
        {
            reason = Loc.GetString("cmu-robotics-console-reason-broken");
            return false;
        }

        if (order.Type == CMURoboticsOrderType.Repair &&
            (!HasComp<DamageableComponent>(drone) ||
             _damageable.GetTotalDamage(drone) > FixedPoint2.New(5)))
        {
            reason = Loc.GetString("cmu-robotics-console-reason-not-repaired");
            return false;
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
            order.Reward);
    }

    private string GetPrototypeName(EntProtoId prototype)
    {
        if (Loc.TryGetString($"ent-{prototype.Id}", out var entityName))
            return entityName;

        var name = _prototypes.Index<EntityPrototype>(prototype).Name;
        return Loc.TryGetString(name, out var localized) ? localized : name;
    }
}
