using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.RichText;
using Content.Client.Guidebook.Richtext;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared._RuCM.Guidebook;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RuCM.Guidebook;

/// <summary>Localized document links through the public IDocumentTag extension point.</summary>
public sealed class GOVFORGuideLink : BoxContainer, IDocumentTag
{
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (args.Count != 1 || !args.TryGetValue("Guide", out var id) || !GOVFORTrainingGuides.IsReference(id)
            || !IoCManager.Resolve<IPrototypeManager>().TryIndex<GuideEntryPrototype>(id, out var guide))
            return false;

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 4;
        var message = new FormattedMessage();
        message.AddText(Loc.GetString(guide.Name));
        var caption = new RichTextLabel { HorizontalExpand = true };
        caption.SetMessage(message);
        AddChild(caption);
        var button = new Button
        {
            Text = Loc.GetString("rucm-guide-open-document"),
            Name = "govfor-guide-" + id,
            MinHeight = 32,
            HorizontalAlignment = HAlignment.Left,
        };
        button.OnPressed += _ =>
        {
            if (this.TryGetParentHandler<ILinkClickHandler>(out var handler))
                handler.HandleClick(id);
        };
        AddChild(button);
        control = this;
        return true;
    }
}
