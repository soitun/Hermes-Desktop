namespace Hermes.Agent.LLM;

/// <summary>
/// Manages multiple API keys for a provider with thread-safe least-used selection
/// and automatic rotation on 401 failures.
/// Matches the official Hermes Agent credential_pool.py behavior.
/// </summary>
public sealed class CredentialPool
{
    private readonly object _lock = new();
    private readonly List<PoolEntry> _entries = new();
    private int _roundRobinIndex;

    /// <summary>A single credential in the pool.</summary>
    private sealed class PoolEntry
    {
        public required string ApiKey { get; init; }
        public string? Label { get; init; }
        public int RequestCount { get; set; }
        public bool IsFailed { get; set; }
        public DateTime? FailedAt { get; set; }
    }

    /// <summary>Cooldown before retrying a failed credential (1 hour for 429, 24h default).</summary>
    public TimeSpan FailedCooldown { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Selection strategy.</summary>
    public PoolStrategy Strategy { get; set; } = PoolStrategy.LeastUsed;

    /// <summary>Number of credentials in the pool.</summary>
    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    /// <summary>Whether any healthy credentials remain.</summary>
    public bool HasHealthyCredentials
    {
        get
        {
            lock (_lock)
            {
                return _entries.Any(e => !e.IsFailed || IsRecovered(e));
            }
        }
    }

    /// <summary>Add a credential to the pool.</summary>
    public void Add(string apiKey, string? label = null)
    {
        lock (_lock)
        {
            // Don't add duplicates
            if (_entries.Any(e => e.ApiKey == apiKey)) return;
            _entries.Add(new PoolEntry { ApiKey = apiKey, Label = label });
        }
    }

    /// <summary>
    /// Get the next API key to use based on the configured strategy.
    /// Returns null if all credentials are exhausted.
    /// </summary>
    public string? GetNext()
    {
        lock (_lock)
        {
            var healthy = _entries.Where(e => !e.IsFailed || IsRecovered(e)).ToList();
            if (healthy.Count == 0) return null;

            // If recovered, reset failure state
            foreach (var e in healthy.Where(e => e.IsFailed && IsRecovered(e)))
            {
                e.IsFailed = false;
                e.FailedAt = null;
            }

            PoolEntry selected;
            switch (Strategy)
            {
                case PoolStrategy.LeastUsed:
                    selected = healthy.MinBy(e => e.RequestCount) ?? healthy[0];
                    break;
                case PoolStrategy.RoundRobin:
                    _roundRobinIndex = _roundRobinIndex % healthy.Count;
                    selected = healthy[_roundRobinIndex];
                    _roundRobinIndex++;
                    break;
                case PoolStrategy.Random:
                    selected = healthy[Random.Shared.Next(healthy.Count)];
                    break;
                default: // FillFirst
                    selected = healthy[0];
                    break;
            }

            selected.RequestCount++;
            return selected.ApiKey;
        }
    }

    /// <summary>
    /// Mark a credential as failed (e.g. on 401/402/429 response).
    /// The pool will rotate to the next healthy credential.
    /// </summary>
    public void MarkFailed(string apiKey)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.ApiKey == apiKey);
            if (entry is not null)
            {
                entry.IsFailed = true;
                entry.FailedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>Reset all failure states.</summary>
    public void ResetAll()
    {
        lock (_lock)
        {
            foreach (var e in _entries)
            {
                e.IsFailed = false;
                e.FailedAt = null;
            }
        }
    }

    private bool IsRecovered(PoolEntry entry)
    {
        return entry.FailedAt.HasValue &&
               DateTime.UtcNow - entry.FailedAt.Value > FailedCooldown;
    }
}

public enum PoolStrategy
{
    FillFirst,
    RoundRobin,
    Random,
    LeastUsed
}
