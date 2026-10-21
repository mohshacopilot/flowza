// Flowza command-line tool. Commands arrive in tasks T019 (init-admin) and T028 (demo-data).
Console.WriteLine("Usage: flowza <command>");
Console.WriteLine("Commands: init-admin, demo-data");
return args.Length == 0 ? 0 : 1;