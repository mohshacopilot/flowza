using Flowza.Identity.Application.Users.LoadDemoUsers;

namespace Flowza.Cli.DemoData;

/// <summary>
/// Seed data: the demo users and their shared password (C5). Fictional people; the roles they will play in the two
/// reference apps (HR leave, loan approval) come with the org chart and the apps in features 002–005.
/// The password is <b>documented on purpose</b> (docs/setup/local-setup.md): demo data only ever goes into empty
/// databases, so it can never open a real installation.
/// </summary>
public static class DemoUsers
{
    public const string Password = "Flowza-Demo-2026";

    private const string PlatformAdmin = "PlatformAdmin";
    private const string AppDesigner = "AppDesigner";

    public static IReadOnlyList<DemoUser> All { get; } =
    [
        new("demo.admin", "Demo Admin", "demo.admin@demo.example", [PlatformAdmin]),
        new("demo.designer", "Dana Designer", "demo.designer@demo.example", [AppDesigner]),
        new("priya.sharma", "Priya Sharma", "priya.sharma@demo.example", []),
        new("rahul.verma", "Rahul Verma", "rahul.verma@demo.example", []),
        new("meena.iyer", "Meena Iyer", "meena.iyer@demo.example", []),
        new("arjun.nair", "Arjun Nair", "arjun.nair@demo.example", []),
        new("kavita.rao", "Kavita Rao", "kavita.rao@demo.example", []),
        new("vikram.singh", "Vikram Singh", "vikram.singh@demo.example", []),
    ];
}
