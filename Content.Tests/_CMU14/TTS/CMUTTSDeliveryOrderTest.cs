using System;
using System.Threading.Tasks;
using Content.Server.Corvax.TTS;
using NUnit.Framework;

namespace Content.Tests.Server.TTS;

[TestFixture]
public sealed class CMUTTSDeliveryOrderTest
{
    [Test]
    public async Task FasterSynthesisCannotOvertakeEarlierSpeech()
    {
        var order = new CMUTTSDeliveryOrder<string>();
        using var first = order.Reserve("speaker");
        using var second = order.Reserve("speaker");
        using var other = order.Reserve("other");
        Assert.That(first.Previous.IsCompleted, Is.True);
        Assert.That(other.Previous.IsCompleted, Is.True);
        Assert.That(second.Previous.IsCompleted, Is.False);
        first.Dispose();
        await second.Previous.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task DroppedMiddleMessageCannotReleaseLaterSpeechAheadOfTheFirst()
    {
        var order = new CMUTTSDeliveryOrder<string>();
        using var first = order.Reserve("speaker");
        using var dropped = order.Reserve("speaker");
        dropped.Dispose();
        using var last = order.Reserve("speaker");
        Assert.That(last.Previous.IsCompleted, Is.False);
        first.Dispose();
        await last.Previous.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void RestartDoesNotWaitForOldRoundSpeech()
    {
        var order = new CMUTTSDeliveryOrder<string>();
        using var old = order.Reserve("speaker");
        order.Reset();
        using var fresh = order.Reserve("speaker");
        Assert.That(fresh.Previous.IsCompleted, Is.True);
    }
}
