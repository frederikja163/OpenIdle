using System;
using Backend;

Log.Info("Creating app");
var app = AppHost.CreateApp(args);
Log.Info("Migrating database");
await AppHost.MigrateDatabaseAsync(app.Services);

bool firstPress = true;
Console.CancelKeyPress += (_, eventArgs) =>
{
    if (firstPress)
    {
        firstPress = false;
        eventArgs.Cancel = true;
        Log.Info("Shutting down gracefully... (Press Ctrl+C again to force quit)");
        app.Lifetime.StopApplication();
    }
    else
    {
        eventArgs.Cancel = false;
        Log.Info("Force quitting");
    }
};

Log.Info("Running");
await app.RunAsync();
