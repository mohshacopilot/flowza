using System.Security.Claims;
using Flowza.Contracts.Identity.Events.V1;
using Flowza.ServiceDefaults.Security;
using Microsoft.AspNetCore.Http;

namespace Flowza.BuildingBlocks.UnitTests.ServiceDefaults;

/// <summary>T018 / PA4: the deny list that makes deactivation take effect at once in every service (IAM-04-AC3).</summary>
public sealed class UserDenyListTests
{
    private static readonly DateTimeOffset s_t0 = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IAM_04_AC3_a_deactivated_user_is_denied_and_an_activated_one_is_allowed_again()
    {
        var list = new UserDenyList();
        var user = Guid.NewGuid();

        list.IsDenied(user).ShouldBeFalse();
        UserDenyListHandler.Handle(new UserDeactivated(user) { OccurredAt = s_t0 }, list);
        list.IsDenied(user).ShouldBeTrue();
        UserDenyListHandler.Handle(new UserActivated(user) { OccurredAt = s_t0.AddMinutes(1) }, list);
        list.IsDenied(user).ShouldBeFalse();
    }

    [Fact]
    public void Events_that_arrive_out_of_order_still_leave_the_latest_state()
    {
        var list = new UserDenyList();
        var user = Guid.NewGuid();

        // Activated at 9:05 arrives first, the older deactivation of 9:00 arrives later.
        list.Allow(user, s_t0.AddMinutes(5));
        list.Deny(user, s_t0);

        list.IsDenied(user).ShouldBeFalse();
    }

    [Fact]
    public void The_same_event_twice_has_the_same_effect_as_once()
    {
        var list = new UserDenyList();
        var deactivated = new UserDeactivated(Guid.NewGuid()) { OccurredAt = s_t0 };

        UserDenyListHandler.Handle(deactivated, list);
        UserDenyListHandler.Handle(deactivated, list);

        list.IsDenied(deactivated.UserId).ShouldBeTrue();
        list.DeniedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Many_threads_can_update_and_read_at_once()
    {
        var list = new UserDenyList();
        var users = Enumerable.Range(0, 1_000).Select(_ => Guid.NewGuid()).ToList();

        await Task.WhenAll(users.Select(user => Task.Run(() =>
        {
            list.Deny(user, s_t0);
            list.IsDenied(user).ShouldBeTrue();
        })));

        list.DeniedCount.ShouldBe(users.Count);
    }

    [Fact]
    public void T036_the_current_user_is_the_subject_of_the_requests_token()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], authenticationType: "test")),
        };

        new HttpCurrentUser(new HttpContextAccessor { HttpContext = context }).UserId.ShouldBe(userId);
    }

    [Fact]
    public void T036_outside_a_request_or_for_a_service_token_it_is_the_system()
    {
        new HttpCurrentUser(new HttpContextAccessor()).UserId.ShouldBeNull();

        var serviceToken = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "flowza-runtime")], authenticationType: "test")),
        };
        new HttpCurrentUser(new HttpContextAccessor { HttpContext = serviceToken }).UserId.ShouldBeNull();

        var anonymous = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())])) };
        new HttpCurrentUser(new HttpContextAccessor { HttpContext = anonymous }).UserId.ShouldBeNull();
    }
}
