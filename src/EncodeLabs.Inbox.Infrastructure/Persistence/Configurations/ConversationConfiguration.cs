using EncodeLabs.Inbox.Domain.Contacts;
using EncodeLabs.Inbox.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EncodeLabs.Inbox.Infrastructure.Persistence.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.LastMessage).HasMaxLength(4096);
        builder.HasIndex(conversation => conversation.TelegramChatId)
            .IsUnique()
            .HasDatabaseName("ux_conversations_telegram_chat_id");
        builder.HasIndex(conversation => conversation.ContactId)
            .IsUnique()
            .HasDatabaseName("ux_conversations_contact_id");
        builder.HasOne<Contact>()
            .WithOne()
            .HasForeignKey<Conversation>(conversation => conversation.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
