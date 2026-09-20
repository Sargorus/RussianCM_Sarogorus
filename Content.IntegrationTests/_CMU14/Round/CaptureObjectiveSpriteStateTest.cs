using Content.Shared.CMU14.Round.Objectives.Type;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Round;

[TestFixture]
public sealed class CaptureObjectiveSpriteStateTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestCaptureWallFlag
          components:
          - type: CaptureObjective
            onceOnly: true
            maxHoldTimes: 5
          - type: CMUObjective
            id: cmu-test-capture
            objectiveDescription: Test capture objective
            allowedPresets: [TestPreset]
            factions: [govfor, opfor, clf, weyu]
            objectiveLevel: 1
        """;

    [TestCase("", "uaflag", TestName = "NoControllerUsesNeutralFlag")]
    [TestCase("govfor", "uaflag", TestName = "GovforUsesGovforFlag")]
    [TestCase("opfor", "uaflag_worn", TestName = "OpforUsesOpforWornFlag")]
    [TestCase("clf", "clfflag", TestName = "ClfUsesClfFlag")]
    [TestCase("weyu", "", TestName = "UnknownFactionKeepsSprite")]
    public async Task SpriteStateMatchesController(string controller, string expectedState)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var flag = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            flag = entities.Spawn("CMUTestCaptureWallFlag", MapCoordinates.Nullspace);
            var comp = entities.GetComponent<CaptureObjectiveComponent>(flag);
            comp.CurrentController = controller;
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<CaptureObjectiveComponent>(flag);
            Assert.That(comp.CurrentSpriteState, Is.EqualTo(expectedState),
                $"controller '{controller}' produced sprite state '{comp.CurrentSpriteState}', " +
                $"expected '{expectedState}'");
        });

        await pair.CleanReturnAsync();
    }
}