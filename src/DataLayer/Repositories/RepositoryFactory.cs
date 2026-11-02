using Fistix.TaskManager.Core.Abstractions.Repositories;

namespace Fistix.TaskManager.DataLayer.Repositories
{
  public class RepositoryFactory : IRepositoryFactory
  {
    private readonly WriteEfContext _write;
    private readonly ReadEfContext _read;
    private ITodoTaskRepository todoTaskRepository;
    private IUserProfileRepository userProfileRepository;

    public RepositoryFactory(WriteEfContext write, ReadEfContext read)
    {
      _write = write;
      _read = read;
    }

    public IUserProfileRepository UserProfileRepository
    {
      // Profile lookup is authz-sensitive → write DB (Phase 1 D10).
      get { return userProfileRepository ??= new UserProfileRepository(_write); }
    }

    public ITodoTaskRepository TodoTaskRepository
    {
      get { return todoTaskRepository ??= new TodoTaskRepository(_write, _read); }
    }
  }
}
