using Flowza.Contracts;
using Flowza.Messaging;
using Microsoft.Extensions.Configuration;

namespace Flowza.BuildingBlocks.UnitTests.Messaging;

public sealed class MessagingConfigurationTests
{
    private sealed record SampleCreated : IntegrationEvent;

    private sealed record SampleClosed : IntegrationEvent;

    [Theory]
    [InlineData(null, MessageTransport.SqlServer)]
    [InlineData("", MessageTransport.SqlServer)]
    [InlineData("SqlServer", MessageTransport.SqlServer)]
    [InlineData("sqlserver", MessageTransport.SqlServer)]
    [InlineData("AzureServiceBus", MessageTransport.AzureServiceBus)]
    public void ADR_0005_transport_comes_from_configuration_and_defaults_to_SqlServer(string? value, MessageTransport expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [FlowzaMessagingExtensions.TransportKey] = value })
            .Build();

        FlowzaMessagingExtensions.ReadTransport(configuration).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Kafka")]
    [InlineData("RabbitMq")] // removed 2026-10-22 (ADR-0005 amendment 3)
    [InlineData("5")]
    [InlineData("-1")]
    public void ADR_0005_an_unknown_transport_is_refused_with_a_clear_message(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [FlowzaMessagingExtensions.TransportKey] = value })
            .Build();

        Should.Throw<InvalidOperationException>(() => FlowzaMessagingExtensions.ReadTransport(configuration))
            .Message.ShouldContain("SqlServer (on-premises) or AzureServiceBus (cloud)");
    }

    [Fact]
    public void T4_MS5_each_subscriber_gets_its_own_queue()
    {
        var routes = new MessageRoutes()
            .Route<SampleCreated>("Runtime", "worker")
            .Route<SampleClosed>("runtime");

        routes.Subscribers[typeof(SampleCreated)].ShouldBe(["runtime", "worker"], ignoreOrder: true);
        routes.TypesFor("Runtime").ShouldBe([typeof(SampleCreated), typeof(SampleClosed)], ignoreOrder: true);
        routes.TypesFor("worker").ShouldBe([typeof(SampleCreated)]);
        routes.TypesFor("design").ShouldBeEmpty();
    }

    [Fact]
    public void T4_MS5_routing_the_same_subscriber_twice_keeps_one_route()
    {
        var routes = new MessageRoutes().Route<SampleCreated>("worker").Route<SampleCreated>("Worker");

        routes.Subscribers[typeof(SampleCreated)].Count.ShouldBe(1);
    }

    [Fact]
    public void T4_MS5_topic_names_are_the_full_type_name_in_lower_case()
    {
        MessageRoutes.TopicName(typeof(SampleCreated)).ShouldBe(typeof(SampleCreated).FullName!.ToLowerInvariant().Replace('+', '.'));
        MessageRoutes.QueueName(" Runtime ").ShouldBe("runtime");
    }

    private sealed class Outer
    {
        public sealed record Nested : IntegrationEvent;
    }

    [Fact]
    public void T4_MS5_topic_names_of_nested_and_generic_types_contain_only_safe_characters()
    {
        MessageRoutes.TopicName(typeof(Outer.Nested)).ShouldMatch("^[a-z0-9._-]+$");
        MessageRoutes.TopicName(typeof(List<SampleCreated>)).ShouldMatch("^[a-z0-9._-]+$");
    }

    [Fact]
    public void T4_MS2_integration_events_carry_the_standard_fields()
    {
        var first = new SampleCreated { OccurredAt = DateTimeOffset.UnixEpoch, ActorId = Guid.Empty, CorrelationId = "4bf92f35" };
        var second = new SampleCreated();

        first.EventId.ShouldNotBe(Guid.Empty);
        first.EventId.ShouldNotBe(second.EventId);
        first.CorrelationId.ShouldBe("4bf92f35");
    }
}
