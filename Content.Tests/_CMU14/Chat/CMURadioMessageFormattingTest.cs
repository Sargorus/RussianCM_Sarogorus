using System.Globalization;
using System.IO;
using System.Text;
using Content.Client.CMU14.Chat;
using Content.Shared.Chat;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Tests.CMU14.Chat;

[TestFixture]
public sealed class CMURadioMessageFormattingTest : ContentUnitTest
{
    [OneTimeSetUp]
    public void SetupLocalization()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Resources", "Locale")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);

        var resources = IoCManager.Resolve<IResourceManager>();
        var mount = resources.GetType().GetMethod("MountStreamAt", new[] { typeof(MemoryStream), typeof(ResPath) });
        Assert.That(mount, Is.Not.Null);
        var localization = IoCManager.Resolve<ILocalizationManager>();
        foreach (var locale in new[] { "en-US", "ru-RU" })
        {
            foreach (var file in new[] { "headset/headset-component.ftl", "chat/managers/chat-manager.ftl" })
            {
                var path = Path.Combine(directory!.FullName, "Resources", "Locale", locale, file);
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path)));
                mount!.Invoke(resources, new object[] { stream, new ResPath($"/Locale/{locale}/{file}") });
            }

            var interceptPath = Path.Combine(directory!.FullName, "Content.CMU", "Resources", "Locale", locale, "CMU14", "anprc-radio.ftl");
            var interceptStream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(interceptPath)));
            mount!.Invoke(resources, new object[] { interceptStream, new ResPath($"/Locale/{locale}/CMU14/anprc-radio.ftl") });

            localization.LoadCulture(new CultureInfo(locale, false));
        }
    }

    [TestCase("ru-RU", false)]
    [TestCase("ru-RU", true)]
    [TestCase("en-US", false)]
    [TestCase("en-US", true)]
    public void RadioWrapperDisplaysOnlyQuotedSpeechBesideChannel(string locale, bool bold)
    {
        var localization = IoCManager.Resolve<ILocalizationManager>();
        localization.SetCulture(new CultureInfo(locale, false));
        const string body = "ОБМЕНЯЕМ МИШУ НА ОДНОГО ЗАЛОЖНИКА";
        var markup = localization.GetString(bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("color", "#73bdf6"),
            ("fontType", "NotoSans"),
            ("fontSize", 12),
            ("channel", @"\[КМД\]"),
            ("name", "ЗВЕНО 01"),
            ("verb", "говорит"),
            ("message", body));
        var formatted = FormattedMessage.FromMarkupOrThrow(markup);
        var message = new ChatMessage(ChatChannel.Radio, body, markup, default, null,
            display: new ChatDisplayMetadata(ChatDisplayKind.Radio, channelLabel: "КМД"));
        var output = CMUChatMessageFormatting.StripChannelPrefix(formatted, message);

        Assert.That(output.ToString(), Is.EqualTo(locale == "ru-RU" ? $"\"{body}\"" : $"“{body}”"));
        Assert.That(formatted.ToString(), Does.StartWith("[КМД]"),
            "The legacy presentation still needs one channel label in its wrapper.");
    }

    [TestCase("ru-RU")]
    [TestCase("en-US")]
    public void InterceptWrapperDoesNotRevealAutomaticSenderIdentity(string locale)
    {
        var localization = IoCManager.Resolve<ILocalizationManager>();
        localization.SetCulture(new CultureInfo(locale, false));
        var markup = localization.GetString("anprc-chat-intercept-line",
            ("color", "#E09A3E"), ("sender", "ЗВЕНО 01"), ("net", "КМД"), ("message", "Message"));
        var message = new ChatMessage(ChatChannel.Radio, "Message", markup, default, null,
            display: new ChatDisplayMetadata(ChatDisplayKind.Radio, channelLabel: "INT"));
        var output = CMUChatMessageFormatting.StripChannelPrefix(FormattedMessage.FromMarkupOrThrow(markup), message);

        Assert.That(output.ToString(), Is.EqualTo(locale == "ru-RU" ? "\"Message\"" : "“Message”"));
    }

    [TestCase("ru-RU", false)]
    [TestCase("ru-RU", true)]
    [TestCase("en-US", false)]
    [TestCase("en-US", true)]
    public void LocalizedOocWrapperKeepsSenderAndBodyWithoutDuplicateChannel(string locale, bool patron)
    {
        var localization = IoCManager.Resolve<ILocalizationManager>();
        localization.SetCulture(new CultureInfo(locale, false));
        var markup = localization.GetString(patron ? "chat-manager-send-ooc-patron-wrap-message" : "chat-manager-send-ooc-wrap-message",
            ("playerName", "Advancer"), ("message", "проверка."), ("patronColor", "#73bdf6"));
        var message = new ChatMessage(ChatChannel.OOC, "проверка.", markup, default, null);
        var output = CMUChatMessageFormatting.StripChannelPrefix(FormattedMessage.FromMarkupOrThrow(markup), message);

        Assert.That(output.ToString(), Is.EqualTo("Advancer: проверка."));
    }
}
