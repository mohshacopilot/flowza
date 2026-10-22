using Flowza.Common.Results;
using Flowza.Identity.Application.Users.LoadDemoUsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace Flowza.Cli.DemoData;

/// <summary>Identity's demo data: the demo users (PLT-07, C5), loaded through Identity's own use case and rules.</summary>
internal sealed class IdentityDemoData(IReadOnlyDictionary<string, string?>? settings) : IDemoDataSet
{
    private IHost? _host;

    public string Service => "identity";

    public async Task<bool> HasDataAsync(CancellationToken cancellationToken) =>
        await (await BusAsync(cancellationToken)).InvokeAsync<bool>(new IdentityHasDataQuery(), cancellationToken);

    public async Task<DemoDataResult> LoadAsync(CancellationToken cancellationToken)
    {
        var bus = await BusAsync(cancellationToken);
        var result = await bus.InvokeAsync<Result<int>>(new LoadDemoUsersCommand(DemoUsers.All, DemoUsers.Password), cancellationToken);
        return result.IsSuccess
            ? new DemoDataResult(true, $"{result.Value} demo users loaded ({string.Join(", ", DemoUsers.All.Select(u => u.Username))}).")
            : new DemoDataResult(false, result.Error.Message);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private async Task<IMessageBus> BusAsync(CancellationToken cancellationToken)
    {
        if (_host is null)
        {
            _host = CliHost.Build(settings);
            await _host.StartAsync(cancellationToken);
        }

        return _host.Services.GetRequiredService<IMessageBus>();
    }
}
