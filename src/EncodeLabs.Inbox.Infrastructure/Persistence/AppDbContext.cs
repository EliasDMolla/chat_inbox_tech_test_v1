using Microsoft.EntityFrameworkCore;

namespace EncodeLabs.Inbox.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
