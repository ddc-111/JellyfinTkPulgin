using Microsoft.EntityFrameworkCore;
using Jellyfin.Clips.Data.Entities;

namespace Jellyfin.Clips.Data;

public class ClipsDbContext : DbContext
{
    public ClipsDbContext()
    {
    }

    public ClipsDbContext(DbContextOptions<ClipsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Clip> Clips => Set<Clip>();
    public DbSet<UserInteraction> UserInteractions => Set<UserInteraction>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ProcessingState> ProcessingStates => Set<ProcessingState>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "jellyfin", "plugins", "clips", "clips.db");
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clip>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceItemId);
            entity.HasIndex(e => e.Genre);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.SceneScore);
            entity.HasIndex(e => e.IsMultimodalAnalyzed);
        });

        modelBuilder.Entity<UserInteraction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ClipId });
            entity.HasIndex(e => e.Timestamp);
            entity.HasOne(e => e.Clip)
                  .WithMany(c => c.Interactions)
                  .HasForeignKey(e => e.ClipId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);
        });

        modelBuilder.Entity<ProcessingState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceItemId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.UpdatedAt });
        });
    }
}
