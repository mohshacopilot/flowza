using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Application.Users;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.Domain.Users.Passwords;
using Flowza.Identity.Infrastructure.AspNetIdentity;
using Flowza.Identity.Infrastructure.Passwords;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Messaging;
using Flowza.Persistence;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Flowza.Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    /// <summary>Name of the connection string (set by the AppHost, PLT-03).</summary>
    public const string ConnectionStringName = "identity-db";

    /// <summary>Data Protection application name: keys made by one instance are readable by the others.</summary>
    public const string DataProtectionApplicationName = "Flowza.Identity";

    /// <summary>Registers the Identity database, its readiness check and messaging with the outbox in this database (PLT-03, PLT-04, PLT-05).</summary>
    public static TBuilder AddIdentityInfrastructure<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing. Start the services through the AppHost.");

        builder.Services.AddFlowzaDbContext<IdentityServiceDbContext>(connectionString);
        builder.Services.AddHealthChecks().AddSqlServer(connectionString, name: "database");
        // Wolverine is also the in-process mediator (T2, S1): it finds the use-case handlers of the application layer.
        // Their ports are resolved from one DI scope per use case (service location), so repository, credentials and
        // unit of work share one DbContext and therefore one transaction.
        builder.AddFlowzaMessaging(
            "identity",
            connectionString,
            MessageRoutes.Default,
            options =>
            {
                options.Discovery.IncludeAssembly(typeof(Application.AssemblyMarker).Assembly);
                options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
            });

        builder.Services.AddDataProtection().SetApplicationName(DataProtectionApplicationName);
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(provider =>
            new ConfigureOptions<KeyManagementOptions>(options =>
                options.XmlRepository = new DatabaseXmlRepository(provider.GetRequiredService<IServiceScopeFactory>())));

        builder.Services.AddUserAccounts();
        return builder;
    }

    /// <summary>Users, passwords and lockout: ports of the application layer and ASP.NET Core Identity (IAM-06, IAM-07).</summary>
    private static void AddUserAccounts(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserCredentials, UserCredentials>();
        services.AddScoped<PasswordLogin>();
        services.AddScoped<LoginSessionRenewer>();
        services.TryAddScoped<ISessionRevoker, NoSessionRevoker>();
        services.AddSingleton<ICommonPasswordList, EmbeddedCommonPasswordList>();
        services.AddSingleton<PasswordPolicy>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();

        services.AddIdentityCore<User>(options =>
            {
                // The domain PasswordPolicy decides (PasswordPolicyValidator below); no character-class rules (K2).
                options.Password.RequiredLength = PasswordPolicy.DefaultMinimumLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                // K3: 5 wrong passwords in a row lock the account for 15 minutes.
                options.Lockout.MaxFailedAccessAttempts = LockoutAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddUserStore<UserStore>()
            .AddSignInManager();

        // Usernames and emails are checked by the domain (C3, C4); passwords by the domain policy only.
        services.RemoveAll<IUserValidator<User>>();
        services.RemoveAll<IPasswordValidator<User>>();
        services.AddScoped<IPasswordValidator<User>, PasswordPolicyValidator>();
    }

    public const int LockoutAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private sealed class NoSessionRevoker : ISessionRevoker
    {
        public Task RevokeAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
