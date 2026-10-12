using System;
using System.Collections.Generic;
using Content.Shared._RuCM.Qualifications;
using Content.Shared.CCVar;
using Robust.Shared.Player;

namespace Content.Server._RuCM.Qualifications;

public sealed partial class QualificationSystem
{
    private int _policyPrototypeRevision = -1;
    private HashSet<string>? _appliedPolicyJobs;
    private int _appliedPrototypeRevision = -1;
    private bool _appliedPolicyActive;
    private string _appliedPolicyOverride = "";
    private string _policyBaseline = "";
    private readonly Dictionary<Guid, CMUPolicyStamp> _sentPolicyStates = new();

    private readonly record struct CMUPolicyStamp(ICommonSession Session, QualificationService Source, long Revision,
        QualificationMode Mode, bool Available, bool FailOpen, bool Staff, string Baseline,
        HashSet<string> Jobs, int PrototypeRevision);

    private void ApplyRolePolicy()
    {
        var active = Mode == QualificationMode.Enforce;
        var jobs = PolicyJobs();
        var currentOverride = _cfg.GetCVar(CCVars.GameRoleTimerOverride);
        if (ReferenceEquals(jobs, _appliedPolicyJobs)
            && active == _appliedPolicyActive
            && _policy.PrototypeRevision == _appliedPrototypeRevision
            && currentOverride == _appliedPolicyOverride)
            return;

        _policyBaseline = _policy.Apply(active, jobs);
        _appliedPolicyJobs = jobs;
        _appliedPolicyActive = active;
        _appliedPrototypeRevision = _policy.PrototypeRevision;
        _appliedPolicyOverride = _cfg.GetCVar(CCVars.GameRoleTimerOverride);
    }
}
