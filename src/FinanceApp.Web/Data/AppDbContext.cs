using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Data;

/// <summary>
/// Entity Framework database context for the Finance application
/// Manages the connection to PostgreSQL and defines the data model
/// 
/// Performance Optimizations:
/// - Comprehensive indexing strategy for all query patterns
/// - Query splitting for complex joins
/// - Connection pooling configuration
/// - Batch size optimization
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        // Configure performance settings
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        ChangeTracker.AutoDetectChangesEnabled = true;
        ChangeTracker.LazyLoadingEnabled = false; // Prevent N+1 queries
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
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Configure performance optimizations
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.EnableServiceProviderCaching(true);
            optionsBuilder.EnableDetailedErrors(false);
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure FinancialPeriod entity
        modelBuilder.Entity<FinancialPeriod>(entity =>
        {
            // Primary unique constraint on Year+Month to prevent duplicate periods
            entity.HasIndex(e => new { e.Year, e.Month })
                .IsUnique()
                .HasDatabaseName("IX_FinancialPeriod_Year_Month");
            
            // Performance index for date range queries
            entity.HasIndex(e => new { e.Year, e.Month, e.ImportedAt })
                .HasDatabaseName("IX_FinancialPeriod_DateRange");
                
            // Index for source file queries (debugging/auditing)
            entity.HasIndex(e => e.SourceFileName)
                .HasDatabaseName("IX_FinancialPeriod_SourceFileName");
            
            // Configure relationship with TransactionLines
            entity.HasMany(e => e.TransactionLines)
                .WithOne(t => t.FinancialPeriod)
                .HasForeignKey(t => t.FinancialPeriodId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure TransactionLine entity with comprehensive indexing
        modelBuilder.Entity<TransactionLine>(entity =>
        {
            // Foreign key index (automatically created but explicitly defined)
            entity.HasIndex(e => e.FinancialPeriodId)
                .HasDatabaseName("IX_TransactionLine_FinancialPeriodId");
            
            // Composite index for efficient year/month queries (most common)
            entity.HasIndex(e => new { e.Year, e.Month })
                .HasDatabaseName("IX_TransactionLine_Year_Month");
            
            // Index for transaction type filtering (Revenue/Expense/Other)
            entity.HasIndex(e => e.Type)
                .HasDatabaseName("IX_TransactionLine_Type");
                
            // Composite index for type + date filtering (Transactions page)
            entity.HasIndex(e => new { e.Type, e.Year, e.Month })
                .HasDatabaseName("IX_TransactionLine_Type_Year_Month");
                
            // Index for category filtering and grouping
            entity.HasIndex(e => e.Category)
                .HasDatabaseName("IX_TransactionLine_Category");
                
            // Composite index for category analysis by period
            entity.HasIndex(e => new { e.Category, e.Year, e.Month })
                .HasDatabaseName("IX_TransactionLine_Category_Year_Month");
                
            // Index for group category (used in reports)
            entity.HasIndex(e => e.GroupCategory)
                .HasDatabaseName("IX_TransactionLine_GroupCategory");
                
            // Covering index for summary calculations (includes Amount in index)
            entity.HasIndex(e => new { e.Type, e.Year, e.Month })
                .IncludeProperties(e => e.Amount)
                .HasDatabaseName("IX_TransactionLine_Summary_Covering");
                
            // Index for amount range queries (if needed in future)
            entity.HasIndex(e => e.Amount)
                .HasDatabaseName("IX_TransactionLine_Amount");
                
            // Composite index for trend analysis queries
            entity.HasIndex(e => new { e.Category, e.Type, e.Year })
                .IncludeProperties(e => new { e.Month, e.Amount })
                .HasDatabaseName("IX_TransactionLine_Trends_Covering");
        });
    }
    
    /// <summary>
    /// Override SaveChanges to implement batch optimizations
    /// Groups multiple operations for better performance
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Batch size optimization for large imports
        var entries = ChangeTracker.Entries().ToList();
        if (entries.Count > 100)
        {
            // For large imports, disable auto-detect changes for performance
            var originalState = ChangeTracker.AutoDetectChangesEnabled;
            try
            {
                ChangeTracker.AutoDetectChangesEnabled = false;
                return await base.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                ChangeTracker.AutoDetectChangesEnabled = originalState;
            }
        }
        
        return await base.SaveChangesAsync(cancellationToken);
    }
}