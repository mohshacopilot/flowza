using System.Net;
using System.Net.Http.Json;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.IntegrationTests.Users;

/// <summary>
/// T035 / PLT-05-AC3: a duplicate email that reaches the database (two saves at the same moment) is logged by EF Core
/// with SQL Server's message, which quotes the email. The log files must still not contain it.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class UserLogPrivacyTests(IdentityApp app)
{
    private const string Email = "ravi.secret.dup@bank.example";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PLT_05_AC3_a_duplicate_email_from_the_database_never_reaches_the_log_files()
    {
        await app.ResetDataAsync();
        var folder = Path.Combine(Path.GetTempPath(), "flowza-log-scan", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("Flowza__Logging__Folder", folder);
        try
        {
            // A second instance of the service, writing log files; the database is already prepared by the shared one.
            await using (var logging = new IdentityApp())
            {
                var (_, admin) = await logging.UserWithSessionAsync("log.admin", SystemRole.PlatformAdmin);
                using var _ = admin;

                (await admin.Client.PostAsJsonAsync(new Uri("/api/v1/users", UriKind.Relative), new { username = "first.one", displayName = "First", email = Email }, Ct))
                    .StatusCode.ShouldBe(HttpStatusCode.Created);

                // The use case's own check answers a normal duplicate...
                var duplicate = await admin.Client.PostAsJsonAsync(new Uri("/api/v1/users", UriKind.Relative), new { username = "second.one", displayName = "Second", email = Email.ToUpperInvariant() }, Ct);
                duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

                // ...and a race past that check fails in the database, where EF Core logs SQL Server's error text.
                await Should.ThrowAsync<DbUpdateException>(() => logging.WithDbContextAsync(async context =>
                {
                    context.Users.Add(User.Create(Guid.NewGuid(), "third.one", "Third", Email, null, []).Value);
                    return await context.SaveChangesAsync(Ct);
                }));
            }

            var text = string.Join("\n", Directory.GetFiles(folder, "*.log").Select(ReadShared));
            text.ShouldContain("IX_Users_NormalizedEmail", customMessage: "the database error itself must still be logged");
            text.ShouldNotContain(Email, Case.Insensitive);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Flowza__Logging__Folder", null);
        }
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
