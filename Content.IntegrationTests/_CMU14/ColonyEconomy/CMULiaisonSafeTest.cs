using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Destructible;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Lock;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Dialog;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

[TestFixture]
public sealed class CMULiaisonSafeTest : GameTest
{
    [Test]
    public async Task SafeWithoutPinOpensAndLiaisonCardCanSetPin()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var safe = SEntMan.SpawnEntity("RMCLockerSecureSafeLiaison", map.GridCoords);
            var safeComp = SComp<CMULiaisonSafeComponent>(safe);
            var lockComp = SComp<LockComponent>(safe);
            var storage = SComp<EntityStorageComponent>(safe);

            var liaison = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var hands = SComp<HandsComponent>(liaison);

            // The safe starts without a PIN and behaves as a normal locker.
            Assert.That(safeComp.Pin, Is.Empty);
            Assert.That(lockComp.Locked, Is.False);

            var openClick = new ActivateInWorldEvent(liaison, safe, true);
            SEntMan.EventBus.RaiseLocalEvent(safe, openClick);
            Assert.That(storage.Open, Is.True);
            Assert.That(lockComp.Locked, Is.False);

            var closeClick = new ActivateInWorldEvent(liaison, safe, true);
            SEntMan.EventBus.RaiseLocalEvent(safe, closeClick);
            Assert.That(storage.Open, Is.False);

            var destructible = SComp<DestructibleComponent>(safe);
            var thresholds = destructible.Thresholds
                .Select(entry => entry.Trigger)
                .OfType<DamageTrigger>()
                .Select(trigger => trigger.Damage)
                .ToList();
            Assert.That(thresholds, Does.Contain(FixedPoint2.New(safeComp.HitPoints)),
                "The safe's destruction threshold must come from its component.");
            Assert.That(thresholds, Does.Not.Contain(FixedPoint2.New(100)),
                "The inherited 100-damage closet threshold must be replaced.");

            var handsSystem = Server.System<SharedHandsSystem>();
            var card = SEntMan.SpawnEntity("AU14IDCardColonyCorporateLiaison", map.GridCoords);
            Assert.That(handsSystem.TryPickupAnyHand(liaison, card), Is.True);

            var setVerb = GetVerb(safe, liaison, hands, "cmu-liaison-safe-set-pin-verb");
            setVerb.Act();
            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.Set,
                    "1234"));
            Assert.That(safeComp.Pin, Is.EqualTo("1234"));

            // The card may be worn in the ID slot instead of held.
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(liaison, card, "id", silent: true), Is.True);

            // A character without the liaison card cannot set or change the PIN.
            var intruder = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(2, 0)));
            var intruderHands = SComp<HandsComponent>(intruder);
            var intruderVerbs = GetVerbs(safe, intruder, intruderHands);
            Assert.That(intruderVerbs.Verbs.Any(verb =>
                    verb.Text == Loc.GetString("cmu-liaison-safe-set-pin-verb")
                    || verb.Text == Loc.GetString("cmu-liaison-safe-change-pin-verb")),
                Is.False);

            // The card owner can lock the safe and then unlock it with the PIN.
            var lockVerb = GetVerb(safe, liaison, hands, "cmu-liaison-safe-lock-verb");
            lockVerb.Act();
            Assert.That(lockComp.Locked, Is.True);
            Assert.That(storage.Open, Is.False);

            var unlockClick = new ActivateInWorldEvent(liaison, safe, true);
            SEntMan.EventBus.RaiseLocalEvent(safe, unlockClick);
            Assert.That(unlockClick.Handled, Is.True);

            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.Unlock,
                    "0000"));
            Assert.That(lockComp.Locked, Is.True);

            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.Unlock,
                    "1234"));
            Assert.That(lockComp.Locked, Is.False);
            Assert.That(storage.Open, Is.True);

            // Changing the PIN requires the current PIN first.
            var changeVerb = GetVerb(safe, liaison, hands, "cmu-liaison-safe-change-pin-verb");
            changeVerb.Act();
            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.VerifyCurrent,
                    "0000"));
            Assert.That(safeComp.Pin, Is.EqualTo("1234"));

            changeVerb = GetVerb(safe, liaison, hands, "cmu-liaison-safe-change-pin-verb");
            changeVerb.Act();
            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.VerifyCurrent,
                    "1234"));
            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.Set,
                    "4321"));
            Assert.That(safeComp.Pin, Is.EqualTo("4321"));

            // A raw Set event without current-PIN verification must not change it.
            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.Set,
                    "9999"));
            Assert.That(safeComp.Pin, Is.EqualTo("4321"));

            var relockVerb = GetVerb(safe, liaison, hands, "cmu-liaison-safe-lock-verb");
            relockVerb.Act();
            Assert.That(lockComp.Locked, Is.True);

            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.Unlock,
                    "1234"));
            Assert.That(lockComp.Locked, Is.True);

            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(
                    SEntMan.GetNetEntity(liaison),
                    CMULiaisonSafePinAction.Unlock,
                    "4321"));
            Assert.That(lockComp.Locked, Is.False);
            Assert.That(storage.Open, Is.True);
        });
    }

    private GetVerbsEvent<AlternativeVerb> GetVerbs(
        EntityUid safe,
        EntityUid user,
        HandsComponent hands)
    {
        return new GetVerbsEvent<AlternativeVerb>(
            user,
            safe,
            null,
            hands,
            true,
            true,
            true,
            new List<VerbCategory>());
    }

    private AlternativeVerb GetVerb(
        EntityUid safe,
        EntityUid user,
        HandsComponent hands,
        string locId)
    {
        var verbs = GetVerbs(safe, user, hands);
        SEntMan.EventBus.RaiseLocalEvent(safe, verbs);
        return verbs.Verbs.Single(verb => verb.Text == Loc.GetString(locId));
    }
}
