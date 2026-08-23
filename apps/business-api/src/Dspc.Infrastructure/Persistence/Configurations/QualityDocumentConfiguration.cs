using Dspc.Domain.Common;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dspc.Infrastructure.Persistence.Configurations;

internal sealed class QualityDocumentConfiguration : IEntityTypeConfiguration<QualityDocument>
{
    public void Configure(EntityTypeBuilder<QualityDocument> b)
    {
        b.ToTable("quality_documents"); b.ConfigureVersioned(); b.Property(e => e.Type).AsEnumString(); b.EnumCheck<QualityDocument, DocumentType>("type"); b.Property(e => e.Status).AsEnumString(); b.EnumCheck<QualityDocument, DocumentStatus>("status");
        b.Property(e => e.DocumentNumber).HasMaxLength(100); b.Property(e => e.FileName).HasMaxLength(255); b.Property(e => e.ContentType).HasMaxLength(100); b.Property(e => e.Sha256).HasMaxLength(64); b.Property(e => e.StorageKey).HasMaxLength(300); b.Property(e => e.LotNumber).HasMaxLength(64); b.Property(e => e.HeatNumber).HasMaxLength(64);
        b.Property(e => e.UploadedBy).HasMaxLength(100); b.Property(e => e.VerifiedBy).HasMaxLength(100); b.Property(e => e.VerificationComment).HasMaxLength(1000); b.Property(e => e.AiSuggestionJson).AsJson(); b.HasIndex(e => e.DocumentNumber); b.HasOne<Supplier>().WithMany().HasForeignKey(e => e.SupplierId);
    }
}
