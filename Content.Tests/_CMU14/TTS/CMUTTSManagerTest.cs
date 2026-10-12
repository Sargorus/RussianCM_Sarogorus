#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Corvax.TTS;
using Content.Shared.Corvax.CCCVars;
using Moq;
using NUnit.Framework;
using Robust.Shared.Configuration;
using Robust.Shared.Log;

namespace Content.Tests.Server.TTS;

[TestFixture]
public sealed class CMUTTSManagerTest
{
    [Test]
    public async Task FrequentlyUsedSpeechSurvivesCacheEviction()
    {
        using var handler = new Handler((_, _) => Task.FromResult(WaveResponse()));
        using var client = new HttpClient(handler);
        var manager = Manager(client, 2);
        await manager.ConvertTextToSpeech("voice", "first");
        await manager.ConvertTextToSpeech("voice", "second");
        await manager.ConvertTextToSpeech("voice", "first");
        await manager.ConvertTextToSpeech("voice", "third");
        await manager.ConvertTextToSpeech("voice", "first");
        Assert.That(handler.Calls, Is.EqualTo(3));
        await manager.ConvertTextToSpeech("voice", "second");
        Assert.That(handler.Calls, Is.EqualTo(4));
    }

    [Test]
    public async Task ZeroCacheLimitDisablesCaching()
    {
        using var handler = new Handler((_, _) => Task.FromResult(WaveResponse()));
        using var client = new HttpClient(handler);
        var manager = Manager(client, 0);
        await manager.ConvertTextToSpeech("voice", "hello");
        await manager.ConvertTextToSpeech("voice", "hello");
        Assert.That(handler.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task InvalidAudioIsRejectedAndNeverCached()
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[128]),
        }));
        using var client = new HttpClient(handler);
        var manager = Manager(client);
        Assert.That(await manager.ConvertTextToSpeech("voice", "hello"), Is.Null);
        Assert.That(await manager.ConvertTextToSpeech("voice", "hello"), Is.Null);
        Assert.That(handler.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task OversizedAudioIsRejected()
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[8 * 1024 * 1024 + 1]),
        }));
        using var client = new HttpClient(handler);
        Assert.That(await Manager(client).ConvertTextToSpeech("voice", "hello"), Is.Null);
    }

    [Test]
    public async Task RoundCancellationReachesTheHttpRequest()
    {
        var started = new TaskCompletionSource();
        var canceled = false;
        using var handler = new Handler(async (_, token) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
                throw;
            }
            return WaveResponse();
        });
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var pending = Manager(client).ConvertTextToSpeech("voice", "hello", cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(5)), Is.Null);
        Assert.That(canceled, Is.True);
    }

    [Test]
    public async Task CacheResetCannotBeUndoneByAnOlderRequest()
    {
        var result = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new Handler((_, _) => result.Task);
        using var client = new HttpClient(handler);
        var manager = Manager(client);
        var old = manager.ConvertTextToSpeech("voice", "hello");
        manager.ResetCache();
        result.SetResult(WaveResponse());
        Assert.That(await old.WaitAsync(TimeSpan.FromSeconds(5)), Is.Not.Null);
        handler.Generate = (_, _) => Task.FromResult(WaveResponse());
        await manager.ConvertTextToSpeech("voice", "hello");
        Assert.That(handler.Calls, Is.EqualTo(2));
    }

    private static TTSManager Manager(HttpClient client, int cacheLimit = 250)
    {
        var cfg = new Mock<IConfigurationManager>();
        cfg.Setup(config => config.GetCVar(CCCVars.TTSApiTimeout)).Returns(5);
        var manager = new TTSManager();
        Set(manager, "_cfg", cfg.Object);
        Set(manager, "_sawmill", Mock.Of<ISawmill>());
        Set(manager, "_httpClient", client);
        Set(manager, "_apiUrl", "http://localhost/tts");
        Set(manager, "_apiToken", "test-token");
        Set(manager, "_maxCachedCount", cacheLimit);
        return manager;
    }

    private static void Set(TTSManager manager, string field, object value)
    {
        typeof(TTSManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, value);
    }

    private static HttpResponseMessage WaveResponse()
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8);
        writer.Write(38u);
        writer.Write("WAVEfmt "u8);
        writer.Write(16u);
        writer.Write((ushort) 1);
        writer.Write((ushort) 1);
        writer.Write(24000u);
        writer.Write(48000u);
        writer.Write((ushort) 2);
        writer.Write((ushort) 16);
        writer.Write("data"u8);
        writer.Write(2u);
        writer.Write((short) 0);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(memory.ToArray()) };
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> generate) : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Generate = generate;
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Generate(request, cancellationToken);
        }
    }
}
