namespace ContactExtractor.Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UploadSession>   UploadSessions  => Set<UploadSession>();
    public DbSet<Contact>         Contacts        => Set<Contact>();
    public DbSet<Tag>             Tags            => Set<Tag>();
    public DbSet<DuplicateGroup>  DuplicateGroups => Set<DuplicateGroup>();
    public DbSet<AuditLogEntry>   AuditLog        => Set<AuditLogEntry>();
    public DbSet<WebhookConfig>   Webhooks        => Set<WebhookConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
