using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioCMS.Domain.Entities;

namespace PortfolioCMS.Infrastructure.Persistence.Configurations;

public class SectionConfiguration : IEntityTypeConfiguration<PortfolioSection>
{
    public void Configure(EntityTypeBuilder<PortfolioSection> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Content).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.SubTitle).HasMaxLength(300);
        builder.Property(x => x.BackgroundColor).HasMaxLength(7).HasDefaultValue("#ffffff");
        builder.Property(x => x.TextColor).HasMaxLength(7).HasDefaultValue("#111111");
        builder.Property(x => x.Type).IsRequired();
        builder.HasIndex(x => x.Type).IsUnique();  // one row per section type
    }
}

public class ProjectConfiguration : IEntityTypeConfiguration<ProjectCard>
{
    public void Configure(EntityTypeBuilder<ProjectCard> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.TechStack).HasMaxLength(500).IsRequired();
        builder.Property(x => x.GitHubUrl).HasMaxLength(500);
        builder.Property(x => x.LiveUrl).HasMaxLength(500);
        builder.Property(x => x.ImageUrl).HasMaxLength(1000);
    }
}

public class ThemeConfiguration : IEntityTypeConfiguration<ThemeSettings>
{
    public void Configure(EntityTypeBuilder<ThemeSettings> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PrimaryColor).HasMaxLength(7).HasDefaultValue("#6366f1");
        builder.Property(x => x.SecondaryColor).HasMaxLength(7).HasDefaultValue("#8b5cf6");
        builder.Property(x => x.AccentColor).HasMaxLength(7).HasDefaultValue("#06b6d4");
        builder.Property(x => x.BackgroundColor).HasMaxLength(7).HasDefaultValue("#0f172a");
        builder.Property(x => x.SurfaceColor).HasMaxLength(7).HasDefaultValue("#1e293b");
        builder.Property(x => x.TextColor).HasMaxLength(7).HasDefaultValue("#f8fafc");
        builder.Property(x => x.FontFamily).HasMaxLength(100).HasDefaultValue("Inter");
        builder.Property(x => x.HeadingFontFamily).HasMaxLength(100).HasDefaultValue("Inter");
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OldValue).HasColumnType("TEXT");
        builder.Property(x => x.NewValue).HasColumnType("TEXT");
        builder.Property(x => x.PerformedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.CreatedAt);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Username).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(20).HasDefaultValue("Viewer");
        builder.HasIndex(x => x.Username).IsUnique();
    }
}
