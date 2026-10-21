using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Platform.IntegrationTests.Messaging;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MessagingTestGroup
{
    public const string Name = "Messaging";
}

/// <summary>
/// PLT-04 on both on-premises transports (ADR-0005): events are saved in the same transaction as the change,
/// never sent for a cancelled change, wait while the receiver is down, and are handled once per EventId.
/// </summary>
[Collection(MessagingTestGroup.Name)]
public sealed class MessagingDeliveryTests : IAsyncLifetime
{
    private static readonly TimeSpan s_deliveryTimeout = TimeSpan.FromSeconds(60);
    private IHost? _sender;
    private IHost? _receiver;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await MessagingHosts.StopAsync(_sender);
        await MessagingHosts.StopAsync(_receiver);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("RabbitMq")]
    public async Task PLT_04_AC1_an_event_saved_with_its_change_reaches_the_subscriber(string transport)
    {
        _receiver = await MessagingHosts.StartReceiverAsync(transport, Ct);
        _sender = await MessagingHosts.StartSenderAsync(transport, Ct);
        var orderId = Guid.NewGuid();

        await SaveOrderAndPublishAsync(orderId, commit: true);

        await EventuallyAsync(() => Log.CountFor(orderId) == 1, $"the event for order {orderId} was not delivered over {transport}");
        (await OrderExistsAsync(orderId)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("RabbitMq")]
    public async Task PLT_04_AC1_an_event_from_a_cancelled_change_is_never_delivered(string transport)
    {
        _receiver = await MessagingHosts.StartReceiverAsync(transport, Ct);
        _sender = await MessagingHosts.StartSenderAsync(transport, Ct);
        var orderId = Guid.NewGuid();

        await SaveOrderAndPublishAsync(orderId, commit: false);
        await Task.Delay(TimeSpan.FromSeconds(8), Ct);

        Log.CountFor(orderId).ShouldBe(0);
        (await OrderExistsAsync(orderId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("RabbitMq")]
    public async Task PLT_04_AC1_an_event_waits_while_the_subscriber_is_down(string transport)
    {
        // The receiver's queue must exist before it goes down (first deployment does this).
        await MessagingHosts.StopAsync(await MessagingHosts.StartReceiverAsync(transport, Ct));
        _sender = await MessagingHosts.StartSenderAsync(transport, Ct, reset: false);
        var orderId = Guid.NewGuid();

        await SaveOrderAndPublishAsync(orderId, commit: true);
        await Task.Delay(TimeSpan.FromSeconds(3), Ct);
        _receiver = await MessagingHosts.StartReceiverAsync(transport, Ct, reset: false);

        await EventuallyAsync(() => Log.CountFor(orderId) == 1, $"the waiting event for order {orderId} was not delivered over {transport}");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("RabbitMq")]
    public async Task PLT_04_AC2_the_same_event_published_twice_is_handled_once(string transport)
    {
        _receiver = await MessagingHosts.StartReceiverAsync(transport, Ct);
        _sender = await MessagingHosts.StartSenderAsync(transport, Ct);
        var orderId = Guid.NewGuid();
        var duplicate = new TestOrderPlaced(orderId);

        await PublishAsync(duplicate);
        await EventuallyAsync(() => Log.CountFor(orderId) == 1, $"the first copy for order {orderId} was not delivered over {transport}");
        await PublishAsync(duplicate);
        await Task.Delay(TimeSpan.FromSeconds(8), Ct);

        Log.CountFor(orderId).ShouldBe(1);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("RabbitMq")]
    public async Task PLT_05_AC2_the_trace_reference_travels_with_the_event(string transport)
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("Flowza.Tests");

        _receiver = await MessagingHosts.StartReceiverAsync(transport, Ct);
        _sender = await MessagingHosts.StartSenderAsync(transport, Ct);
        var orderId = Guid.NewGuid();

        string traceId;
        using (var userAction = source.StartActivity("user action"))
        {
            traceId = userAction!.TraceId.ToHexString();
            await SaveOrderAndPublishAsync(orderId, commit: true);
        }

        await EventuallyAsync(() => Log.CountFor(orderId) == 1, $"the event for order {orderId} was not delivered over {transport}");
        Log.TraceIdFor(orderId).ShouldBe(traceId);
    }

    private ReceivedLog Log => _receiver!.Services.GetRequiredService<ReceivedLog>();

    private async Task SaveOrderAndPublishAsync(Guid orderId, bool commit)
    {
        await using var scope = _sender!.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<MessagingTestDbContext>>();
        outbox.DbContext.Orders.Add(new TestOrder { Id = orderId, Name = "test order" });
        await outbox.PublishAsync(new TestOrderPlaced(orderId));

        if (commit)
        {
            await outbox.SaveChangesAndFlushMessagesAsync(Ct);
        }

        // Without a commit the scope simply ends: the change and its event are both discarded.
    }

    private async Task PublishAsync(TestOrderPlaced message)
    {
        await using var scope = _sender!.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<MessagingTestDbContext>>();
        await outbox.PublishAsync(message);
        await outbox.SaveChangesAndFlushMessagesAsync(Ct);
    }

    private async Task<bool> OrderExistsAsync(Guid orderId)
    {
        await using var scope = _sender!.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        return await context.Orders.AnyAsync(o => o.Id == orderId, Ct);
    }

    private static async Task EventuallyAsync(Func<bool> condition, string failureMessage)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > s_deliveryTimeout)
            {
                throw new ShouldAssertException(failureMessage);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), Ct);
        }
    }
}
