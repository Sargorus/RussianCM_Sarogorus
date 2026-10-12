using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Robust.Shared.Localization;

namespace Content.Server.Discord;

public enum RoundStatusWebhookKind
{
    Starting,
    Lobby,
    Running,
    Ended,
    Shutdown,
}

public readonly record struct RoundStatusWebhookColors(
    int Starting,
    int Running,
    int Ended,
    int Shutdown);

public readonly record struct RoundStatusWebhookData(
    int RoundId,
    int PlayerCount,
    string MapName,
    string Govfor,
    string Gamemode,
    IReadOnlyList<RoundStatusRecentGamemode> RecentGamemodes,
    TimeSpan? Duration = null);

public readonly record struct RoundStatusRecentGamemode(int RoundId, string Gamemode, TimeSpan Duration);

public readonly record struct RoundStatusWebhookMessageIds(
    ulong StatusMessageId,
    ulong RoundEndPingMessageId,
    ulong GamemodeVotePingMessageId);

public static class RoundStatusWebhook
{
    private const int DetailValueLength = 96;
    private const int RecentGamemodeLength = 72;
    private static string FooterText => Loc.GetString("discord-round-status-footer");

    public static readonly RoundStatusWebhookColors DefaultColors = new(
        0xF0C419,
        0x23EB49,
        0xCD1010,
        0x6B7280);

    private static readonly JsonSerializerOptions MessageIdsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static WebhookPayload CreatePayload(
        RoundStatusWebhookKind kind,
        RoundStatusWebhookData status,
        IEnumerable<string?> roleIds,
        RoundStatusWebhookColors? colors = null)
    {
        colors ??= DefaultColors;
        var content = BuildRoleMentions(roleIds);

        if (kind == RoundStatusWebhookKind.Shutdown)
            return CreateOfflinePayload(content, colors.Value);

        var fields = new List<WebhookEmbedField>
        {
            new() { Name = Loc.GetString("discord-round-status-field-status"), Value = GetState(kind), Inline = true },
            new() { Name = Loc.GetString("discord-round-status-field-players"), Value = status.PlayerCount.ToString(CultureInfo.InvariantCulture), Inline = true },
            new() { Name = Loc.GetString("discord-round-status-field-round"), Value = $"#{status.RoundId}", Inline = true },
        };

        if (status.Duration is { } duration)
            fields.Add(new WebhookEmbedField { Name = Loc.GetString("discord-round-status-field-runtime"), Value = FormatDuration(duration), Inline = true });

        fields.Add(new WebhookEmbedField { Name = Loc.GetString("discord-round-status-field-operation"), Value = FormatOperation(status), Inline = false });
        fields.Add(new WebhookEmbedField { Name = Loc.GetString("discord-round-status-field-recent-rounds"), Value = FormatRecentGamemodes(status.RecentGamemodes), Inline = false });
        fields.Add(CreateLastUpdatedField(DateTimeOffset.UtcNow));

        var payload = new WebhookPayload
        {
            Content = content,
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Title = GetTitle(kind, status.RoundId),
                    Description = GetDescription(kind),
                    Color = GetColor(kind, colors.Value),
                    Footer = new WebhookEmbedFooter { Text = FooterText },
                    Fields = fields,
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(content))
            payload.AllowedMentions.AllowRoleMentions();

        return payload;
    }

    private static WebhookPayload CreateOfflinePayload(string content, RoundStatusWebhookColors colors)
    {
        var payload = new WebhookPayload
        {
            Content = content,
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Title = GetTitle(RoundStatusWebhookKind.Shutdown, 0),
                    Description = Loc.GetString("discord-round-status-description-offline"),
                    Color = colors.Shutdown,
                    Footer = new WebhookEmbedFooter { Text = FooterText },
                    Fields = new List<WebhookEmbedField>
                    {
                        new() { Name = Loc.GetString("discord-round-status-field-status"), Value = Loc.GetString("discord-round-status-state-offline"), Inline = true },
                    },
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(content))
            payload.AllowedMentions.AllowRoleMentions();

        return payload;
    }

    public static WebhookPayload CreateRolePingPayload(IEnumerable<string?> roleIds, string? message = null)
    {
        var content = BuildRoleMentions(roleIds);
        if (!string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(message))
            content = $"{content} {message.Trim()}";

        var payload = new WebhookPayload
        {
            Content = content,
        };

        if (!string.IsNullOrWhiteSpace(content))
            payload.AllowedMentions.AllowRoleMentions();

        return payload;
    }

    public static string SerializeMessageIds(RoundStatusWebhookMessageIds messageIds)
    {
        return JsonSerializer.Serialize(messageIds, MessageIdsJsonOptions);
    }

    public static bool TryDeserializeMessageIds(string? json, out RoundStatusWebhookMessageIds messageIds)
    {
        messageIds = default;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            messageIds = JsonSerializer.Deserialize<RoundStatusWebhookMessageIds>(
                json,
                MessageIdsJsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static int ParseColor(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var color = value.Trim().TrimStart('#');
        if (color.Length != 6)
            return fallback;

        return int.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public static string? GetGamemodeRole(
        string? presetId,
        string? distressSignalRole,
        string? colonyFallRole,
        string? insurgencyRole)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return null;

        if (presetId.Equals("DistressSignal", StringComparison.OrdinalIgnoreCase))
            return NullIfEmpty(distressSignalRole);

        if (presetId.Equals("ColonyFall", StringComparison.OrdinalIgnoreCase))
            return NullIfEmpty(colonyFallRole);

        if (presetId.Equals("Insurgency", StringComparison.OrdinalIgnoreCase))
            return NullIfEmpty(insurgencyRole);

        return null;
    }

    public static IEnumerable<string> GetRoundStatusRoleIds(
        bool includeRoundEndRole,
        string? presetId,
        string? roundEndRole,
        string? distressSignalRole,
        string? colonyFallRole,
        string? insurgencyRole)
    {
        if (includeRoundEndRole && NullIfEmpty(roundEndRole) is { } endRole)
            yield return endRole;

        if (GetGamemodeRole(presetId, distressSignalRole, colonyFallRole, insurgencyRole) is { } gamemodeRole)
            yield return gamemodeRole;
    }

    public static bool TryGetMessageId(string? responseContent, out ulong messageId)
    {
        messageId = 0;

        if (string.IsNullOrWhiteSpace(responseContent))
            return false;

        try
        {
            var id = JsonNode.Parse(responseContent)?["id"]?.GetValue<string>();
            return ulong.TryParse(id, out messageId);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetMessageIdToDelete(ulong previousMessageId, ulong newMessageId, out ulong messageId)
    {
        messageId = 0;

        if (previousMessageId == 0 || previousMessageId == newMessageId)
            return false;

        messageId = previousMessageId;
        return true;
    }

    public static bool ShouldUpdate(TimeSpan now, TimeSpan nextUpdate, TimeSpan interval, bool hasStatusMessage)
    {
        return hasStatusMessage &&
               interval > TimeSpan.Zero &&
               now >= nextUpdate;
    }

    private static string BuildRoleMentions(IEnumerable<string?> roleIds)
    {
        return string.Join(
            " ",
            roleIds
                .Where(roleId => !string.IsNullOrWhiteSpace(roleId))
                .Distinct(StringComparer.Ordinal)
                .Select(roleId => $"<@&{roleId}>"));
    }

    private static WebhookEmbedField CreateLastUpdatedField(DateTimeOffset updatedAt)
    {
        return new WebhookEmbedField
        {
            Name = Loc.GetString("discord-round-status-field-last-updated"),
            Value = $"<t:{updatedAt.ToUnixTimeSeconds()}:R>",
            Inline = false,
        };
    }

    private static string GetTitle(RoundStatusWebhookKind kind, int roundId)
    {
        var round = ("id", (object) roundId.ToString(CultureInfo.InvariantCulture));
        return kind switch
        {
            RoundStatusWebhookKind.Starting => Loc.GetString("discord-round-status-title-starting"),
            RoundStatusWebhookKind.Lobby => Loc.GetString("discord-round-status-title-lobby"),
            RoundStatusWebhookKind.Running => Loc.GetString("discord-round-status-title-running", round),
            RoundStatusWebhookKind.Ended => Loc.GetString("discord-round-status-title-ended", round),
            RoundStatusWebhookKind.Shutdown => Loc.GetString("discord-round-status-title-offline"),
            _ => Loc.GetString("discord-round-status-title-unknown"),
        };
    }

    private static int GetColor(RoundStatusWebhookKind kind, RoundStatusWebhookColors colors)
    {
        return kind switch
        {
            RoundStatusWebhookKind.Starting => colors.Starting,
            RoundStatusWebhookKind.Lobby => colors.Starting,
            RoundStatusWebhookKind.Running => colors.Running,
            RoundStatusWebhookKind.Ended => colors.Ended,
            RoundStatusWebhookKind.Shutdown => colors.Shutdown,
            _ => colors.Running,
        };
    }

    private static string GetDescription(RoundStatusWebhookKind kind)
    {
        return kind switch
        {
            RoundStatusWebhookKind.Starting => Loc.GetString("discord-round-status-description-starting"),
            RoundStatusWebhookKind.Lobby => Loc.GetString("discord-round-status-description-lobby"),
            RoundStatusWebhookKind.Running => Loc.GetString("discord-round-status-description-running"),
            RoundStatusWebhookKind.Ended => Loc.GetString("discord-round-status-description-ended"),
            RoundStatusWebhookKind.Shutdown => Loc.GetString("discord-round-status-description-offline"),
            _ => Loc.GetString("discord-round-status-description-unknown"),
        };
    }

    private static string GetState(RoundStatusWebhookKind kind)
    {
        return kind switch
        {
            RoundStatusWebhookKind.Starting => Loc.GetString("discord-round-status-state-starting"),
            RoundStatusWebhookKind.Lobby => Loc.GetString("discord-round-status-state-lobby"),
            RoundStatusWebhookKind.Running => Loc.GetString("discord-round-status-state-running"),
            RoundStatusWebhookKind.Ended => Loc.GetString("discord-round-status-state-ended"),
            RoundStatusWebhookKind.Shutdown => Loc.GetString("discord-round-status-state-offline"),
            _ => Loc.GetString("discord-round-status-state-unknown"),
        };
    }

    private static string FormatOperation(RoundStatusWebhookData status)
    {
        return string.Join(
            "\n",
            FormatOperationLine("discord-round-status-operation-map", status.MapName),
            FormatOperationLine("discord-round-status-operation-govfor", status.Govfor),
            FormatOperationLine("discord-round-status-operation-mode", status.Gamemode));
    }

    private static string FormatOperationLine(string id, string value)
    {
        var shortened = Shorten(value, DetailValueLength);
        return Loc.GetString(id, ("value", (object) shortened));
    }

    private static string FormatRecentGamemodes(IReadOnlyList<RoundStatusRecentGamemode> recentGamemodes)
    {
        if (recentGamemodes.Count == 0)
            return Loc.GetString("discord-round-status-no-recent-rounds");

        return string.Join(
            "\n",
            recentGamemodes
                .Take(3)
                .Select(round => $"`#{round.RoundId}` {Shorten(round.Gamemode, RecentGamemodeLength)} - {FormatShortDuration(round.Duration)}"));
    }

    private static string UnknownIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Loc.GetString("discord-round-status-unknown-value")
            : value.Trim();
    }

    private static string Shorten(string value, int maxLength)
    {
        value = UnknownIfEmpty(value)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        while (value.Contains("  ", StringComparison.Ordinal))
        {
            value = value.Replace("  ", " ");
        }

        if (value.Length <= maxLength)
            return value;

        return maxLength <= 3
            ? value[..maxLength]
            : $"{value[..(maxLength - 3)].TrimEnd()}...";
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var hours = (int) duration.TotalHours;
        return Loc.GetString(
            "discord-round-status-duration-long",
            ("hours", (object) hours.ToString(CultureInfo.InvariantCulture)),
            ("minutes", (object) duration.Minutes.ToString(CultureInfo.InvariantCulture)),
            ("seconds", (object) duration.Seconds.ToString(CultureInfo.InvariantCulture)));
    }

    private static string FormatShortDuration(TimeSpan duration)
    {
        var hours = (int) duration.TotalHours;
        return duration.TotalHours >= 1
            ? Loc.GetString(
                "discord-round-status-duration-short-hours",
                ("hours", (object) hours.ToString(CultureInfo.InvariantCulture)),
                ("minutes", (object) duration.Minutes.ToString("D2", CultureInfo.InvariantCulture)))
            : Loc.GetString(
                "discord-round-status-duration-short-minutes",
                ("minutes", (object) duration.Minutes.ToString(CultureInfo.InvariantCulture)),
                ("seconds", (object) duration.Seconds.ToString("D2", CultureInfo.InvariantCulture)));
    }
}
