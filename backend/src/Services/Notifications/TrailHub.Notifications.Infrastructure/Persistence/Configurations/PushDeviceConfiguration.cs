using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Notifications.Domain.Devices;

namespace TrailHub.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> d)
    {
        d.ToTable("PushDevices");
        d.HasKey(x => x.Id);
        d.Property(x => x.Id).ValueGeneratedNever();
        d.Property(x => x.Token).HasMaxLength(PushDevice.MaxTokenLength).IsRequired();
        d.HasIndex(x => x.Token).IsUnique();
        d.Property(x => x.UserId).HasMaxLength(128).IsRequired();
        d.HasIndex(x => x.UserId);
        d.Property(x => x.Platform).HasConversion<string>().HasMaxLength(20);
    }
}
