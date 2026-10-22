using Flowza.Gateway.Proxy;
using Flowza.Gateway.Session;
using Microsoft.AspNetCore.Authentication;

namespace Flowza.Gateway.IntegrationTests.Session;

/// <summary>Unit tests of the Gateway's small rules (no server needed).</summary>
public sealed class SessionRulesTests
{
    private static readonly DateTimeOffset LoginAt = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IAM_08_AC1_absolute_limit_is_12_hours_from_login()
    {
        var properties = new AuthenticationProperties();
        SessionRules.MarkLogin(properties, LoginAt);

        SessionRules.IsWithinAbsoluteLimit(properties, LoginAt).ShouldBeTrue();
        SessionRules.IsWithinAbsoluteLimit(properties, LoginAt.AddHours(12).AddTicks(-1)).ShouldBeTrue();
        SessionRules.IsWithinAbsoluteLimit(properties, LoginAt.AddHours(12)).ShouldBeFalse();
    }

    [Fact]
    public void IAM_08_AC1_session_without_login_time_is_not_valid()
    {
        SessionRules.IsWithinAbsoluteLimit(new AuthenticationProperties(), LoginAt).ShouldBeFalse();
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(2.01, false)]
    [InlineData(2, true)]
    [InlineData(-1, true)]
    public void SE1_access_token_is_renewed_when_less_than_2_minutes_are_left(double minutesLeft, bool expected)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "expires_at", Value = LoginAt.AddMinutes(minutesLeft).ToString("O") }]);

        SessionRules.AccessTokenNeedsRefresh(properties, LoginAt).ShouldBe(expected);
    }

    [Fact]
    public void SE1_unknown_token_expiry_counts_as_expired()
    {
        SessionRules.AccessTokenNeedsRefresh(new AuthenticationProperties(), LoginAt).ShouldBeTrue();
        SessionRules.IsAccessTokenExpired(new AuthenticationProperties(), LoginAt).ShouldBeTrue();
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    public void SE1_access_token_is_expired_once_its_time_has_come(double minutesLeft, bool expired)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "expires_at", Value = LoginAt.AddMinutes(minutesLeft).ToString("O") }]);

        SessionRules.IsAccessTokenExpired(properties, LoginAt).ShouldBe(expired);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(20 * 60, true)]
    public void IAM_08_AC1_cookie_is_renewed_at_most_once_a_minute(int secondsSinceIssued, bool renew)
    {
        SessionRules.ShouldRenewCookie(TimeSpan.FromSeconds(secondsSinceIssued)).ShouldBe(renew);
    }

    [Fact]
    public void SE1_refreshed_tokens_replace_the_stored_ones()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "old-access" },
            new AuthenticationToken { Name = "refresh_token", Value = "old-refresh" },
            new AuthenticationToken { Name = "id_token", Value = "old-id" },
            new AuthenticationToken { Name = "expires_at", Value = LoginAt.ToString("O") },
        ]);

        SessionRules.StoreRefreshedTokens(properties, new RefreshedTokens("new-access", "new-refresh", null, LoginAt.AddMinutes(15)));

        properties.GetTokenValue("access_token").ShouldBe("new-access");
        properties.GetTokenValue("refresh_token").ShouldBe("new-refresh");
        properties.GetTokenValue("id_token").ShouldBe("old-id", "kept when Identity sends none");
        SessionRules.AccessTokenNeedsRefresh(properties, LoginAt.AddMinutes(12)).ShouldBeFalse();
        SessionRules.AccessTokenNeedsRefresh(properties, LoginAt.AddMinutes(13)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/home", true)]
    [InlineData("/", true)]
    [InlineData("/admin/users?page=2", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("home", false)]
    [InlineData("//evil.example/", false)]
    [InlineData("/\\evil.example/", false)]
    [InlineData("https://evil.example/", false)]
    [InlineData("/home\r\nX: y", false)]
    public void SE1_login_returns_only_to_a_page_of_the_gateway(string? url, bool local)
    {
        LocalUrl.IsLocal(url).ShouldBe(local);
        LocalUrl.OrRoot(url).ShouldBe(local ? url : "/");
    }

    [Theory]
    [InlineData("flowza-session=abc", "")]
    [InlineData("a=1; flowza-session=abc; b=2", "a=1; b=2")]
    [InlineData("flowza-session=chunks:2; flowza-sessionC1=x; flowza-sessionC2=y; c=3", "c=3")]
    [InlineData("flowza-sessionCx=keep; flowza-session-other=keep", "flowza-sessionCx=keep; flowza-session-other=keep")]
    [InlineData("a=1", "a=1")]
    public void SE1_session_cookie_is_never_passed_on(string header, string expected)
    {
        CookieHeader.Without(header, "flowza-session").ShouldBe(expected);
    }
}
