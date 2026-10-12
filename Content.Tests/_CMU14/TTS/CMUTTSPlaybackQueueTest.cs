using System;
using Content.Client.Corvax.TTS;
using NUnit.Framework;

namespace Content.Tests.Client.TTS;

[TestFixture]
public sealed class CMUTTSPlaybackQueueTest
{
    [Test]
    public void QueuesMaintainSpeechOrderIndependentlyForEachSpeaker()
    {
        var queue = new CMUTTSPlaybackQueue<string, string>(() => TimeSpan.Zero);
        queue.Enqueue("a", "first", 1);
        queue.Enqueue("a", "second", 1);
        queue.Enqueue("b", "independent", 1);
        Assert.That(queue.TryDequeue("b", out var other), Is.True);
        Assert.That(other, Is.EqualTo("independent"));
        queue.TryDequeue("a", out var first);
        queue.TryDequeue("a", out var second);
        Assert.That(first, Is.EqualTo("first"));
        Assert.That(second, Is.EqualTo("second"));
        Assert.That(queue.Keys, Is.Empty);
    }

    [Test]
    public void SpeakerBacklogIsBoundedAndRecoversAfterPlayback()
    {
        var queue = new CMUTTSPlaybackQueue<string, int>(() => TimeSpan.Zero);
        for (var i = 0; i < 6; i++)
            Assert.That(queue.Enqueue("speaker", i, 1), Is.True);
        Assert.That(queue.Enqueue("speaker", 7, 1), Is.False);
        queue.TryDequeue("speaker", out _);
        Assert.That(queue.Enqueue("speaker", 8, 1), Is.True);
    }

    [Test]
    public void TotalBacklogIsBounded()
    {
        var queue = new CMUTTSPlaybackQueue<int, int>(() => TimeSpan.Zero);
        for (var i = 0; i < 48; i++)
            Assert.That(queue.Enqueue(i, i, 1), Is.True);
        Assert.That(queue.Enqueue(49, 49, 1), Is.False);
    }

    [Test]
    public void ExpiredSpeechIsDiscardedAndDoesNotBlockFreshSpeech()
    {
        var now = TimeSpan.Zero;
        var queue = new CMUTTSPlaybackQueue<string, string>(() => now);
        for (var i = 0; i < 6; i++)
            queue.Enqueue("speaker", "stale", 1);
        now = TimeSpan.FromSeconds(13);
        Assert.That(queue.Enqueue("speaker", "fresh", 1), Is.True);
        queue.TryDequeue("speaker", out var fresh);
        Assert.That(fresh, Is.EqualTo("fresh"));
        Assert.That(queue.TryDequeue("speaker", out _), Is.False);
    }

    [Test]
    public void ExpiredOtherSpeakersCannotExhaustTheMemoryBudget()
    {
        var now = TimeSpan.Zero;
        var queue = new CMUTTSPlaybackQueue<int, string>(() => now);
        Assert.That(queue.Enqueue(1, "large", 32 * 1024 * 1024), Is.True);
        Assert.That(queue.Enqueue(2, "fresh", 1), Is.False);
        now = TimeSpan.FromSeconds(13);
        Assert.That(queue.Enqueue(2, "fresh", 1), Is.True);
        Assert.That(queue.TryDequeue(1, out _), Is.False);
    }

    [Test]
    public void ClearReleasesMemoryAndOnlyTheRequestedLane()
    {
        var queue = new CMUTTSPlaybackQueue<string, int>(() => TimeSpan.Zero);
        queue.Enqueue("preview", 1, 16 * 1024 * 1024);
        queue.Enqueue("radio", 2, 16 * 1024 * 1024);
        queue.Clear("preview");
        Assert.That(queue.Enqueue("preview", 3, 16 * 1024 * 1024), Is.True);
        queue.TryDequeue("radio", out var radio);
        Assert.That(radio, Is.EqualTo(2));
        queue.Clear();
        Assert.That(queue.Keys, Is.Empty);
        Assert.That(queue.Enqueue("preview", 4, 32 * 1024 * 1024), Is.True);
    }
}
