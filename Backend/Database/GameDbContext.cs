using Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Database;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<ProfileSetting> ProfileSettings => Set<ProfileSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>().HasKey(e => new { e.ProfileId, e.ItemId });
        modelBuilder.Entity<Item>().Property(e => e.ItemId).HasConversion<string>();
        modelBuilder.Entity<Skill>().HasKey(e => new { e.ProfileId, e.SkillId });
        modelBuilder.Entity<Skill>().Property(e => e.SkillId).HasConversion<string>();
        modelBuilder.Entity<Profile>().Property(e => e.ActivityId).HasConversion<string>();

        modelBuilder.Entity<UserSetting>().HasKey(e => new { e.UserId, e.Name });
        modelBuilder.Entity<UserSetting>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileSetting>().HasKey(e => new { e.ProfileId, e.Name });
        modelBuilder.Entity<ProfileSetting>()
            .HasOne<Profile>()
            .WithMany()
            .HasForeignKey(e => e.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
