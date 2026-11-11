using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RequestsPortal.Core.Entities;

namespace RequestsPortal.Core.Persistence.Configurations;

public sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("Requests", table =>
        {
            table.HasCheckConstraint(
                "CK_Requests_Status",
                "Status IN ('Pending', 'Processing', 'Completed', 'Failed')");
        });

        builder.HasKey(request => request.Id);

        builder.Property(request => request.ExternalReference)
            .IsRequired()
            .HasMaxLength(128)
            .UseCollation("BINARY");

        builder.Property(request => request.CustomerId)
            .IsRequired()
            .HasMaxLength(100)
            .UseCollation("BINARY");

        builder.Property(request => request.ServiceCode)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(request => request.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(request => new
        {
            request.CustomerId,
            request.ExternalReference
        }).IsUnique();

        builder.HasIndex(request => request.Status);

        // SQLite does not persist DateTime.Kind; restore the UTC contract on reads.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        builder.Property(request => request.CreatedAtUtc)
            .HasConversion(utcConverter);

        builder.Property(request => request.UpdatedAtUtc)
            .HasConversion(utcConverter);
    }
}
