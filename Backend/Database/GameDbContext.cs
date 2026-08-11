using Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Database;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Item> Items => Set<Item>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>().HasKey(e => new { e.ProfileId, e.ItemId });
        modelBuilder.Entity<Skill>().HasKey(e => new { e.ProfileId, e.SkillId });
    }
}
