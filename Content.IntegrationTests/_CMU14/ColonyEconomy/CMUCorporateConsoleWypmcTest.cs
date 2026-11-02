using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.ColonyEconomy;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

[TestFixture]
public sealed class CMUCorporateConsoleWypmcTest : GameTest
{
    [Test]
    public async Task WypmcCanBeCalledTwiceOnlyAtXClearance()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var console = SEntMan.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            var comp = SComp<CorporateConsoleComponent>(console);
            var system = Server.System<CorporateConsoleSystem>();
            var research = Server.System<SharedResearchDataTerminalSystem>();

            var limitMethod = typeof(CorporateConsoleSystem).GetMethod(
                "GetPartyCallLimit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var refreshMethod = typeof(CorporateConsoleSystem).GetMethod(
                "RefreshCalledParties",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(limitMethod, Is.Not.Null);
            Assert.That(refreshMethod, Is.Not.Null);

            research.Clearance = 5;
            comp.PartyCallCounts["WYPMCParty"] = 1;
            refreshMethod!.Invoke(system, new object[] { comp });
            Assert.That((int) limitMethod!.Invoke(system, new object[] { comp, "WYPMCParty" })!, Is.EqualTo(1));
            Assert.That(comp.CalledParties, Does.Contain("WYPMCParty"),
                "Below X clearance the WYPMC can only be called once.");

            research.Clearance = 6;
            refreshMethod.Invoke(system, new object[] { comp });
            Assert.That((int) limitMethod.Invoke(system, new object[] { comp, "WYPMCParty" })!, Is.EqualTo(2));
            Assert.That(comp.CalledParties, Does.Not.Contain("WYPMCParty"),
                "At X clearance the first WYPMC call must leave a second call available.");

            comp.PartyCallCounts["WYPMCParty"] = 2;
            refreshMethod.Invoke(system, new object[] { comp });
            Assert.That(comp.CalledParties, Does.Contain("WYPMCParty"),
                "After the second WYPMC call the party is exhausted.");

            comp.PartyCallCounts["WYHT"] = 1;
            refreshMethod.Invoke(system, new object[] { comp });
            Assert.That(comp.CalledParties, Does.Contain("WYHT"),
                "Other parties keep their normal single-call limit.");
        });
    }
}
