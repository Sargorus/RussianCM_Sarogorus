using System;
using System.Collections.Generic;
using Content.Shared._RuCM.Qualifications;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._RuCM.Qualifications;

public sealed partial class QualificationSystem
{
    private readonly Dictionary<Guid, CMURosterEntry> _viewRoster = new();
    private readonly List<Guid> _staleRosterEntries = new();
    private readonly List<Guid> _completedNameLoads = new();
    private int _rosterGeneration;

    private readonly record struct CMURosterEntry(ICommonSession Session, string Name,
        SessionStatus Status, EntityUid? Entity, int Generation);

    private bool ViewRosterChanged()
    {
        _rosterGeneration++;
        var changed = false;
        foreach (var session in _players.Sessions)
        {
            var entry = new CMURosterEntry(session, session.Name, session.Status, session.AttachedEntity, _rosterGeneration);
            if (!_viewRoster.TryGetValue(session.UserId, out var previous)
                || previous.Session != entry.Session || previous.Name != entry.Name
                || previous.Status != entry.Status || previous.Entity != entry.Entity)
                changed = true;
            _viewRoster[session.UserId] = entry;
        }
        foreach (var (id, entry) in _viewRoster)
        {
            if (entry.Generation != _rosterGeneration)
                _staleRosterEntries.Add(id);
        }
        foreach (var id in _staleRosterEntries)
        {
            _viewRoster.Remove(id);
            changed = true;
        }
        _staleRosterEntries.Clear();
        return changed;
    }

    private void RemoveCompletedNameLoads()
    {
        foreach (var (id, task) in _nameLoads)
        {
            if (task.IsCompleted)
                _completedNameLoads.Add(id);
        }
        foreach (var id in _completedNameLoads)
            _nameLoads.Remove(id);
        _completedNameLoads.Clear();
    }

    private void OnQualificationUiClosed(Entity<QualificationUiComponent> ent, ref BoundUIClosedEvent args)
    {
        if (Equals(args.UiKey, QualificationUiKey.Main))
            _boundTargets.Remove(ent.Owner);
    }

    private void OnQualificationUiShutdown(Entity<QualificationUiComponent> ent, ref ComponentShutdown args) =>
        _boundTargets.Remove(ent.Owner);
}
