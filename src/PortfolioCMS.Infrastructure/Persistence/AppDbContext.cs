using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Domain.Entities;

namespace PortfolioCMS.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PortfolioSection> Sections => Set<PortfolioSection>();
    public DbSet<ProjectCard> Projects => Set<ProjectCard>();
    public DbSet<ThemeSettings> Themes => Set<ThemeSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
