using System.Net;
using System.Net.Http.Headers;
using Flowza.Gateway.Session;
using Flowza.Testing;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Flowza.Gateway.IntegrationTests.Session;

/// <summary>A refresh token is redeemed once and its answer shared (phase 2 review, finding 2); unit tests with a fake.</summary>
public sealed class SingleFlightTokenRefresherTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Hand-written fake of Identity's token endpoint: counts calls, answers with numbered tokens.</summary>
    private sealed class FakeRefresher(ManualTimeProvider clock) : ITokenRefresher
    {
        private int _calls;

        public int Calls => _calls;

        public TaskCompletionSource? Gate { get; set; }

        public TokenRefreshStatus Answer { get; set; } = TokenRefreshStatus.Refreshed;

        public async Task<TokenRefreshOutcome> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            return Answer == TokenRefreshStatus.Refreshed
                ? new TokenRefreshOutcome(TokenRefreshStatus.Refreshed, new RefreshedTokens($"access-{call}", $"{refreshToken}>{call}", null, clock.Now.AddMinutes(15)))
                : new TokenRefreshOutcome(Answer);
        }
    }

    [Fact]
    public async Task SE1_parallel_requests_of_one_session_share_one_refresh()
    {
        var clock = new ManualTimeProvider(Start);
        var fake = new FakeRefresher(clock) { Gate = new TaskCompletionSource() };
        var refresher = new SingleFlightTokenRefresher(fake, clock);

        var waiting = Enumerable.Range(0, 5).Select(_ => refresher.RefreshAsync("rt1", Ct)).ToList();
        fake.Gate.SetResult();
        var outcomes = await Task.WhenAll(waiting);

        fake.Calls.ShouldBe(1);
        outcomes.Select(o => o.Tokens!.AccessToken).Distinct().ShouldBe(["access-1"]);
    }

    [Fact]
    public async Task SE1_old_refresh_token_presented_again_gets_the_kept_answer_not_a_second_redemption()
    {
        var clock = new ManualTimeProvider(Start);
        var fake = new FakeRefresher(clock);
        var refresher = new SingleFlightTokenRefresher(fake, clock);

        var first = await refresher.RefreshAsync("rt1", Ct);
        clock.Now += TimeSpan.FromMinutes(5); // the browser never received the new cookie and comes back with rt1
        var second = await refresher.RefreshAsync("rt1", Ct);

        fake.Calls.ShouldBe(1);
        second.Tokens!.AccessToken.ShouldBe(first.Tokens!.AccessToken);
    }

    [Fact]
    public async Task SE1_kept_answer_close_to_expiry_is_renewed_with_its_own_refresh_token()
    {
        var clock = new ManualTimeProvider(Start);
        var fake = new FakeRefresher(clock);
        var refresher = new SingleFlightTokenRefresher(fake, clock);

        var first = await refresher.RefreshAsync("rt1", Ct);
        clock.Now += TimeSpan.FromMinutes(14);
        var renewed = await refresher.RefreshAsync("rt1", Ct);

        fake.Calls.ShouldBe(2);
        renewed.Tokens!.RefreshToken.ShouldBe($"{first.Tokens!.RefreshToken}>2", "renewed with the rotated token, not with rt1 again");
        renewed.Tokens.ExpiresAt.ShouldBe(clock.Now.AddMinutes(15));
    }

    [Fact]
    public async Task SE1_identity_unavailable_is_not_kept_so_the_next_request_tries_again()
    {
        var clock = new ManualTimeProvider(Start);
        var fake = new FakeRefresher(clock) { Answer = TokenRefreshStatus.Unavailable };
        var refresher = new SingleFlightTokenRefresher(fake, clock);

        (await refresher.RefreshAsync("rt1", Ct)).Status.ShouldBe(TokenRefreshStatus.Unavailable);
        fake.Answer = TokenRefreshStatus.Refreshed;
        (await refresher.RefreshAsync("rt1", Ct)).Status.ShouldBe(TokenRefreshStatus.Refreshed);

        fake.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task SE1_a_cancelled_request_does_not_cancel_the_refresh()
    {
        var clock = new ManualTimeProvider(Start);
        var fake = new FakeRefresher(clock) { Gate = new TaskCompletionSource() };
        var refresher = new SingleFlightTokenRefresher(fake, clock);
        using var browserGaveUp = new CancellationTokenSource();

        var abandoned = refresher.RefreshAsync("rt1", browserGaveUp.Token);
        await browserGaveUp.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(abandoned);

        fake.Gate.SetResult();
        var next = await refresher.RefreshAsync("rt1", Ct);

        fake.Calls.ShouldBe(1);
        next.Status.ShouldBe(TokenRefreshStatus.Refreshed);
    }

    [Fact]
    public async Task SE1_answers_are_forgotten_after_the_reuse_window()
    {
        var clock = new ManualTimeProvider(Start);
        var fake = new FakeRefresher(clock);
        var refresher = new SingleFlightTokenRefresher(fake, clock);

        await refresher.RefreshAsync("rt1", Ct);
        refresher.PendingOrKept.ShouldBe(1);

        clock.Now += SingleFlightTokenRefresher.ReuseWindow + TimeSpan.FromMinutes(2);
        await refresher.RefreshAsync("other", Ct);

        refresher.PendingOrKept.ShouldBe(1, "rt1's answer was swept; only the new one is kept");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Bearer", "error=\"invalid_token\", error_description=\"The user is no longer active.\"", true)]
    [InlineData(HttpStatusCode.Unauthorized, "Bearer", null, false)]
    [InlineData(HttpStatusCode.Unauthorized, "Bearer", "error=\"insufficient_scope\"", false)]
    [InlineData(HttpStatusCode.Unauthorized, "Basic", "error=\"invalid_token\"", false)]
    [InlineData(HttpStatusCode.Forbidden, "Bearer", "error=\"invalid_token\"", false)]
    public void IAM_04_AC3_only_a_refused_token_ends_the_session(HttpStatusCode status, string scheme, string? parameter, bool ends)
    {
        using var response = new HttpResponseMessage(status);
        response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(scheme, parameter));

        SessionEnding.IsRefusedToken(response).ShouldBe(ends);
        SessionEnding.ShouldEnd(LoggedInContext(), response).ShouldBe(ends);
    }

    [Fact]
    public void IAM_04_AC3_session_stays_when_identity_was_unreachable()
    {
        using var refused = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        refused.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Bearer", "error=\"invalid_token\""));
        var context = LoggedInContext();
        SessionEnding.MarkIdentityUnavailable(context);

        SessionEnding.ShouldEnd(context, refused).ShouldBeFalse();
        SessionEnding.ShouldEnd(new DefaultHttpContext(), refused).ShouldBeFalse("nobody logged in: nothing to end");
    }

    private static DefaultHttpContext LoggedInContext() =>
        new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], authenticationType: "test")) };
}
