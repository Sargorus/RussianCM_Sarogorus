using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.Examine;
using Content.Shared.NPC.Components;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Round;

[TestFixture]
public sealed class CaptureObjectiveExamineTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestCaptureFlagExamine
          components:
          - type: CaptureObjective
            pointIncrementTime: 800
          - type: CMUObjective
            id: cmu-test-capture-examine
            objectiveDescription: Test examine capture objective
            allowedPresets: [TestPreset]
            factions: [govfor, opfor]
            objectiveLevel: 1

        - type: entity
          id: CMUTestExaminer
          components:
          - type: NpcFactionMember
        """;

    [Test]
    public async Task PointsInfoOnlyShownToObjectiveFactions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            const string govforDisplayName = "Government Forces";
            const string clfDisplayName = "Colonial Liberation Front";

            var flag = entities.Spawn("CMUTestCaptureFlagExamine", MapCoordinates.Nullspace);
            var capComp = entities.GetComponent<CaptureObjectiveComponent>(flag);
            capComp.CurrentController = "govfor";
            capComp.ControllerDisplayName = govforDisplayName;
            capComp.TimeUntilNextIncrement = 30f;
            var objComp = entities.GetComponent<CMUObjectiveComponent>(flag);
            objComp.Active = true;

            var clfExaminer = entities.Spawn("CMUTestExaminer", MapCoordinates.Nullspace);
            entities.GetComponent<NpcFactionMemberComponent>(clfExaminer).Factions.Add("CLF");

            var govforExaminer = entities.Spawn("CMUTestExaminer", MapCoordinates.Nullspace);
            entities.GetComponent<NpcFactionMemberComponent>(govforExaminer).Factions.Add("GovFor");

            var isObjective =
                Loc.GetString("cmu-capture-objective-examine-is-objective");
            var notObjective =
                Loc.GetString("cmu-capture-objective-examine-not-objective");
            var controlledByGovfor =
                Loc.GetString("cmu-capture-objective-examine-controlled", ("faction", govforDisplayName));
            var controlledByClf =
                Loc.GetString("cmu-capture-objective-examine-controlled", ("faction", clfDisplayName));
            var pointsTime =
                Loc.GetString("cmu-capture-objective-examine-points-time",
                    ("time", FormatPointsTime(capComp.PointIncrementTime)));
            var countdown =
                Loc.GetString("cmu-capture-objective-examine-until-increment",
                    ("time", FormatPointsTime(capComp.TimeUntilNextIncrement)));

            var clfExamined = new ExaminedEvent(new FormattedMessage(), flag, clfExaminer, true, false);
            entities.EventBus.RaiseLocalEvent(flag, clfExamined);
            var clfMarkup = clfExamined.GetTotalMessage().ToMarkup();

            var govforExamined = new ExaminedEvent(new FormattedMessage(), flag, govforExaminer, true, false);
            entities.EventBus.RaiseLocalEvent(flag, govforExamined);
            var govforMarkup = govforExamined.GetTotalMessage().ToMarkup();

            capComp.CurrentController = "clf";
            capComp.ControllerDisplayName = clfDisplayName;

            var govforOnClfExamined = new ExaminedEvent(new FormattedMessage(), flag, govforExaminer, true, false);
            entities.EventBus.RaiseLocalEvent(flag, govforOnClfExamined);
            var govforOnClfMarkup = govforOnClfExamined.GetTotalMessage().ToMarkup();

            capComp.CurrentController = "govfor";
            capComp.ControllerDisplayName = govforDisplayName;
            objComp.Active = false;

            var inactiveExamined = new ExaminedEvent(new FormattedMessage(), flag, govforExaminer, true, false);
            entities.EventBus.RaiseLocalEvent(flag, inactiveExamined);
            var inactiveMarkup = inactiveExamined.GetTotalMessage().ToMarkup();

            Assert.Multiple(() =>
            {
                Assert.That(clfMarkup, Does.Contain(controlledByGovfor),
                    "CLF should see who currently controls the flag");
                Assert.That(clfMarkup, Does.Contain(notObjective),
                    "CLF should see this flag is not their faction's objective");
                Assert.That(clfMarkup, Does.Not.Contain(pointsTime),
                    "CLF has no objective for this flag, points info must be hidden");
                Assert.That(clfMarkup, Does.Not.Contain(countdown),
                    "CLF has no objective for this flag, countdown must be hidden");

                Assert.That(govforMarkup, Does.Contain(isObjective),
                    "Govfor should see this flag is their faction's objective");
                Assert.That(govforMarkup, Does.Contain(controlledByGovfor),
                    "Govfor has an objective for this flag, controller info must be shown");
                Assert.That(govforMarkup, Does.Contain(pointsTime),
                    "Govfor has an objective for this flag, points info must be shown");
                Assert.That(govforMarkup, Does.Contain(countdown),
                    "Govfor has an objective for this flag, countdown must be shown");

                Assert.That(govforOnClfMarkup, Does.Contain(controlledByClf),
                    "Govfor should still see who holds the flag");
                Assert.That(govforOnClfMarkup, Does.Contain(pointsTime),
                    "Govfor should still see the flag's points info");
                Assert.That(govforOnClfMarkup, Does.Not.Contain(countdown),
                    "Countdown must be hidden when the holder (CLF) cannot earn points for this flag");

                Assert.That(inactiveMarkup, Does.Contain(controlledByGovfor),
                    "Owner info should be visible even when no objective is active on the flag");
                Assert.That(inactiveMarkup, Does.Contain(notObjective),
                    "Everyone should see the flag is not their faction's objective when it is inactive");
                Assert.That(inactiveMarkup, Does.Not.Contain(isObjective),
                    "No one should see this flag as their objective while it is inactive");
                Assert.That(inactiveMarkup, Does.Not.Contain(pointsTime),
                    "Points info must be hidden while the flag objective is inactive");
                Assert.That(inactiveMarkup, Does.Not.Contain(countdown),
                    "Countdown must be hidden while the flag objective is inactive");
            });
        });

        await pair.CleanReturnAsync();
    }

    private string FormatPointsTime(float seconds)
    {
        var total = Math.Max(0, (int) Math.Ceiling(seconds));
        return total < 60
            ? Loc.GetString("cmu-capture-objective-duration-seconds", ("seconds", total))
            : Loc.GetString("cmu-capture-objective-duration-minutes-seconds",
                ("minutes", total / 60), ("seconds", total % 60));
    }
}