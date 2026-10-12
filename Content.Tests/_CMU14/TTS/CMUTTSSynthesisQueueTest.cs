using System;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Corvax.TTS;
using NUnit.Framework;

namespace Content.Tests.Server.TTS;

[TestFixture]
public sealed class CMUTTSSynthesisQueueTest
{
    private static TaskCompletionSource<byte[]?> Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Test]
    public async Task DuplicateMessagesShareOneApiRequest()
    {
        var result = Completion();
        var started = new TaskCompletionSource();
        var calls = 0;
        var queue = new CMUTTSSynthesisQueue((_, _, _) =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            return result.Task;
        }, () => TimeSpan.Zero);
        var first = queue.Enqueue("voice", "hello");
        var duplicate = queue.Enqueue("voice", "hello");
        Assert.That(duplicate, Is.SameAs(first));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.SetResult(new byte[] { 1 });
        Assert.That(await first.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(new byte[] { 1 }));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public async Task BurstsAreQueuedAndApiConcurrencyStaysBounded()
    {
        var releaseFirst = Completion();
        var firstStarted = new TaskCompletionSource();
        var calls = 0;
        var queue = new CMUTTSSynthesisQueue((_, text, _) =>
        {
            Interlocked.Increment(ref calls);
            if (text == "first")
            {
                firstStarted.SetResult();
                return releaseFirst.Task;
            }
            return Task.FromResult<byte[]?>(new byte[] { 2 });
        }, () => TimeSpan.Zero, maxConcurrent: 1, maxQueued: 1);
        var first = queue.Enqueue("voice", "first");
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = queue.Enqueue("voice", "second");
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(second.IsCompleted, Is.False);
        Assert.That(await queue.Enqueue("voice", "overflow"), Is.Null);
        releaseFirst.SetResult(new byte[] { 1 });
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(await second.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(new byte[] { 2 }));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task StaleQueuedMessagesDoNotReachTheApi()
    {
        var now = TimeSpan.Zero;
        var release = Completion();
        var calls = 0;
        var queue = new CMUTTSSynthesisQueue((_, _, _) =>
        {
            Interlocked.Increment(ref calls);
            return release.Task;
        }, () => now, maxConcurrent: 1);
        var first = queue.Enqueue("voice", "first");
        var stale = queue.Enqueue("voice", "stale");
        now = TimeSpan.FromSeconds(9);
        release.SetResult(new byte[] { 1 });
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(await stale.WaitAsync(TimeSpan.FromSeconds(5)), Is.Null);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public async Task RestartCancelsActiveRequestsAndReleasesWaitingMessages()
    {
        var started = new TaskCompletionSource();
        var canceled = new TaskCompletionSource();
        var queue = new CMUTTSSynthesisQueue(async (_, text, token) =>
        {
            if (text == "fresh")
                return new byte[] { 3 };
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                canceled.TrySetResult();
            }
            return null;
        }, () => TimeSpan.Zero, maxConcurrent: 1);
        var first = queue.Enqueue("voice", "first");
        var pending = queue.Enqueue("voice", "pending");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        queue.Reset();
        Assert.That(await first, Is.Null);
        Assert.That(await pending, Is.Null);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(await queue.Enqueue("voice", "fresh").WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(new byte[] { 3 }));
    }

    [Test]
    public async Task OldRequestCannotRemoveANewRequestWithTheSameTextAfterRestart()
    {
        var oldResult = Completion();
        var newResult = Completion();
        var oldStarted = new TaskCompletionSource();
        var newStarted = new TaskCompletionSource();
        var calls = 0;
        var queue = new CMUTTSSynthesisQueue((_, _, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                oldStarted.SetResult();
                return oldResult.Task;
            }
            newStarted.SetResult();
            return newResult.Task;
        }, () => TimeSpan.Zero, maxConcurrent: 2);
        var old = queue.Enqueue("voice", "same");
        await oldStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        queue.Reset();
        var fresh = queue.Enqueue("voice", "same");
        await newStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        oldResult.SetResult(new byte[] { 1 });
        Assert.That(await old, Is.Null);
        Assert.That(queue.Enqueue("voice", "same"), Is.SameAs(fresh));
        newResult.SetResult(new byte[] { 2 });
        Assert.That(await fresh.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(new byte[] { 2 }));
    }

    [Test]
    public async Task FailedRequestsReleaseTheirSlot()
    {
        var queue = new CMUTTSSynthesisQueue((_, text, _) => text == "bad"
            ? Task.FromException<byte[]?>(new InvalidOperationException("test"))
            : Task.FromResult<byte[]?>(new byte[] { 1 }), () => TimeSpan.Zero, maxConcurrent: 1);
        var bad = queue.Enqueue("voice", "bad");
        var good = queue.Enqueue("voice", "good");
        Assert.That(await bad.WaitAsync(TimeSpan.FromSeconds(5)), Is.Null);
        Assert.That(await good.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(new byte[] { 1 }));
    }
}
