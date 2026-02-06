using Microsoft.EntityFrameworkCore;
using CreditAI.API.Domain.Entities;

namespace CreditAI.API.Infrastructure.Data;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options) {}

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BehaviorEmbedding).HasColumnType("VARBINARY(MAX)");
            entity.Property(e => e.PublicId)
                  .HasDefaultValueSql("newsequentialid()")
                  .ValueGeneratedOnAdd();
        });
    }
}
