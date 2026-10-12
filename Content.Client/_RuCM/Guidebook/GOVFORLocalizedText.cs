using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.Richtext;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Client._RuCM.Guidebook;

/// <summary>Plain localized explanatory text for RuCM guidebook navigation; no dynamic player data or markup evaluation.</summary>
public sealed class GOVFORLocalizedText : RichTextLabel, IDocumentTag
{
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (args.Count != 1 || !args.TryGetValue("Key", out var key) || !key.StartsWith("rucm-guide-")
            || !IoCManager.Resolve<ILocalizationManager>().TryGetString(key, out var text))
            return false;

        HorizontalExpand = true;
        var message = new FormattedMessage();
        message.AddText(text);
        SetMessage(message);
        control = this;
        return true;
    }
}
