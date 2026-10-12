#pragma warning disable RA0002 // Regression tests inspect server-owned callsign and squad state.

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Callsigns;
using Content.Shared.CMU14.Callsigns;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Chat;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
[TestOf(typeof(AU14CallsignSystem))]
public sealed class CMUCallsignTest : GameTest
{
    [Test]
    public async Task AssignmentStaysUniqueAcrossReassignmentAndRoleCollisions()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<AU14CallsignSystem>();
            var squad = SEntMan.SpawnEntity("SquadGovfor", map.GridCoords);
            var spawned = new List<EntityUid>();

            EntityUid AddMember(string preferred = null)
            {
                var uid = SEntMan.SpawnEntity(null, map.GridCoords);
                spawned.Add(uid);
                var callsign = SEntMan.AddComponent<AU14CallsignComponent>(uid);
                callsign.Faction = "govfor";
                SEntMan.AddComponent<SquadMemberComponent>(uid).Squad = squad;
                if (preferred != null)
                    SEntMan.AddComponent<AU14CallsignRoleComponent>(uid).Suffix = preferred;
                Reassign(uid);
                return uid;
            }

            void Reassign(EntityUid uid)
            {
                var currentSquad = SEntMan.GetComponent<SquadMemberComponent>(uid).Squad!.Value;
                var team = SEntMan.GetComponent<SquadTeamComponent>(currentSquad);
                var ev = new SquadMemberAddedEvent((currentSquad, team), uid);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
            }

            var leader = AddMember("01");
            var duplicateLeader = AddMember("01");
            var duplicateSign = SEntMan.GetComponent<AU14CallsignComponent>(duplicateLeader);
            Assert.That(duplicateSign.Suffix, Is.EqualTo("10"));
            Reassign(duplicateLeader);
            Assert.That(duplicateSign.Suffix, Is.EqualTo("10"), "Repeated assignment must not churn numbers.");

            var numbers = new HashSet<string> { "01", "10" };
            for (var i = 0; i < 95; i++)
            {
                var member = AddMember();
                var sign = SEntMan.GetComponent<AU14CallsignComponent>(member);
                Assert.That(numbers.Add(sign.Suffix), Is.True, "Station numbers must stay unique beyond 99.");
                var original = sign.Callsign;
                Reassign(member);
                Assert.That(sign.Callsign, Is.EqualTo(original));
            }
            Assert.That(numbers, Does.Contain("100"));

            var otherSquad = SEntMan.SpawnEntity("SquadGovforBravo", map.GridCoords);
            SEntMan.GetComponent<SquadMemberComponent>(duplicateLeader).Squad = otherSquad;
            Reassign(duplicateLeader);
            Assert.That(duplicateSign.Suffix, Is.EqualTo("10"), "A free assigned number must survive a transfer.");

            var replacement = AddMember();
            var replacementSign = SEntMan.GetComponent<AU14CallsignComponent>(replacement);
            Assert.That(replacementSign.Suffix, Is.EqualTo("10"), "The old element can reuse the released number.");
            SEntMan.GetComponent<SquadMemberComponent>(replacement).Squad = otherSquad;
            Reassign(replacement);
            Assert.That(replacementSign.Suffix, Is.EqualTo("11"), "A transfer must resolve an occupied number.");

            var groupMember = AddMember();
            var groupSign = SEntMan.GetComponent<AU14CallsignComponent>(groupMember);
            groupSign.Group = "TEST";
            Reassign(groupMember);
            duplicateSign.Group = "TEST";
            Reassign(duplicateLeader);
            Assert.That(duplicateSign.Callsign, Is.Not.EqualTo(groupSign.Callsign),
                "A custom group shares one numbering space across its source squads.");

            var loc = Server.ResolveDependency<ILocalizationManager>();
            Assert.That(system.GetSquadWord(squad), Is.EqualTo(loc.GetString("cmu-callsign-word-sabre")));
            var leaderSign = SEntMan.GetComponent<AU14CallsignComponent>(leader);
            Assert.That(leaderSign.Callsign, Is.EqualTo($"{system.GetSquadWord(squad)} 01"));

            // An on-air identity contains no role marker or character name.
            leaderSign.RadioMaskTick = Server.ResolveDependency<IGameTiming>().CurTick;
            var nameEvent = new TransformSpeakerNameEvent(leader, "Private name");
            SEntMan.EventBus.RaiseLocalEvent(leader, nameEvent);
            Assert.That(nameEvent.VoiceName, Is.EqualTo(leaderSign.Callsign));

            foreach (var uid in spawned)
                SEntMan.DeleteEntity(uid);
            SEntMan.DeleteEntity(squad);
            SEntMan.DeleteEntity(otherSquad);
        });
    }
}
