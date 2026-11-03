using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Destructible;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Preferences;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Dialog;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

[TestFixture]
public sealed class CMULiaisonSafeTest : GameTest
{
    [Test]
    public async Task SafeHasReinforcedHealthAndLiaisonPinFlow()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var safe = SEntMan.SpawnEntity("RMCLockerSecureSafeLiaison", map.GridCoords);
            var safeComp = SComp<CMULiaisonSafeComponent>(safe);
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

            var liaison = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            Server.PlayerMan.SetAttachedEntity(ServerSession, liaison);

            var spawn = new PlayerSpawnCompleteEvent(
                liaison,
                ServerSession!,
                "AU14JobCivilianCorporateLiaison",
                false,
                false,
                1,
                EntityUid.Invalid,
                HumanoidCharacterProfile.Random());
            SEntMan.EventBus.RaiseLocalEvent(liaison, spawn, true);

            Assert.That(safeComp.Pin, Is.Not.Zero, "The safe always has a PIN.");
            var pin = SComp<CMULiaisonPinComponent>(liaison).Pin;
            Assert.That(pin, Is.Not.Zero);
            Assert.That(safeComp.Pin, Is.EqualTo(pin));

            // A safe created after the liaison spawned must share the same round PIN.
            var lateSafe = SEntMan.SpawnEntity("RMCLockerSecureSafeLiaison", map.GridCoords.Offset(new(2, 0)));
            Assert.That(SComp<CMULiaisonSafeComponent>(lateSafe).Pin, Is.EqualTo(pin));

            var activate = new ActivateInWorldEvent(liaison, safe, true);
            SEntMan.EventBus.RaiseLocalEvent(safe, activate);
            Assert.That(activate.Handled, Is.True);
            Assert.That(SEntMan.HasComponent<DialogComponent>(safe), Is.True);

            // Another entity cannot answer a dialog opened for the liaison.
            var intruder = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(3, 0)));
            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(SEntMan.GetNetEntity(intruder), pin.ToString()));
            Assert.That(SComp<LockComponent>(safe).Locked, Is.True);

            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(SEntMan.GetNetEntity(liaison), pin.ToString()));
            Assert.That(SComp<LockComponent>(safe).Locked, Is.False);
            Assert.That(SComp<EntityStorageComponent>(safe).Open, Is.True);

            // Clicking an open safe closes it instead of opening the PIN dialog again.
            var closeClick = new ActivateInWorldEvent(liaison, safe, true);
            SEntMan.EventBus.RaiseLocalEvent(safe, closeClick);
            Assert.That(closeClick.Handled, Is.True);
            Assert.That(SComp<EntityStorageComponent>(safe).Open, Is.False);

            // Reopen it so the lock verb still has an open storage to close.
            SEntMan.EventBus.RaiseLocalEvent(safe, new ActivateInWorldEvent(liaison, safe, true));
            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(SEntMan.GetNetEntity(liaison), pin.ToString()));
            Assert.That(SComp<EntityStorageComponent>(safe).Open, Is.True);

            var hands = SComp<HandsComponent>(liaison);
            var lockMenu = new GetVerbsEvent<AlternativeVerb>(
                liaison, safe, null, hands, true, true, true, new List<VerbCategory>());
            SEntMan.EventBus.RaiseLocalEvent(safe, lockMenu);
            var lockVerb = lockMenu.Verbs.Single(verb =>
                verb.Text == Loc.GetString("cmu-liaison-safe-lock-verb"));
            lockVerb.Act();
            Assert.That(SComp<LockComponent>(safe).Locked, Is.True);
            Assert.That(SComp<EntityStorageComponent>(safe).Open, Is.False);

            var unlockMenu = new GetVerbsEvent<AlternativeVerb>(
                liaison, safe, null, hands, true, true, true, new List<VerbCategory>());
            SEntMan.EventBus.RaiseLocalEvent(safe, unlockMenu);
            var unlockVerb = unlockMenu.Verbs.Single(verb =>
                verb.Text == Loc.GetString("cmu-liaison-safe-unlock-verb"));
            unlockVerb.Act();

            SEntMan.EventBus.RaiseLocalEvent(safe,
                new CMULiaisonSafePinEvent(SEntMan.GetNetEntity(liaison), pin.ToString()));
            Assert.That(SComp<LockComponent>(safe).Locked, Is.False);
            Assert.That(SComp<EntityStorageComponent>(safe).Open, Is.True);

            // The late safe accepts the same PIN and can be opened independently.
            var lateActivate = new ActivateInWorldEvent(liaison, lateSafe, true);
            SEntMan.EventBus.RaiseLocalEvent(lateSafe, lateActivate);
            SEntMan.EventBus.RaiseLocalEvent(lateSafe,
                new CMULiaisonSafePinEvent(SEntMan.GetNetEntity(liaison), pin.ToString()));
            Assert.That(SComp<EntityStorageComponent>(lateSafe).Open, Is.True);
        });
    }
}
