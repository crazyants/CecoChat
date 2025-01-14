using CecoChat.Contracts.User;

namespace CecoChat.Data.User.Profiles;

public interface IProfileQueryRepo
{
    Task<FullProfileResult> GetFullProfile(string userName, bool includePassword);

    Task<ProfilePublic?> GetPublicProfile(long requestedUserId, long userId);

    Task<IEnumerable<ProfilePublic>> GetPublicProfiles(IList<long> requestedUserIds, long userId);
}

public readonly struct FullProfileResult
{
    public bool Success { get; init; }
    public ProfileFull? Profile { get; init; }
    public string? Password { get; init; }
}
