using EncodeLabs.Inbox.Domain.Contacts;
using EncodeLabs.Inbox.Domain.Conversations;
using EncodeLabs.Inbox.Domain.Messages;
using Microsoft.EntityFrameworkCore;

namespace EncodeLabs.Inbox.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
