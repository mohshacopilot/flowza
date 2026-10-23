using Flowza.Common.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Flowza.Messaging;

/// <summary>
/// Hosted services that need the database to start (Wolverine's runtime checks its tables and registers its node),
/// run as one start-up step instead of at host start (T034, Decorator). The host then starts even when the database
/// is down; the step starts them once it answers and stops them, in reverse order, when the service stops.
/// <para>
/// Before the first hosted service is entered, a <paramref name="precondition"/> (e.g. "Wolverine's tables exist")
/// may fail and is retried. Once a hosted service's start has been entered, a failure is final
/// (<see cref="CanRetry"/> false): Wolverine marks itself started before the parts that can fail, so a second call
/// would do nothing and the service would look ready with dead messaging (phase 3–4 review, finding 1).
/// </para>
/// </summary>
internal sealed class DeferredHostedServices(
    string name,
    int order,
    IServiceProvider services,
    IReadOnlyList<ServiceDescriptor> descriptors,
    Func<IServiceProvider, CancellationToken, Task>? precondition) : IStartupStep
{
    private readonly Dictionary<ServiceDescriptor, IHostedService> _created = [];
    private readonly List<IHostedService> _entered = [];
    private bool _failedAfterEntering;

    public int Order { get; } = order;

    public string Name { get; } = name;

    public bool CanRetry => !_failedAfterEntering;

    /// <summary>
    /// Takes the hosted services registered by <paramref name="register"/> out of the host's start and returns them
    /// as one start-up step, started once <paramref name="precondition"/> passes.
    /// </summary>
    public static void Defer(
        IServiceCollection serviceCollection,
        string name,
        int order,
        Action register,
        Func<IServiceProvider, CancellationToken, Task>? precondition = null)
    {
        var before = serviceCollection.Where(d => d.ServiceType == typeof(IHostedService)).ToHashSet();
        register();
        var added = serviceCollection.Where(d => d.ServiceType == typeof(IHostedService) && !before.Contains(d)).ToList();
        foreach (var descriptor in added)
        {
            serviceCollection.Remove(descriptor);
        }

        serviceCollection.AddSingleton<IStartupStep>(provider => new DeferredHostedServices(name, order, provider, added, precondition));
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, StartupStepsDuringStart>());
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_failedAfterEntering)
        {
            throw new InvalidOperationException($"The start-up step '{Name}' failed part-way and cannot be run again; restart the service.");
        }

        if (_entered.Count == 0 && precondition is not null)
        {
            await precondition(services, cancellationToken);
        }

        foreach (var descriptor in descriptors)
        {
            var hosted = Create(descriptor);
            if (_entered.Contains(hosted))
            {
                continue;
            }

            _entered.Add(hosted);
            try
            {
                await hosted.StartAsync(cancellationToken);
            }
            catch
            {
                _failedAfterEntering = true;
                throw;
            }
        }
    }

    /// <summary>Stops every hosted service whose start was entered, also one that failed part-way, newest first.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        for (var i = _entered.Count - 1; i >= 0; i--)
        {
            await _entered[i].StopAsync(cancellationToken);
        }
    }

    /// <summary>One instance per registration, kept, so a retry starts the same object.</summary>
    private IHostedService Create(ServiceDescriptor descriptor)
    {
        if (!_created.TryGetValue(descriptor, out var hosted))
        {
            hosted = (IHostedService)(descriptor.ImplementationInstance
                ?? descriptor.ImplementationFactory?.Invoke(services)
                ?? ActivatorUtilities.GetServiceOrCreateInstance(services, descriptor.ImplementationType!));
            _created[descriptor] = hosted;
        }

        return hosted;
    }
}

/// <summary>
/// For hosts without the services' start-up runner (<see cref="IStartupStepRunner"/>): the flowza tool and test hosts.
/// Runs the start-up steps during start, in order, and stops them in reverse, so messaging starts as it did before T034.
/// </summary>
internal sealed class StartupStepsDuringStart(IServiceProvider services) : IHostedService
{
    private readonly List<IStartupStep> _ran = [];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (services.GetService<IStartupStepRunner>() is not null)
        {
            return; // the runner does it, waiting for the database if needed
        }

        foreach (var step in services.GetServices<IStartupStep>().OrderBy(s => s.Order))
        {
            _ran.Add(step); // before running: a step that fails part-way is stopped too
            await step.RunAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        for (var i = _ran.Count - 1; i >= 0; i--)
        {
            await _ran[i].StopAsync(cancellationToken);
        }
    }
}
