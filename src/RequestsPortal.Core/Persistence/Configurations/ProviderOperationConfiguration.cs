using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RequestsPortal.Core.Entities;

namespace RequestsPortal.Core.Persistence.Configurations;

public sealed class ProviderOperationConfiguration : IEntityTypeConfiguration<ProviderOperation>
{
    public void Configure(EntityTypeBuilder<ProviderOperation> builder)
    {
        builder.ToTable("ProviderOperations", table =>
        {
            table.HasCheckConstraint("CK_ProviderOperations_InvocationCount", "InvocationCount >= 0");
            table.HasCheckConstraint(
                "CK_ProviderOperations_Receipt",
                "(ReceiptId IS NULL AND CompletedAt IS NULL) OR (ReceiptId IS NOT NULL AND CompletedAt IS NOT NULL)");
        });

        builder.HasKey(operation => operation.RequestId);
        builder.HasIndex(operation => operation.ReceiptId).IsUnique();

        var utcConverter = new ValueConverter<DateTime?, DateTime?>(
            value => value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

        builder.Property(operation => operation.CompletedAt)
            .HasConversion(utcConverter);
    }
}
