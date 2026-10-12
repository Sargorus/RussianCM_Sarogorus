using System.Linq;
using Content.Client.CMU14.Chat;
using Content.Shared.Chat;
using NUnit.Framework;
using Robust.Shared.Utility;

namespace Content.Tests.CMU14.Chat;

[TestFixture]
[TestOf(typeof(CMUChatMessageFormatting))]
public sealed class CMUChatMessageFormattingTest
{
    [TestCase(ChatChannel.OOC, "[color=cyan][bold]OOC[/bold]: [bold]Advancer:[/bold] проверка.", "Advancer: проверка.")]
    [TestCase(ChatChannel.OOC, "ООС: [bold][color=red]Advancer[/color]:[/bold] проверка.", "Advancer: проверка.")]
    [TestCase(ChatChannel.LOOC, "[bold]LOOC:[/bold] Advancer: проверка.", "Advancer: проверка.")]
    [TestCase(ChatChannel.LOOC, "HELP: [bold]Advancer:[/bold] проверка.", "Advancer: проверка.")]
    [TestCase(ChatChannel.Dead, "МЁРТВЫЕ: [bold]Advancer:[/bold] проверка.", "Advancer: проверка.")]
    [TestCase(ChatChannel.Dead, "АДМИН: [bold](Advancer):[/bold] проверка.", "(Advancer): проверка.")]
    [TestCase(ChatChannel.Admin, "[bold]АДМИН: предупреждение[/bold]", "предупреждение")]
    [TestCase(ChatChannel.AdminAlert, "[bold]ADMIN[/bold]: предупреждение", "предупреждение")]
    [TestCase(ChatChannel.AdminChat, "[bold]ASAY:[/bold] Advancer: проверка.", "Advancer: проверка.")]
    [TestCase(ChatChannel.MentorChat, "[bold]MENTOR[/bold]: Advancer: проверка.", "Advancer: проверка.")]
    [TestCase(ChatChannel.Radio, "[color=cyan]\\[КМД\\]: [font size=12]\"ОБМЕНЯЕМ МИШУ НА ОДНОГО ЗАЛОЖНИКА\"[/font][/color]", "\"ОБМЕНЯЕМ МИШУ НА ОДНОГО ЗАЛОЖНИКА\"")]
    [TestCase(ChatChannel.Radio, "[color=cyan][font size=12]\\[КМД\\] [bold]“Message”[/bold][/font][/color]", "“Message”")]
    public void RemovesOneChannelLabelAcrossFormattingTags(ChatChannel channel, string markup, string expected)
    {
        var formatted = FormattedMessage.FromMarkupOrThrow(markup);
        var message = new ChatMessage(channel, "", markup, default, null,
            display: new ChatDisplayMetadata(ChatDisplayKind.Unknown, channelLabel: "КМД"));

        var output = CMUChatMessageFormatting.StripChannelPrefix(formatted, message,
            channel == ChatChannel.Dead ? "МЁРТВЫЕ" : "АДМИН", "АДМИН");

        Assert.That(output.ToString(), Is.EqualTo(expected));
        Assert.That(output.Where(node => !node.IsPlainText), Is.EqualTo(formatted.Where(node => !node.IsPlainText)),
            "Removing a channel label must preserve font, color, and emphasis tags.");
    }

    [TestCase(ChatChannel.OOC, "OOC: Advancer: OOC: текст сообщения", "Advancer: OOC: текст сообщения")]
    [TestCase(ChatChannel.OOC, "OOC: OOC: сообщение", "OOC: сообщение")]
    [TestCase(ChatChannel.OOC, "Advancer: OOC: сообщение", "Advancer: OOC: сообщение")]
    [TestCase(ChatChannel.Server, "SYS сообщение", "SYS сообщение")]
    [TestCase(ChatChannel.Local, "OOC: это произнесённый текст", "OOC: это произнесённый текст")]
    [TestCase(ChatChannel.Emotes, "OOC: это эмоут", "OOC: это эмоут")]
    [TestCase(ChatChannel.Radio, "\\[ДРУГОЙ\\] сообщение", "[ДРУГОЙ] сообщение")]
    public void PreservesBodyText(ChatChannel channel, string markup, string expected)
    {
        var message = new ChatMessage(channel, "", markup, default, null,
            display: new ChatDisplayMetadata(ChatDisplayKind.Unknown, channelLabel: "КМД"));
        var output = CMUChatMessageFormatting.StripChannelPrefix(FormattedMessage.FromMarkupOrThrow(markup), message);

        Assert.That(output.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void RemovesDirectFrequencyPrefixWhilePreservingItsBodyColor()
    {
        const string markup = "[color=#5B9BD5][bold]FREQ 147.900[/bold][/color] [color=#C8D2E8]\"Message\"[/color]";
        var formatted = FormattedMessage.FromMarkupOrThrow(markup);
        var message = new ChatMessage(ChatChannel.Radio, "Message", markup, default, null,
            display: new ChatDisplayMetadata(ChatDisplayKind.Radio, channelLabel: "FREQ 147.900"));
        var output = CMUChatMessageFormatting.StripChannelPrefix(formatted, message);

        Assert.That(output.ToString(), Is.EqualTo("\"Message\""));
        Assert.That(output.Where(node => !node.IsPlainText), Is.EqualTo(formatted.Where(node => !node.IsPlainText)));
    }
}
