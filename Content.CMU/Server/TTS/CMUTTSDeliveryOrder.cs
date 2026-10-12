using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Content.Server.Corvax.TTS;

/// <summary>
/// Allows synthesis to run concurrently while delivering a speaker's messages in speech order.
/// Every reservation must be disposed, including failed or inaudible requests.
/// </summary>
public sealed class CMUTTSDeliveryOrder<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, Task> _tails = new();
    private readonly object _lock = new();

    public Reservation Reserve(TKey key)
    {
        lock (_lock)
        {
            var previous = _tails.GetValueOrDefault(key, Task.CompletedTask);
            var reservation = new Reservation(previous);
            _tails[key] = reservation.Finished;
            // Only the most recent reservation may remove the tail.
            reservation.OnFinished = () =>
            {
                lock (_lock)
                {
                    if (_tails.TryGetValue(key, out var tail) && tail == reservation.Finished)
                        _tails.Remove(key);
                }
            };
            return reservation;
        }
    }

    public void Reset()
    {
        lock (_lock)
            _tails.Clear();
    }

    public sealed class Reservation : IDisposable
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Action OnFinished = delegate { };
        internal Task Finished { get; }
        public Task Previous { get; }

        internal Reservation(Task previous)
        {
            Previous = previous;
            Finished = Task.WhenAll(previous, _completion.Task);
        }

        public void Dispose()
        {
            if (_completion.TrySetResult())
                _ = Cleanup();
        }

        private async Task Cleanup()
        {
            await Finished;
            OnFinished();
        }
    }
}
