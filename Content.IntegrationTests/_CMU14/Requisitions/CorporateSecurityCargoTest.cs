using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
public sealed class CorporateSecurityCargoTest : GameTest
{
    [Test]
    public async Task SecurityCargoIsSoldByCorporateCatalog()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var console = SEntMan.SpawnEntity("AU14CorporateASRSConsole", map.GridCoords);
            var catalog = SComp<RequisitionsComputerComponent>(console);
            var crates = catalog.Categories
                .SelectMany(category => category.Entries)
                .Select(entry => entry.Crate.Id)
                .ToList();

            Assert.That(crates, Does.Contain("CMUWYSecurityBarrier"));
            Assert.That(crates, Does.Contain("CMUCrateM54C2"));
            Assert.That(crates, Does.Contain("CMUCrateNSG23"));
            Assert.That(crates, Does.Contain("CMUCrateMagazineM96S"));
            Assert.That(crates, Does.Contain("CMUCrateMagazineML66A"));

            var crate = SEntMan.SpawnEntity("CMUCrateWYSecurityBarrier", map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUDeployOnOpenComponent>(crate), Is.True,
                "the barrier crate must deploy its barrier when opened");
        });
    }
}
