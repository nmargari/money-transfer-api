using Microsoft.EntityFrameworkCore;

namespace MoneyTransfer.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasMaxLength(64);
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.ApiKeyHash).HasMaxLength(64);
            e.HasIndex(c => c.ApiKeyHash).IsUnique(); 
        });
    
        b.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasMaxLength(64);
            e.Property(a => a.Currency).HasMaxLength(3);

            e.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("ck_accounts_balance_non_negative", "balance >= 0"));
        });

        b.Entity<Transfer>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Currency).HasMaxLength(3);
            e.Property(t => t.CreatedAt).HasDefaultValueSql("now()");

            e.HasOne<Account>()
                .WithMany()
                .HasForeignKey(t => t.DestinationAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(t => new { t.SourceAccountId, t.Id });
            e.HasIndex(t => new { t.DestinationAccountId, t.Id });

            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_transfers_amount_positive", "amount > 0");
                t.HasCheckConstraint("ck_transfers_distinct_accounts", "source_account_id <> destination_account_id");
            });
        });

        b.Entity<IdempotencyKey>(e =>
        {
            e.HasKey(k => new { k.CustomerId, k.Key });
            e.Property(k => k.CustomerId).HasMaxLength(64);
            e.Property(k => k.Key).HasMaxLength(255);
            e.Property(k => k.RequestHash).HasMaxLength(64);
            e.Property(k => k.CreatedAt).HasDefaultValueSql("now()");

            e.HasOne<Customer>().WithMany()
                                .HasForeignKey(k => k.CustomerId)
                                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}