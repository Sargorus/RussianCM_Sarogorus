using System;
using System.Collections.Generic;
using System.Threading;
using Content.Shared._RuCM.Qualifications;

namespace Content.Server._RuCM.Qualifications;

public sealed partial class QualificationService
{
    private readonly object _metricsLock = new();
    private WeakReference<QualificationStore>? _metricsSource;
    private Dictionary<string, double> _cachedMetrics = new();

    public Dictionary<string, double> CachedMetrics()
    {
        var cache = Volatile.Read(ref _cache);
        lock (_metricsLock)
        {
            // Store identity also detects a same-revision repository replacement.
            if (_metricsSource == null || !_metricsSource.TryGetTarget(out var source) || !ReferenceEquals(cache, source))
            {
                _cachedMetrics = ComputeMetrics(cache);
                // Don't retain a previous full history after a new store has been published.
                _metricsSource = new(cache);
            }
            return new(_cachedMetrics);
        }
    }
}
