using CecoChat.Contracts.User;

namespace CecoChat.Data.User.Connections;

public interface IConnectionCommandRepo
{
    Task<AddConnectionResult> AddConnection(long userId, Connection connection);

    Task<UpdateConnectionResult> UpdateConnection(long userId, Connection connection);

    Task<RemoveConnectionResult> RemoveConnection(long userId, Connection connection);
}

public readonly struct AddConnectionResult
{
    public bool Success { get; init; }
    public Guid Version { get; init; }
    public bool MissingUser { get; init; }
    public bool AlreadyExists { get; init; }
}

public readonly struct UpdateConnectionResult
{
    public bool Success { get; init; }
    public Guid NewVersion { get; init; }
    public bool ConcurrentlyUpdated { get; init; }
}

public readonly struct RemoveConnectionResult
{
    public bool Success { get; init; }
    public bool ConcurrentlyUpdated { get; init; }
}
