namespace Fistix.TaskManager.Core.Config
{
  public class ConnectionStringConfig
  {
    /// <summary>Primary (write) database.</summary>
    public string MainDb { get; set; }

    /// <summary>Read replica connection. When null/empty, falls back to <see cref="MainDb"/>.</summary>
    public string MainDbRead { get; set; }

    public string ResolveReadConnectionString() =>
      string.IsNullOrWhiteSpace(MainDbRead) ? MainDb : MainDbRead;
  }
}
