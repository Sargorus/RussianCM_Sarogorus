using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Round;
using Content.Shared._RuCM.Qualifications;
using Content.Shared.Administration;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server._RuCM.Qualifications;

public sealed partial class QualificationSystem
{
    [Dependency] private AuRoundSystem _round = default!;
    [Dependency] private QualificationRolePolicy _policy = default!;
    private float _policyTimer;
    private long _policyRevision = -1;
    private QualificationService? _policySource;
    private HashSet<string> _policyJobs = new();
    [Dependency] private Content.Shared.Interaction.SharedInteractionSystem _interaction = default!;
    // private readonly Dictionary<Guid, string> _sentPolicy = new(); // CMU14: use versioned policy stamps.
    private readonly Dictionary<Guid, DateTimeOffset> _entryRate = new();
    private readonly Dictionary<Guid, DateTimeOffset> _policyRequests = new();
    public bool IsInsurgency => string.Equals(_ticker.RunLevel == Content.Server.GameTicking.GameRunLevel.PreRoundLobby
        ? _round.SelectedPreset?.ID ?? _ticker.Preset?.ID
        : _ticker.CurrentPreset?.ID ?? _ticker.Preset?.ID ?? _round.SelectedPreset?.ID, "Insurgency", StringComparison.OrdinalIgnoreCase);
    private static bool IsDrillInstructor(QualificationAuthority actor) => actor.CurrentParticipant && QualificationRules.IsDrillInstructor(actor.Context.Job);
    // CMU14 method: policy polling must not copy the qualification store for each player.
    public bool CanBrowseRecords(ICommonSession player)
    {
        var actor = Authority(player);
        return Service.IsManagement(actor) || actor.CurrentOfficer || actor.CurrentCo || IsDrillInstructor(actor) ||
            Service.IsActiveInstructor(player.UserId);
    }
    // CMU14 method: invalidate cached jobs on prototype reload as well as store replacement.
    private HashSet<string> PolicyJobs()
    {
        if (_policySource == Service && _policyRevision == Service.Revision &&
            _policyPrototypeRevision == _policy.PrototypeRevision) return _policyJobs;
        _policySource = Service;
        _policyRevision = Service.Revision;
        _policyPrototypeRevision = _policy.PrototypeRevision;
        _policyJobs = Service.EnabledJobIds().Concat(new[] { "AU14JobGOVFORadvisor", "AU14JobGOVFORadvisorRMC", "AU14JobGOVFORadvisorUPP" }).Where(id =>
            ProtoMan.TryIndex<Content.Shared.Roles.JobPrototype>(id, out var job) &&
            job.RoundSide == Content.Shared.CMU14.Round.Roles.RoundJobSide.Govfor && !job.IsSynthetic).ToHashSet();
        return _policyJobs;
    }
    public void SynchronizeRolePolicy() => ApplyRolePolicy(); // CMU14: don't sort/reapply for every job lookup.
    private void InitializeEntryPoints()
    {
        SubscribeNetworkEvent<QualificationEntryRequest>((_, args) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (_entryRate.TryGetValue(args.SenderSession.UserId, out var at) && now - at < TimeSpan.FromSeconds(1)) return;
            _entryRate[args.SenderSession.UserId] = now;
            Open(args.SenderSession);
        });
        SubscribeNetworkEvent<QualificationPolicyRequest>((_, args) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (_policyRequests.TryGetValue(args.SenderSession.UserId, out var at) && now - at < TimeSpan.FromSeconds(1)) return;
            _policyRequests[args.SenderSession.UserId] = now;
            SendPolicy(args.SenderSession, true);
        });
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(GetRecordVerb);
    }
    // CMU14 method: reuse policy stamps; discard them when a session disconnects.
    private void UpdateEntryPoints(float frameTime)
    {
        if (!_ready) return;
        SynchronizeRolePolicy();
        _policyTimer -= frameTime;
        if (_policyTimer > 0) return;
        _policyTimer = 1;
        foreach (var player in _players.Sessions.Where(Online)) SendPolicy(player);
        foreach (var id in _sentPolicyStates.Keys.Where(id => !TargetOnline(id)).ToArray())
        { _sentPolicyStates.Remove(id); _entryRate.Remove(id); _policyRequests.Remove(id); }
    }
    // CMU14 method: unchanged policies allocate no per-job DTOs, requirements copies or signature strings.
    private void SendPolicy(ICommonSession player, bool force = false)
    {
        if (!_ready || !Online(player)) return;
        SynchronizeRolePolicy();
        var mode = Mode;
        var staff = CanBrowseRecords(player) || _admins.IsAdmin(player);
        var stamp = new CMUPolicyStamp(player, Service, Service.Revision, mode, Service.Available,
            _cfg.GetCVar(QualificationCVars.FailOpen), staff, _policyBaseline, PolicyJobs(), _policy.PrototypeRevision);
        if (!force && _sentPolicyStates.TryGetValue(player.UserId, out var previous) && previous == stamp) return;
        var state = new QualificationPolicyState { Active = mode == QualificationMode.Enforce, Staff = staff,
            Jobs = stamp.Jobs, Baseline = stamp.Baseline };
        foreach (var job in state.Jobs)
        {
            state.Eligibility[job] = CanTakeJob(player.UserId, job);
            if (_policy.BaseRequirements.TryGetValue(job, out var requirements)) state.BaseRequirements[job] = new(requirements);
        }
        _sentPolicyStates[player.UserId] = stamp;
        RaiseNetworkEvent(state, player);
    }
    private void GetRecordVerb(GetVerbsEvent<Verb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract || !TryComp<ActorComponent>(ev.User, out var actor) ||
            !TryComp<ActorComponent>(ev.Target, out var target) || actor.PlayerSession == target.PlayerSession ||
            !CanBrowseRecords(actor.PlayerSession)) return;
        var userEntity = ev.User; var targetEntity = ev.Target;
        ev.Verbs.Add(new Verb { Text = Loc.GetString("rucm-qualifications-open-record-verb"), Priority = 10,
            Act = () => OpenCharacterRecord(actor.PlayerSession, userEntity, targetEntity) });
    }
    public void OpenCharacterRecord(ICommonSession player, EntityUid user, EntityUid target)
    {
        // Re-resolve identity and access after the menu was opened; no client-supplied account ID.
        if (player.AttachedEntity != user || !Online(player) || !CanBrowseRecords(player) ||
            !TryComp<ActorComponent>(target, out var actor) || actor.PlayerSession.AttachedEntity != target ||
            !Online(actor.PlayerSession) || !_interaction.InRangeUnobstructed(user, Transform(target).Coordinates)) return;
        Open(player, target: actor.PlayerSession.UserId);
    }
}
