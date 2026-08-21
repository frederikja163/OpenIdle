using Backend;

Log.Info("Creating app");
var app = AppHost.CreateApp(args);
Log.Info("Migrating database");
await AppHost.MigrateDatabaseAsync(app.Services);
Log.Info("Running");
app.Run();
