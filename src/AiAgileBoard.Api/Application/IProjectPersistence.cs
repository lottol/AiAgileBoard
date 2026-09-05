namespace AiAgileBoard.Application;

public interface IProjectPersistence
{
    Task<T> MutateAsync<T>(Func<Task<T>> mutation, CancellationToken cancellationToken);
}

public sealed class DatabasePersistence : IProjectPersistence
{
    public Task<T> MutateAsync<T>(Func<Task<T>> mutation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        return mutation();
    }
}

public sealed class ProjectSavePendingException() : InvalidOperationException(
    "Project saving is unavailable. Retry saving the project before making more changes.");
