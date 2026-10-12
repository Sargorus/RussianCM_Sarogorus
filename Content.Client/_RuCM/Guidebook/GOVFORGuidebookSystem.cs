using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.Guidebook.Controls;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared._RuCM.Guidebook;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RuCM.Guidebook;

/// <summary>Adds RuCM regulations to the public guide window without editing its upstream root list.</summary>
public sealed partial class GOVFORGuidebookSystem : EntitySystem
{
    public const string DrillRegulations = GOVFORTrainingGuides.Drill;

    /// <summary>Opens a public reading reference with the full GOVFOR navigation, without any training mutation.</summary>
    public void OpenDocument(string id)
    {
        if (!GOVFORTrainingGuides.IsReference(id) || !ProtoMan.HasIndex<GuideEntryPrototype>(id)
            || !ProtoMan.HasIndex<GuideEntryPrototype>(GOVFORTrainingGuides.Root))
            return;

        _ui.GetUIController<GuidebookUIController>().OpenGuidebook(
            new List<ProtoId<GuideEntryPrototype>> { GOVFORTrainingGuides.Root },
            rootEntries: new() { GOVFORTrainingGuides.Root }, selected: id);
    }

    [Dependency] private IUserInterfaceManager _ui = default!;
    private readonly Dictionary<GuidebookWindow, Action> _windows = new();

    public override void Initialize()
    {
        base.Initialize();
        _ui.WindowRoot.OnChildAdded += OnWindowAdded;
        _ui.WindowRoot.OnChildRemoved += OnWindowRemoved;
        foreach (var window in _ui.WindowRoot.Children.ToArray())
            OnWindowAdded(window);
    }

    public override void Shutdown()
    {
        _ui.WindowRoot.OnChildAdded -= OnWindowAdded;
        _ui.WindowRoot.OnChildRemoved -= OnWindowRemoved;
        foreach (var (window, handler) in _windows)
            window.OnOpen -= handler;
        _windows.Clear();
        base.Shutdown();
    }

    private void OnWindowAdded(Control control)
    {
        if (control is not GuidebookWindow window || _windows.ContainsKey(window))
            return;

        Action handler = () => AddGOVFORNavigation(window);
        _windows.Add(window, handler);
        window.OnOpen += handler;
    }

    private void OnWindowRemoved(Control control)
    {
        if (control is GuidebookWindow window && _windows.Remove(window, out var handler))
            window.OnOpen -= handler;
    }

    private void AddGOVFORNavigation(GuidebookWindow window)
    {
        // Read only the public table of contents, preserving restricted/book-specific views.
        var entries = new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>();
        foreach (var item in window.Tree.Items)
        {
            if (item.Metadata is GuideEntry entry)
                entries.TryAdd(entry.Id, entry);
        }

        var roots = new HashSet<ProtoId<GuideEntryPrototype>>(entries.Keys);
        foreach (var entry in entries.Values)
            roots.ExceptWith(entry.Children);

        // The ordinary CMU guidebook has both these roots. Single books and rules stay scoped.
        if (!roots.Contains(GOVFORTrainingGuides.Sop) || !roots.Contains(GOVFORTrainingGuides.MilitaryCode)
            || !roots.Contains("CMUGuidebook")
            || !ProtoMan.TryIndex<GuideEntryPrototype>(GOVFORTrainingGuides.Root, out var govfor))
            return;

        // Retain CM guide entries that are only reached through text links, not the visible tree.
        foreach (var entry in ProtoMan.EnumerateCM<GuideEntryPrototype>())
            entries.TryAdd(entry.Id, entry);

        // UpdateGuides compares entry values before rebuilding roots; give our new root its own entry.
        entries[GOVFORTrainingGuides.Root] = new GuideEntry
        {
            Id = govfor.Id,
            Name = govfor.Name,
            Text = govfor.Text,
            Priority = govfor.Priority,
            Children = new(govfor.Children),
            FilterEnabled = govfor.FilterEnabled,
            RuleEntry = govfor.RuleEntry,
        };
        roots.Remove(GOVFORTrainingGuides.Sop);
        roots.Remove(GOVFORTrainingGuides.MilitaryCode);
        roots.Remove(DrillRegulations);
        roots.Add(GOVFORTrainingGuides.Root);
        window.UpdateGuides(entries, rootEntries: roots.ToList(), selected: window.Selected);
        window.Tree.SetAllExpanded(false);
        window.Tree.SetAllExpanded(true, 1);
    }
}
