using EncodeLabs.Inbox.Domain.Conversations;
using EncodeLabs.Inbox.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EncodeLabs.Inbox.Infrastructure.Persistence.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Direction).HasConversion<string>().HasMaxLength(16);
        builder.Property(message => message.Text).HasMaxLength(4096);
        builder.HasIndex(message => message.TelegramUpdateId)
            .IsUnique()
            .HasDatabaseName("ux_messages_telegram_update_id");
        builder.HasIndex(message => new { message.ConversationId, message.TelegramMessageId })
            .IsUnique()
            .HasDatabaseName("ux_messages_conversation_telegram_message_id");
        builder.HasIndex(message => new { message.ConversationId, message.SentAt })
            .HasDatabaseName("ix_messages_conversation_sent_at");
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
