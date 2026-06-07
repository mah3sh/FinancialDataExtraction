using DocPipeline.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DocPipeline.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Document>(e =>
        {
            e.HasKey(d => d.Id);

            e.Property(d => d.FileName).IsRequired().HasMaxLength(500);
            e.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
            e.Property(d => d.StoragePath).IsRequired().HasMaxLength(1000);
            e.Property(d => d.UploadedByUserId).IsRequired().HasMaxLength(450);
            e.Property(d => d.ExtractedDataJson).HasColumnType("nvarchar(max)");
            e.Property(d => d.ErrorMessage).HasMaxLength(2000);
            e.Property(d => d.Status).IsRequired();

            // Optimistic concurrency via RowVersion
            e.Property(d => d.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            // Indexes for common query patterns
            e.HasIndex(d => d.UploadedByUserId).HasDatabaseName("IX_Documents_UploadedByUserId");
            e.HasIndex(d => d.Status).HasDatabaseName("IX_Documents_Status");
            e.HasIndex(d => d.UploadedAt).HasDatabaseName("IX_Documents_UploadedAt");
        });
    }
}
