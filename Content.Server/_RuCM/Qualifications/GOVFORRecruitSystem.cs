using System;
using System.Collections.Generic;
using Content.Server.Chat.Managers;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RuCM.Qualifications;
using Content.Shared.CMU14;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Content.Server.GameTicking.GameTicker;

namespace Content.Server._RuCM.Qualifications;

/// <summary>
/// Adds the training job through public station and spawning APIs. Neither maps nor upstream
/// slot lists are changed; the selected GOVFOR base/ship provides the existing spawn anchor.
/// </summary>
public sealed class GOVFORRecruitSystem : EntitySystem
{
    [Dependency] private QualificationSystem _qualifications = default!;
    [Dependency] private AuRoundSystem _round = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private StationSystem _stations = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private SquadSystem _squads = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IChatManager _chat = default!;

    private readonly HashSet<EntityUid> _configured = new();
    private float _refresh;
    private int _slots = -1;
    private bool _warnedMissingAnchor;

    private bool Available => _qualifications.IsInsurgency &&
        _cfg.GetCVar(GOVFORRecruitJob.Slots) > 0;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRoundStart);
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnCandidates);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnDisallowed);
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnAllowed);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnSpawning,
            before: new[] { typeof(ContainerSpawnPointSystem), typeof(SpawnPointSystem) });
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _refresh -= frameTime;
        if (_refresh > 0) return;
        _refresh = 1;
        ConfigureSlots();
    }

    private void OnRoundStart(RulePlayerSpawningEvent ev)
    {
        // Maps and their jobs have been loaded; this event precedes job assignment.
        // Reapply our round-start count if a map rule replaced the station's slot list.
        _configured.Clear();
        ConfigureSlots();
    }

    private void ConfigureSlots()
    {
        var slots = Available ? Math.Clamp(_cfg.GetCVar(GOVFORRecruitJob.Slots), 1, 1024) : 0;
        if (_slots != slots)
        {
            _configured.Clear();
            _slots = slots;
        }
        _configured.RemoveWhere(station => Deleted(station));
        var anchors = slots > 0 ? FindAnchors() : new Dictionary<EntityUid, SpawnAnchor>();
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out var jobs))
        {
            if (slots > 0 && anchors.ContainsKey(station))
            {
                if (!_configured.Add(station)) continue;
                _stationJobs.SetRoundStartJobSlot(station, GOVFORRecruitJob.Id, slots, jobs);
                // Changes to the CVar preserve occupied places instead of replenishing them.
                var occupied = 0;
                foreach (var assigned in jobs.PlayerJobs.Values)
                    foreach (var job in assigned)
                        if (job.Id == GOVFORRecruitJob.Id) occupied++;
                _stationJobs.TrySetJobSlot(station, GOVFORRecruitJob.Id,
                    Math.Max(0, slots - occupied), true, jobs);
            }
            else if (_stationJobs.TryGetJobSlot(station, GOVFORRecruitJob.Id, out var remaining, jobs))
            {
                _configured.Remove(station);
                _stationJobs.SetRoundStartJobSlot(station, GOVFORRecruitJob.Id, 0, jobs);
                if (remaining != 0) _stationJobs.TrySetJobSlot(station, GOVFORRecruitJob.Id, 0, false, jobs);
            }
        }
    }

    private Dictionary<EntityUid, SpawnAnchor> FindAnchors()
    {
        var result = new Dictionary<EntityUid, SpawnAnchor>();
        var planet = _round.GetSelectedPlanet();
        if (planet == null) return result;
        var planetStation = _stations.GetStationInMap(_ticker.DefaultMap);
        var allShipStations = new HashSet<EntityUid>();
        var govforShipStations = new HashSet<EntityUid>();
        var allShipGrids = new HashSet<EntityUid>();
        var govforShipGrids = new HashSet<EntityUid>();
        var ships = EntityQueryEnumerator<ShipFactionComponent>();
        while (ships.MoveNext(out var ship, out var faction))
        {
            if (string.IsNullOrEmpty(faction.Faction)) continue;
            allShipGrids.Add(ship);
            var station = _stations.GetOwningStation(ship);
            if (station is { } owner) allShipStations.Add(owner);
            if (!faction.Faction.Equals("govfor", StringComparison.OrdinalIgnoreCase)) continue;
            govforShipGrids.Add(ship);
            if (station is { } govforOwner) govforShipStations.Add(govforOwner);
        }

        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (points.MoveNext(out var uid, out var point, out var xform))
        {
            if (_stations.GetOwningStation(uid, xform) is not { } station ||
                !HasComp<StationJobsComponent>(station)) continue;
            var onShip = allShipStations.Contains(station) ||
                xform.GridUid is { } grid && allShipGrids.Contains(grid);
            var onGovforShip = govforShipStations.Contains(station) ||
                xform.GridUid is { } govforGrid && govforShipGrids.Contains(govforGrid);
            if (planet.GovforInShip ? !onGovforShip : onShip || station != planetStation) continue;

            var priority = Priority(point, onGovforShip); // CMU14: ordinary arrivals are safe only on the owned carrier.
            if (priority < 0) continue;
            if (!result.TryGetValue(station, out var current) || priority < current.Priority)
                result[station] = new(xform.Coordinates, priority);
        }
        return result;
    }

    private static int Priority(SpawnPointComponent point, bool onGovforShip) // CMU14
    {
        if (point.SpawnType == SpawnPointType.LateJoinGovfor) return 3;
        // CMU14: Bush uses ordinary arrivals; colony arrivals on a planet are not training anchors.
        if (onGovforShip && point.SpawnType == SpawnPointType.LateJoin) return 4;
        if (point.SpawnType is not (SpawnPointType.Job or SpawnPointType.Unset)) return -1;
        var job = point.Job?.Id;
        if (job == GOVFORRecruitJob.Id) return 0;
        if (job?.StartsWith("AU14JobGOVFORadvisor", StringComparison.Ordinal) == true) return 1;
        if (job?.StartsWith("AU14JobGOVFORSquadRifleman", StringComparison.Ordinal) == true) return 2;
        return -1;
    }

    private void OnCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        if (!Available) ev.Jobs.RemoveAll(job => job.Id == GOVFORRecruitJob.Id);
    }

    private void OnDisallowed(ref GetDisallowedJobsEvent ev)
    {
        if (!Available) ev.Jobs.Add(GOVFORRecruitJob.Id);
    }

    private void OnAllowed(ref IsRoleAllowedEvent ev)
    {
        if (Available || ev.Jobs == null) return;
        foreach (var job in ev.Jobs)
            if (job.Id == GOVFORRecruitJob.Id) ev.Cancelled = true;
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (ev.JobId != GOVFORRecruitJob.Id) return;
        ConfigureSlots();
        if (Available && FindAnchors().ContainsKey(ev.Station)) return;
        // Clear the request, letting the normal picker retain bans and other restrictions.
        ev.JobId = null;
        _chat.DispatchServerMessage(ev.Player, Loc.GetString("rucm-recruit-unavailable"));
    }

    private void OnSpawning(PlayerSpawningEvent ev)
    {
        if (ev.Job?.Id != GOVFORRecruitJob.Id || ev.SpawnResult != null || !Available) return;
        if (ev.Station is not { } station || !FindAnchors().TryGetValue(station, out var anchor))
        {
            if (!_warnedMissingAnchor)
            {
                Log.Error("GOVFOR recruit has no training, instructor, rifleman or GOVFOR arrival anchor on its selected base/ship.");
                _warnedMissingAnchor = true;
            }
            return;
        }
        _warnedMissingAnchor = false;
        ev.SpawnResult = _spawning.SpawnPlayerMob(anchor.Coordinates, ev.Job,
            ev.HumanoidCharacterProfile, station);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId != GOVFORRecruitJob.Id) return;
        // CMU14: native spawning excludes Recruit; keep alternative spawn paths out of combat squads too.
        // An instructor may arrange the recruit's RP supervision.
        _squads.RemoveSquad(ev.Mob, new ProtoId<JobPrototype>(GOVFORRecruitJob.Id));
        _squads.MarineSetTitle(ev.Mob, Loc.GetString("rucm-recruit-job-name"));
        _chat.DispatchServerMessage(ev.Player, Loc.GetString("rucm-recruit-arrival"));
    }

    private readonly record struct SpawnAnchor(EntityCoordinates Coordinates, int Priority);
}
