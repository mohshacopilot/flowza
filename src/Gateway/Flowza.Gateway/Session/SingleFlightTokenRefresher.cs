using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Flowza.Gateway.Session;

/// <summary>
/// Decorator around the real refresher (phase 2 review, finding 2). Identity rotates refresh tokens and treats a
/// redeemed one, used again after a short grace period, as stolen: it then revokes the whole login. So a refresh token is
/// redeemed <b>once</b>, and its answer is shared:
/// <list type="bullet">
/// <item>parallel requests of one session (a page load) wait for the same refresh;</item>
/// <item>a request that still carries the old token because the browser never received the new cookie (cancelled or
/// lost answer) gets the same answer for <see cref="ReuseWindow"/>, instead of redeeming the old token again;</item>
/// <item>the refresh runs on its own, not bound to the request that started it.</item>
/// </list>
/// Answers are kept in memory, keyed by a hash of the refresh token. "Unavailable" is never kept, so the next request
/// tries again. One Gateway instance shares only its own answers; several instances need sticky sessions (T030).
/// </summary>
internal sealed class SingleFlightTokenRefresher(ITokenRefresher inner, TimeProvider time) : ITokenRefresher
{
    /// <summary>As long as an access token lives (15 minutes): an answer older than that is of no use to anyone.</summary>
    public static readonly TimeSpan ReuseWindow = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan s_sweepInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Flight> _flights = new(StringComparer.Ordinal);
    private long _nextSweepTicks;

    public int PendingOrKept => _flights.Count;

    public async Task<TokenRefreshOutcome> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);
        var now = time.GetUtcNow();
        Sweep(now);

        var key = Key(refreshToken);
        var flight = _flights.GetOrAdd(key, _ => new Flight(now, new Lazy<Task<TokenRefreshOutcome>>(() => RunAsync(key, refreshToken))));
        var outcome = await flight.Outcome.Value.WaitAsync(cancellationToken);

        // A kept answer may itself be close to running out: renew once more with the refresh token it brought.
        if (outcome is { Status: TokenRefreshStatus.Refreshed, Tokens: { } tokens }
            && tokens.ExpiresAt - time.GetUtcNow() <= GatewaySession.RefreshBefore
            && tokens.RefreshToken is { Length: > 0 } next
            && !string.Equals(next, refreshToken, StringComparison.Ordinal))
        {
            return await RefreshAsync(next, cancellationToken);
        }

        return outcome;
    }

    private async Task<TokenRefreshOutcome> RunAsync(string key, string refreshToken)
    {
        // Not the request's token: a browser that gives up must not leave a redeemed refresh token without its answer.
        var outcome = await inner.RefreshAsync(refreshToken, CancellationToken.None);
        if (outcome.Status == TokenRefreshStatus.Unavailable)
        {
            _flights.TryRemove(key, out _);
        }

        return outcome;
    }

    private void Sweep(DateTimeOffset now)
    {
        var next = Interlocked.Read(ref _nextSweepTicks);
        if (now.UtcTicks < next || Interlocked.CompareExchange(ref _nextSweepTicks, (now + s_sweepInterval).UtcTicks, next) != next)
        {
            return;
        }

        foreach (var (key, flight) in _flights)
        {
            if (now - flight.StartedAt > ReuseWindow)
            {
                _flights.TryRemove(new KeyValuePair<string, Flight>(key, flight));
            }
        }
    }

    private static string Key(string refreshToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private sealed record Flight(DateTimeOffset StartedAt, Lazy<Task<TokenRefreshOutcome>> Outcome);
}
