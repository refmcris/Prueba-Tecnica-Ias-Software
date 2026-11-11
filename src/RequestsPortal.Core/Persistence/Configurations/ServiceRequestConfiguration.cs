using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RequestsPortal.Core.Entities;
using RequestsPortal.Core.Validation;

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
            .HasMaxLength(RequestFieldLimits.ExternalReference)
            .UseCollation("BINARY");

        builder.Property(request => request.CustomerId)
            .IsRequired()
            .HasMaxLength(RequestFieldLimits.CustomerId)
            .UseCollation("BINARY");

        builder.Property(request => request.ServiceCode)
            .IsRequired()
            .HasMaxLength(RequestFieldLimits.ServiceCode);

        builder.Property(request => request.Description)
            .IsRequired()
            .HasMaxLength(RequestFieldLimits.Description);

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(request => new
        {
            request.CustomerId,
            request.ExternalReference
        }).IsUnique();

        builder.HasIndex(request => request.Status);

        // SQLite no conserva DateTime.Kind; al leer recuperamos UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        builder.Property(request => request.CreatedAt)
            .HasConversion(utcConverter);

        builder.Property(request => request.UpdatedAt)
            .HasConversion(utcConverter);
    }
}
