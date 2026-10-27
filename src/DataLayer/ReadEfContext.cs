using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Fistix.TaskManager.DataLayer
{
  /// <summary>Read-side EF context (replica or MainDb fallback). No tracking; SaveChanges forbidden.</summary>
  public class ReadEfContext : TaskManagerDbContext
  {
    public ReadEfContext(DbContextOptions<ReadEfContext> options) : base(options)
    {
      ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public override int SaveChanges() =>
      throw new InvalidOperationException("ReadEfContext is read-only. Use WriteEfContext for writes.");

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
      throw new InvalidOperationException("ReadEfContext is read-only. Use WriteEfContext for writes.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException("ReadEfContext is read-only. Use WriteEfContext for writes.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException("ReadEfContext is read-only. Use WriteEfContext for writes.");
  }
}
