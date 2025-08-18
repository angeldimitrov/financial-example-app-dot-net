using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Data;

/// <summary>
/// Entity Framework database context for the Finance application
/// Manages the connection to PostgreSQL and defines the data model
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    /// <summary>
    /// Financial periods that have been imported
    /// Used to track and prevent duplicate imports
    /// </summary>
    public DbSet<FinancialPeriod> FinancialPeriods { get; set; }
    
    /// <summary>
    /// Individual transaction lines from imported financial reports
    /// Each line represents a category/amount combination for a specific month
    /// </summary>
    public DbSet<TransactionLine> TransactionLines { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure FinancialPeriod entity
        modelBuilder.Entity<FinancialPeriod>(entity =>
        {
            // Create unique constraint on Year+Month to prevent duplicate periods
            entity.HasIndex(e => new { e.Year, e.Month })
                .IsUnique()
                .HasDatabaseName("IX_FinancialPeriod_Year_Month");
            
            // Configure relationship with TransactionLines
            entity.HasMany(e => e.TransactionLines)
                .WithOne(t => t.FinancialPeriod)
                .HasForeignKey(t => t.FinancialPeriodId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure TransactionLine entity
        modelBuilder.Entity<TransactionLine>(entity =>
        {
            // Index for efficient queries by period
            entity.HasIndex(e => e.FinancialPeriodId)
                .HasDatabaseName("IX_TransactionLine_FinancialPeriodId");
            
            // Index for efficient queries by year/month
            entity.HasIndex(e => new { e.Year, e.Month })
                .HasDatabaseName("IX_TransactionLine_Year_Month");
            
            // Index for filtering by transaction type
            entity.HasIndex(e => e.Type)
                .HasDatabaseName("IX_TransactionLine_Type");
        });
    }
}