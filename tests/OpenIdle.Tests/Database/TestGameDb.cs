using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OpenIdle.Tests.Database;

internal sealed class TestGameDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestGameDb()
    {
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"OpenIdleTests-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        DbContextOptions<GameDbContext> options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(connectionString)
            .Options;
        using GameDbContext dbContext = new(options);
        dbContext.Database.EnsureCreated();
        Factory = new TestDbContextFactory(options);
    }

    public IDbContextFactory<GameDbContext> Factory { get; }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestDbContextFactory(DbContextOptions<GameDbContext> options) : IDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext()
        {
            return new GameDbContext(options);
        }

        public Task<GameDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
