using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Database;

internal sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
}
