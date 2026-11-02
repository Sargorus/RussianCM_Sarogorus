using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.TileMovement;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Movement;

[TestFixture]
public sealed class CMUTileMovementCollisionTest : GameTest
{
    [TestCase("CMUCrateSyntheticTransport")]
    [TestCase("CMURoboticsFabricator")]
    public async Task SolidObstacleStopsTileSlide(string obstaclePrototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid mob = default;
        NetEntity mobNet = default;

        await Server.WaitAssertion(() =>
        {
            mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.SpawnEntity(obstaclePrototype, map.GridCoords.Offset(new Vector2(2.5f, 0)));
            SEntMan.EnsureComponent<CMUTileMovementComponent>(mob);
            Server.PlayerMan.SetAttachedEntity(ServerSession, mob);
            mobNet = SEntMan.GetNetEntity(mob);
            SEntMan.GetComponent<InputMoverComponent>(mob).HeldMoveButtons = MoveButtons.Right;
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var position = SEntMan.GetComponent<TransformComponent>(mob).LocalPosition;
            Assert.That(position.X, Is.GreaterThan(0.5f), "The mob must start tile-moving.");
            Assert.That(position.X, Is.LessThan(1.9f),
                $"{obstaclePrototype} must stop the tile slide instead of letting the mob pass through it.");

            SEntMan.GetComponent<InputMoverComponent>(mob).HeldMoveButtons = MoveButtons.None;
        });

        await Client.WaitAssertion(() =>
        {
            if (!Client.EntMan.TryGetEntity(mobNet, out var clientMobNullable) ||
                clientMobNullable is not { } clientMob)
                return;

            var position = Client.EntMan.GetComponent<TransformComponent>(clientMob).LocalPosition;
            Assert.That(position.X, Is.LessThan(1.9f),
                "Client prediction must also stop at the obstacle.");
        });
    }
}
