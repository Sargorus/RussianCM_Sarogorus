using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Corvax.TTS;

/// <summary>
/// Coalesces identical requests and absorbs short bursts without unbounded API work or stale speech.
/// </summary>
public sealed class CMUTTSSynthesisQueue(
    Func<string, string, CancellationToken, Task<byte[]?>> generate,
    Func<TimeSpan> now,
    int maxConcurrent = 16,
    int maxQueued = 64,
    TimeSpan? maxWait = null,
    Action<Exception>? onError = null)
{
    private readonly object _lock = new();
    private readonly Dictionary<(string Speaker, string Text), Request> _pending = new();
    private readonly Queue<Request> _queue = new();
    private readonly TimeSpan _maxWait = maxWait ?? TimeSpan.FromSeconds(8);
    private int _active;
    private uint _generation;

    private sealed class Request((string Speaker, string Text) key, TimeSpan created, uint generation)
    {
        public readonly (string Speaker, string Text) Key = key;
        public readonly TimeSpan Created = created;
        public readonly uint Generation = generation;
        public readonly CancellationTokenSource Cancellation = new();
        public readonly TaskCompletionSource<byte[]?> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Active;
    }

    public Task<byte[]?> Enqueue(string speaker, string text)
    {
        lock (_lock)
        {
            var key = (speaker, text);
            if (_pending.TryGetValue(key, out var pending))
                return pending.Completion.Task;

            if (_pending.Count >= maxConcurrent + maxQueued)
                return Task.FromResult<byte[]?>(null);

            var request = new Request(key, now(), _generation);
            _pending.Add(key, request);
            _queue.Enqueue(request);
            Pump();
            return request.Completion.Task;
        }
    }

    public void Reset()
    {
        Request[] requests;
        lock (_lock)
        {
            _generation++;
            requests = _pending.Values.ToArray();
            _pending.Clear();
            _queue.Clear();
            foreach (var request in requests)
            {
                request.Completion.TrySetResult(null);
                // Cancellation and disposal are serialized with Execute's finally block.
                if (request.Active)
                    request.Cancellation.Cancel();
                else
                    request.Cancellation.Dispose();
            }
        }
    }

    // Called while holding _lock. Execute yields before invoking external code.
    private void Pump()
    {
        while (_active < maxConcurrent && _queue.TryDequeue(out var request))
        {
            if (now() - request.Created >= _maxWait)
            {
                _pending.Remove(request.Key);
                request.Completion.TrySetResult(null);
                request.Cancellation.Dispose();
                continue;
            }

            request.Active = true;
            _active++;
            _ = Execute(request);
        }
    }

    private async Task Execute(Request request)
    {
        await Task.Yield();
        byte[]? result = null;
        try
        {
            request.Cancellation.Token.ThrowIfCancellationRequested();
            result = await generate(request.Key.Speaker, request.Key.Text, request.Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Disable, restart and timeout all release the slot and any waiting deliveries.
        }
        catch (Exception e)
        {
            onError?.Invoke(e);
        }
        finally
        {
            lock (_lock)
            {
                _active--;
                if (request.Generation == _generation)
                {
                    _pending.Remove(request.Key);
                    request.Completion.TrySetResult(result);
                }
                request.Cancellation.Dispose();
                Pump();
            }
        }
    }
}
