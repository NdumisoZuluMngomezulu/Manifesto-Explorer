using ManifestoExplorer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ManifestoExplorer.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Manifesto> Manifestos => Set<Manifesto>();
    public DbSet<Policy> Policies => Set<Policy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Party>().HasIndex(p => p.Name).IsUnique();
        modelBuilder.Entity<Party>().HasData(
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "African National Congress", Abbreviation = "ANC", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Democratic Alliance", Abbreviation = "DA", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Economic Freedom Fighters", Abbreviation = "EFF", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = "Inkatha Freedom Party", Abbreviation = "IFP", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Name = "ActionSA", Abbreviation = "ActionSA", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Name = "Freedom Front Plus", Abbreviation = "FF Plus", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000007"), Name = "uMkhonto weSizwe Party", Abbreviation = "MK Party", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000008"), Name = "GOOD", Abbreviation = "GOOD", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000009"), Name = "Build One South Africa", Abbreviation = "BOSA", Description = "Political party in South Africa." },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000010"), Name = "African Christian Democratic Party", Abbreviation = "ACDP", Description = "Political party in South Africa." }
        );
        modelBuilder.Entity<Manifesto>().HasOne(m => m.Party).WithMany(p => p.Manifestos).HasForeignKey(m => m.PartyId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Manifesto>().HasIndex(m => m.ContentSha256);
        modelBuilder.Entity<Policy>().HasOne(p => p.Manifesto).WithMany(m => m.Policies).HasForeignKey(p => p.ManifestoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Policy>().HasIndex(p => p.Topic);
        modelBuilder.Entity<Policy>().HasIndex(p => p.VerificationStatus);
        modelBuilder.Entity<Policy>().HasIndex(p => p.Title);
    }
}
