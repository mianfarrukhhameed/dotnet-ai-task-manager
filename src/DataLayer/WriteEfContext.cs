using Microsoft.EntityFrameworkCore;

namespace Fistix.TaskManager.DataLayer
{
  /// <summary>Write-side EF context (primary). Owns migrations and SaveChanges.</summary>
  public class WriteEfContext : TaskManagerDbContext
  {
    public WriteEfContext(DbContextOptions<WriteEfContext> options) : base(options)
    {
    }
  }
}
