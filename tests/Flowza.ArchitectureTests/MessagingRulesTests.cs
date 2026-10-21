using System.Reflection;
using Flowza.Contracts;
using Wolverine.Attributes;

namespace Flowza.ArchitectureTests;

/// <summary>Rules for message handlers (constitution 3.4; tech discussion T4, MS3).</summary>
public sealed class MessagingRulesTests
{
    private static readonly string[] s_handlerMethodNames = ["Handle", "HandleAsync", "Consume", "ConsumeAsync"];

    [Fact]
    public void Constitution_3_4_every_integration_event_handler_is_deduplicated_by_event_id()
    {
        var missing =
            from assembly in SourceAssemblies()
            from type in assembly.GetTypes()
            from method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            where s_handlerMethodNames.Contains(method.Name)
            let message = method.GetParameters().FirstOrDefault()?.ParameterType
            where message is not null && typeof(IntegrationEvent).IsAssignableFrom(message)
            where method.GetCustomAttribute<DeduplicatedAttribute>() is null && type.GetCustomAttribute<DeduplicatedAttribute>() is null
            select $"{type.FullName}.{method.Name}({message!.Name})";

        missing.ShouldBeEmpty("handlers of integration events must carry [Deduplicated] so a duplicate EventId is handled once");
    }

    private static readonly string[] s_sourceAssemblies =
    [
        "Flowza.Identity.Application", "Flowza.Identity.Infrastructure", "Flowza.Identity.Api",
        "Flowza.Design.Application", "Flowza.Design.Infrastructure", "Flowza.Design.Api",
        "Flowza.Runtime.Application", "Flowza.Runtime.Infrastructure", "Flowza.Runtime.Api",
        "Flowza.Worker", "Flowza.Gateway", "Flowza.ServiceDefaults", "Flowza.Messaging",
    ];

    private static IEnumerable<Assembly> SourceAssemblies() =>
        s_sourceAssemblies.Select(name => Assembly.Load(new AssemblyName(name)));
}
