// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI.Loadouts;

// Lobby window for colonist skills: a list of options per category (only one per category) plus the special loadout list.
public sealed class ColonistSkillEditorWindow : DefaultWindow
{
    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>>? OnLoadoutPressed;
    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>>? OnLoadoutUnpressed;

    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>>? OnSpecialLoadoutPressed;
    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>>? OnSpecialLoadoutUnpressed;

    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>, Color?>? OnSpecialLoadoutColorChanged;

    public event Action? OnClothingEditorRequested;

    private const int ItemIconSize = 40;
    private const int MaxItemIcons = 4;

    private static readonly Color MutedColor = Color.FromHex("#8c8c8c");
    private static readonly Color GrantColor = Color.FromHex("#6fcf97");
    private static readonly Color WarningColor = Color.FromHex("#eb5757");
    private static readonly Color HeaderPanelColor = Color.FromHex("#25252a");

    private readonly IPrototypeManager _protoMan;
    private readonly SpriteSystem _sprite;

    private readonly Label _pointsLabel;
    private readonly Label _selectedLabel;
    private readonly ProgressBar _pointsBar;
    private readonly BoxContainer _perksBox;
    private readonly Label _specialLoadoutPointsLabel;
    private readonly BoxContainer _specialLoadoutBox;

    private sealed record CheckRow(ProtoId<LoadoutGroupPrototype> Group, ProtoId<LoadoutPrototype> Loadout, CheckBox Box, List<Control> Icons);

    // Paint controls of a special loadout item the player may color.
    private sealed class PaintRow
    {
        public required ProtoId<LoadoutGroupPrototype> Group;
        public required Button Toggle;
        public required Control Panel;
        public required ColorSelectorSliders Selector;
        public required List<Control> Icons;
        public Color Color = Color.White;
    }

    private readonly List<CheckRow> _perks = new();
    private readonly List<CheckRow> _specialLoadoutChecks = new();
    private readonly Dictionary<string, PaintRow> _paintRows = new();
    private bool _refreshingPaint;

    public int PerkRowCount => _perks.Count;

    public int SelectedPerkCount { get; private set; }

    public int PaintableLoadoutCount => _paintRows.Count;

    public bool CanPaint(string loadoutId)
    {
        return _paintRows.TryGetValue(loadoutId, out var row) && !row.Toggle.Disabled;
    }

    public void ApplyPaint(string loadoutId, Color color)
    {
        if (_paintRows.TryGetValue(loadoutId, out var row))
            OnSpecialLoadoutColorChanged?.Invoke(row.Group, loadoutId, color.WithAlpha(1f));
    }

    public IReadOnlyList<TextureRect> SpecialLoadoutIcons =>
        _specialLoadoutChecks.SelectMany(row => row.Icons).OfType<TextureRect>().ToList();

    public ColonistSkillEditorWindow(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        RoleLoadoutPrototype roleProto,
        RoleLoadout specialLoadout,
        RoleLoadoutPrototype specialLoadoutProto,
        ICommonSession session,
        IDependencyCollection collection)
    {
        _protoMan = collection.Resolve<IPrototypeManager>();
        _sprite = collection.Resolve<IEntityManager>().System<SpriteSystem>();

        Title = Loc.GetString("colonist-skill-editor-title");
        MinSize = new Vector2(560, 640);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };

        var headerPanel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = HeaderPanelColor },
        };
        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
        };
        headerPanel.AddChild(header);

        var topRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        _pointsLabel = new Label { HorizontalExpand = true };
        topRow.AddChild(_pointsLabel);
        _selectedLabel = new Label { FontColorOverride = MutedColor };
        topRow.AddChild(_selectedLabel);
        var resetButton = new Button { Text = Loc.GetString("colonist-skill-editor-reset") };
        resetButton.OnPressed += _ => OnResetPressed();
        topRow.AddChild(resetButton);
        header.AddChild(topRow);

        _pointsBar = new ProgressBar { MinValue = 0, MaxValue = 1, MinSize = new Vector2(0, 14), HorizontalExpand = true };
        header.AddChild(_pointsBar);

        root.AddChild(headerPanel);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var inner = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(0, 0, 8, 0),
        };
        scroll.AddChild(inner);
        root.AddChild(scroll);

        inner.AddChild(new Label
        {
            Text = Loc.GetString("colonist-skill-editor-perks-header"),
            StyleClasses = { "LabelHeadingBigger" },
        });
        _perksBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        inner.AddChild(_perksBox);

        inner.AddChild(new Label
        {
            Text = Loc.GetString("colonist-skill-editor-special-loadout-header"),
            StyleClasses = { "LabelHeadingBigger" },
            Margin = new Thickness(0, 8, 0, 0),
        });
        _specialLoadoutPointsLabel = new Label();
        inner.AddChild(_specialLoadoutPointsLabel);
        var clothingButton = new Button
        {
            Text = Loc.GetString("colonist-skill-editor-clothing-button"),
            HorizontalAlignment = HAlignment.Left,
            MinWidth = 160,
        };
        clothingButton.OnPressed += _ => RequestClothingEditor();
        inner.AddChild(clothingButton);
        _specialLoadoutBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        inner.AddChild(_specialLoadoutBox);

        Contents.AddChild(root);

        BuildRows(roleProto, specialLoadoutProto);
        RefreshLoadouts(profile, loadout, specialLoadout, session, collection);
    }

    public void RequestClothingEditor()
    {
        OnClothingEditorRequested?.Invoke();
    }

    public bool IsPerkDisabled(string perkId)
    {
        return _perks.First(row => row.Loadout.Id == perkId).Box.Disabled;
    }

    private void OnResetPressed()
    {
        foreach (var row in _perks)
            OnLoadoutUnpressed?.Invoke(row.Group, row.Loadout);

        foreach (var row in _specialLoadoutChecks)
            OnSpecialLoadoutUnpressed?.Invoke(row.Group, row.Loadout);
    }

    private void BuildRows(RoleLoadoutPrototype roleProto, RoleLoadoutPrototype specialLoadoutProto)
    {
        _perksBox.RemoveAllChildren();
        _specialLoadoutBox.RemoveAllChildren();
        _perks.Clear();
        _specialLoadoutChecks.Clear();
        _paintRows.Clear();

        var unlocks = CollectUnlocks(specialLoadoutProto);

        foreach (var groupId in roleProto.Groups)
        {
            if (!_protoMan.TryIndex(groupId, out var groupProto) || !groupProto.Hidden)
                continue;

            var perks = ResolveLoadouts(groupProto)
                .Where(perk => perk.Effects.OfType<SetSkillLoadoutEffect>().Any())
                .ToList();

            if (perks.Count == 0)
                continue;

            if (Loc.TryGetString(groupProto.Name, out var categoryName))
            {
                _perksBox.AddChild(new Label
                {
                    Text = categoryName,
                    StyleClasses = { "LabelHeading" },
                    Margin = new Thickness(0, 6, 0, 2),
                });
            }

            foreach (var perk in perks)
                AddPerkRow(groupId, perk, unlocks.GetValueOrDefault(perk.ID) ?? new List<string>());
        }

        foreach (var groupId in specialLoadoutProto.Groups)
        {
            if (!_protoMan.TryIndex(groupId, out var groupProto))
                continue;

            var entries = ResolveLoadouts(groupProto);
            if (entries.Count == 0 || entries.Any(entry => CustomClothingRules.TryGetEffect(entry, out _)))
                continue;

            if (Loc.TryGetString(groupProto.Name, out var groupName))
            {
                _specialLoadoutBox.AddChild(new Label
                {
                    Text = groupName,
                    StyleClasses = { "LabelHeading" },
                    Margin = new Thickness(0, 6, 0, 2),
                });
            }

            foreach (var loadoutProto in entries)
                AddSpecialCheckbox(groupId, loadoutProto);
        }
    }

    private List<LoadoutPrototype> ResolveLoadouts(LoadoutGroupPrototype groupProto)
    {
        var loadouts = new List<LoadoutPrototype>();
        foreach (var id in groupProto.Loadouts)
        {
            if (_protoMan.TryIndex(id, out var loadoutProto))
                loadouts.Add(loadoutProto);
        }

        return loadouts;
    }

    // Maps each skill option to the names of the special loadout items it opens.
    private Dictionary<string, List<string>> CollectUnlocks(RoleLoadoutPrototype specialLoadoutProto)
    {
        var unlocks = new Dictionary<string, List<string>>();

        foreach (var groupId in specialLoadoutProto.Groups)
        {
            if (!_protoMan.TryIndex(groupId, out var groupProto))
                continue;

            foreach (var entry in ResolveLoadouts(groupProto))
            {
                var name = LoadoutName(entry);
                foreach (var effect in entry.Effects.OfType<PerkRequirementLoadoutEffect>())
                {
                    foreach (var perk in effect.AnyOf)
                    {
                        var names = unlocks.GetValueOrDefault(perk.Id) ?? new List<string>();
                        if (!names.Contains(name))
                            names.Add(name);

                        unlocks[perk.Id] = names;
                    }
                }
            }
        }

        return unlocks;
    }

    private static string LoadoutName(LoadoutPrototype loadoutProto)
    {
        return Loc.TryGetString($"colonist-skill-editor-loadout-{loadoutProto.ID}", out var name) ? name : loadoutProto.ID;
    }

    private string SkillName(string skillId)
    {
        return _protoMan.TryIndex<EntityPrototype>(skillId, out var proto) ? proto.Name : skillId;
    }

    // One skill option: name and cost, what it grants, the age it needs and how many items it opens.
    private void AddPerkRow(ProtoId<LoadoutGroupPrototype> groupId, LoadoutPrototype perk, List<string> unlockedItems)
    {
        var name = LoadoutName(perk);
        var box = new CheckBox
        {
            Text = perk.Cost is { } cost
                ? Loc.GetString("colonist-skill-editor-extra-cost", ("name", name), ("cost", cost))
                : name,
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 0, 0, 4),
        };
        column.AddChild(box);

        var grants = perk.Effects.OfType<SetSkillLoadoutEffect>()
            .Select(effect => Loc.GetString("colonist-skill-editor-grants-part",
                ("skill", SkillName(effect.Skill.Id)),
                ("level", effect.Level)));
        column.AddChild(new Label
        {
            Text = Loc.GetString("colonist-skill-editor-grants", ("skills", string.Join(", ", grants))),
            FontColorOverride = GrantColor,
            Margin = new Thickness(28, 0, 0, 0),
        });

        foreach (var age in perk.Effects.OfType<AgeRequirementLoadoutEffect>())
        {
            column.AddChild(new Label
            {
                Text = Loc.GetString("colonist-skill-editor-min-age", ("age", age.MinAge)),
                FontColorOverride = MutedColor,
                Margin = new Thickness(28, 0, 0, 0),
            });
        }

        if (unlockedItems.Count > 0)
        {
            column.AddChild(new Label
            {
                Text = Loc.GetString("colonist-skill-editor-unlocks", ("count", unlockedItems.Count)),
                FontColorOverride = MutedColor,
                Margin = new Thickness(28, 0, 0, 0),
                MouseFilter = Control.MouseFilterMode.Pass,
                ToolTip = string.Join("\n", unlockedItems),
            });
        }

        _perksBox.AddChild(column);
        _perks.Add(new CheckRow(groupId, perk.ID, box, new List<Control>()));

        box.OnToggled += args =>
        {
            if (args.Pressed)
                OnLoadoutPressed?.Invoke(groupId, perk.ID);
            else
                OnLoadoutUnpressed?.Invoke(groupId, perk.ID);
        };
    }

    private void AddSpecialCheckbox(ProtoId<LoadoutGroupPrototype> groupId, LoadoutPrototype loadoutProto)
    {
        var name = LoadoutName(loadoutProto);

        var box = new CheckBox
        {
            Text = loadoutProto.Cost is { } cost
                ? Loc.GetString("colonist-skill-editor-extra-cost", ("name", name), ("cost", cost))
                : name,
            VerticalAlignment = VAlignment.Center,
        };

        var rowBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var icons = BuildItemIcons(loadoutProto);
        foreach (var icon in icons)
            rowBox.AddChild(icon);

        var textColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
        };
        textColumn.AddChild(box);
        foreach (var requirement in FormatRequirements(loadoutProto))
            textColumn.AddChild(new Label { Text = requirement, FontColorOverride = MutedColor });

        if (CustomClothingRules.IsPaintable(loadoutProto))
            AddPaintControls(textColumn, groupId, loadoutProto, icons);

        rowBox.AddChild(textColumn);
        _specialLoadoutBox.AddChild(rowBox);
        _specialLoadoutChecks.Add(new CheckRow(groupId, loadoutProto.ID, box, icons));

        box.OnToggled += args =>
        {
            if (args.Pressed)
                OnSpecialLoadoutPressed?.Invoke(groupId, loadoutProto.ID);
            else
                OnSpecialLoadoutUnpressed?.Invoke(groupId, loadoutProto.ID);
        };
    }

    // A button that opens a color picker under the row; the chosen color is saved with the loadout.
    private void AddPaintControls(
        BoxContainer column,
        ProtoId<LoadoutGroupPrototype> groupId,
        LoadoutPrototype loadoutProto,
        List<Control> icons)
    {
        var toggle = new Button
        {
            Text = Loc.GetString("colonist-skill-editor-paint-button"),
            ToggleMode = true,
            Disabled = true,
            HorizontalAlignment = HAlignment.Left,
            MinWidth = 140,
        };

        var selector = new ColorSelectorSliders { SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv };
        selector.Color = Color.White;

        var panel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Visible = false,
            MinWidth = 320,
        };
        panel.AddChild(selector);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var apply = new Button { Text = Loc.GetString("colonist-skill-editor-paint-apply") };
        var reset = new Button { Text = Loc.GetString("colonist-skill-editor-paint-reset") };
        buttons.AddChild(apply);
        buttons.AddChild(reset);
        panel.AddChild(buttons);

        column.AddChild(toggle);
        column.AddChild(panel);

        var row = new PaintRow
        {
            Group = groupId,
            Toggle = toggle,
            Panel = panel,
            Selector = selector,
            Icons = icons,
        };
        _paintRows[loadoutProto.ID] = row;

        toggle.OnToggled += args => panel.Visible = args.Pressed;

        selector.OnColorChanged += color =>
        {
            if (_refreshingPaint)
                return;

            row.Color = color;
            TintIcons(row, color);
        };

        apply.OnPressed += _ => OnSpecialLoadoutColorChanged?.Invoke(groupId, loadoutProto.ID, row.Color.WithAlpha(1f));
        reset.OnPressed += _ => OnSpecialLoadoutColorChanged?.Invoke(groupId, loadoutProto.ID, null);
    }

    private static void TintIcons(PaintRow row, Color color)
    {
        foreach (var icon in row.Icons.OfType<TextureRect>())
            icon.Modulate = new Color(color.R, color.G, color.B, icon.Modulate.A);
    }

    // Shows the saved color of each paintable item and only allows painting items that are picked.
    private void RefreshPaint(RoleLoadout specialLoadout)
    {
        _refreshingPaint = true;

        foreach (var (loadoutId, row) in _paintRows)
        {
            var selected = specialLoadout.SelectedLoadouts.TryGetValue(row.Group, out var picks)
                ? picks.FirstOrDefault(pick => pick.Prototype.Id == loadoutId)
                : null;

            row.Toggle.Disabled = selected == null;
            if (selected == null)
            {
                row.Toggle.Pressed = false;
                row.Panel.Visible = false;
            }

            row.Color = selected?.CustomColor ?? Color.White;
            row.Selector.Color = row.Color;
            TintIcons(row, row.Color);
        }

        _refreshingPaint = false;
    }

    // One line per requirement of a special loadout item.
    private static IEnumerable<string> FormatRequirements(LoadoutPrototype loadoutProto)
    {
        foreach (var requirement in loadoutProto.Effects.OfType<PerkRequirementLoadoutEffect>())
        {
            var perks = requirement.AnyOf.Select(perk => Loc.TryGetString($"colonist-skill-editor-loadout-{perk.Id}", out var name) ? name : perk.Id);
            yield return Loc.GetString("colonist-skill-editor-requires-any", ("perks", string.Join(" / ", perks)));
        }
    }

    private IEnumerable<EntProtoId> CollectItems(LoadoutPrototype loadoutProto)
    {
        if (loadoutProto.DummyEntity is { } dummy)
            return new[] { dummy };

        var sources = new List<IEquipmentLoadout> { loadoutProto };
        if (_protoMan.Resolve(loadoutProto.StartingGear, out var gear))
            sources.Add(gear);

        return sources.SelectMany(source => source.Equipment.Values
            .Concat(source.Inhand)
            .Concat(source.Storage.Values.SelectMany(list => list)));
    }

    // Makes small icons of the items a loadout gives.
    private List<Control> BuildItemIcons(LoadoutPrototype loadoutProto)
    {
        var icons = new List<Control>();
        var items = CollectItems(loadoutProto).Distinct().ToList();

        foreach (var itemId in items.Take(MaxItemIcons))
        {
            if (!_protoMan.TryIndex(itemId, out var itemProto))
                continue;

            icons.Add(new TextureRect
            {
                Texture = _sprite.GetPrototypeIcon(itemProto).GetFrame(RsiDirection.South, 0),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2(ItemIconSize, ItemIconSize),
                MouseFilter = Control.MouseFilterMode.Pass,
                ToolTip = itemProto.Name + "\n" + itemProto.Description,
            });
        }

        if (items.Count > MaxItemIcons)
        {
            icons.Add(new Label
            {
                Text = $"+{items.Count - MaxItemIcons}",
                FontColorOverride = MutedColor,
                VerticalAlignment = VAlignment.Center,
            });
        }

        return icons;
    }

    // Updates points, which options are picked and which are available.
    public void RefreshLoadouts(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        RoleLoadout specialLoadout,
        ICommonSession session,
        IDependencyCollection collection)
    {
        if (_protoMan.Resolve(loadout.Role, out var roleProto) && roleProto.Points != null && loadout.Points != null)
        {
            var max = roleProto.Points.Value;
            var remaining = loadout.Points.Value;

            _pointsLabel.Text = Loc.GetString("loadouts-points-limit", ("count", remaining), ("max", max));
            _pointsLabel.FontColorOverride = remaining <= 0 ? WarningColor : null;
            _pointsBar.MaxValue = Math.Max(max, 1);
            _pointsBar.Value = Math.Clamp(max - remaining, 0, Math.Max(max, 1));
        }

        if (_protoMan.Resolve(specialLoadout.Role, out var specialRoleProto) &&
            specialRoleProto.Points != null && specialLoadout.Points != null)
        {
            _specialLoadoutPointsLabel.Text = Loc.GetString("loadouts-points-limit",
                ("count", specialLoadout.Points.Value),
                ("max", specialRoleProto.Points.Value));
            _specialLoadoutPointsLabel.FontColorOverride = specialLoadout.Points.Value <= 0 ? WarningColor : null;
        }

        RefreshPerks(profile, loadout, session, collection);
        RefreshChecks(_specialLoadoutChecks, specialLoadout, profile, session, collection);
        RefreshPaint(specialLoadout);
    }

    private void RefreshPerks(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession session,
        IDependencyCollection collection)
    {
        var selectedCount = 0;

        foreach (var row in _perks)
        {
            var picks = loadout.SelectedLoadouts.TryGetValue(row.Group, out var groupPicks) ? groupPicks : new List<Loadout>();
            var selected = picks.Any(pick => pick.Prototype.Id == row.Loadout.Id);
            var takenByOther = !selected && picks.Count > 0;

            if (selected)
                selectedCount++;

            row.Box.Pressed = selected;

            FormattedMessage? reason = null;
            var valid = selected || (!takenByOther && loadout.IsValid(profile, session, row.Loadout, collection, out reason));

            row.Box.Disabled = !valid;
            row.Box.ToolTip = takenByOther
                ? Loc.GetString("colonist-skill-editor-category-taken")
                : !valid && reason != null ? reason.ToString() : null;
        }

        SelectedPerkCount = selectedCount;
        _selectedLabel.Text = Loc.GetString("colonist-skill-editor-selected", ("count", selectedCount), ("total", CategoryCount()));
    }

    private int CategoryCount()
    {
        return _perks.Select(row => row.Group.Id).Distinct().Count();
    }

    private static void RefreshChecks(
        List<CheckRow> rows,
        RoleLoadout loadout,
        HumanoidCharacterProfile profile,
        ICommonSession session,
        IDependencyCollection collection)
    {
        foreach (var row in rows)
        {
            var selected = loadout.SelectedLoadouts.TryGetValue(row.Group, out var picks) &&
                           picks.Any(pick => pick.Prototype.Id == row.Loadout.Id);

            row.Box.Pressed = selected;

            FormattedMessage? reason = null;
            var valid = selected || loadout.IsValid(profile, session, row.Loadout, collection, out reason);

            row.Box.Disabled = !valid;

            foreach (var icon in row.Icons)
                icon.Modulate = valid ? Color.White : new Color(1f, 1f, 1f, 0.4f);
            row.Box.ToolTip = !valid && reason != null ? reason.ToString() : null;
        }
    }
}
