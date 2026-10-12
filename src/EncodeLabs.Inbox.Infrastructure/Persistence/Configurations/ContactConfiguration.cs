using EncodeLabs.Inbox.Domain.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EncodeLabs.Inbox.Infrastructure.Persistence.Configurations;

internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");
        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Username).HasMaxLength(64);
        builder.Property(contact => contact.FirstName).HasMaxLength(128);
        builder.Property(contact => contact.LastName).HasMaxLength(128);
        builder.HasIndex(contact => contact.TelegramUserId)
            .IsUnique()
            .HasDatabaseName("ux_contacts_telegram_user_id");
    }
}
