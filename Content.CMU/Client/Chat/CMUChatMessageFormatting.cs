using System;
using Content.Shared.Chat;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Chat;

internal static class CMUChatMessageFormatting
{
    public static FormattedMessage StripChannelPrefix(
        FormattedMessage formatted,
        ChatMessage message,
        string? localizedChannelLabel = null,
        string? localizedAdminChannelLabel = null)
    {
        // Match visible text, so a closing bold/color tag between the label and colon
        // cannot prevent removal. Keep the nodes themselves to preserve sender styling.
        var text = formatted.ToString();
        var labels = message.Channel switch
        {
            ChatChannel.OOC => new[] { "OOC", "ООС" },
            ChatChannel.LOOC => new[] { "LOOC", "HELP" },
            ChatChannel.Dead => new[] { "DEAD", "ADMIN", localizedChannelLabel, localizedAdminChannelLabel },
            ChatChannel.Admin or ChatChannel.AdminAlert or ChatChannel.AdminChat =>
                new[] { "ADM", "ADMIN", "ASAY", "ALERT", localizedChannelLabel },
            ChatChannel.MentorChat => new[] { "MENTOR" },
            ChatChannel.Local or ChatChannel.Whisper or ChatChannel.Emotes => Array.Empty<string>(),
            _ => new[] { message.Display?.ChannelLabel }
        };

        var prefixLength = 0;
        foreach (var label in labels)
        {
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var prefix = message.Channel == ChatChannel.Radio && text.StartsWith($"[{label}]", StringComparison.OrdinalIgnoreCase)
                ? $"[{label}]"
                : label;
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var end = prefix.Length;
            if (end < text.Length && text[end] == ':')
                end++;
            else if (message.Channel != ChatChannel.Radio)
                continue;

            if (end >= text.Length || !char.IsWhiteSpace(text[end]))
                continue;

            while (end < text.Length && char.IsWhiteSpace(text[end]))
                end++;

            prefixLength = end;
            break;
        }

        if (prefixLength == 0)
            return formatted;

        var output = new FormattedMessage(formatted.Count);
        foreach (var node in formatted)
        {
            if (!node.IsPlainText || prefixLength == 0)
            {
                output.PushTag(node);
                continue;
            }

            var value = node.Value.StringValue ?? string.Empty;
            var removed = Math.Min(prefixLength, value.Length);
            prefixLength -= removed;
            if (removed < value.Length)
                output.AddText(value[removed..]);
        }

        return output;
    }
}
