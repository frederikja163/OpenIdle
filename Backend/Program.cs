using Backend;

var app = AppHost.CreateApp(args);
await AppHost.MigrateDatabaseAsync(app.Services);
app.Run();
