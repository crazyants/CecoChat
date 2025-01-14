using AutoMapper;
using CecoChat.Client.Chats;
using CecoChat.Client.User;
using CecoChat.Contracts.Bff.Chats;
using CecoChat.Contracts.Bff.Connections;
using CecoChat.Contracts.Bff.Profiles;
using CecoChat.Contracts.Bff.Screens;
using CecoChat.Server.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CecoChat.Server.Bff.Endpoints.Screens;

[ApiController]
[Route("api/screens/allChats")]
[ApiExplorerSettings(GroupName = "Screens")]
public class AllChatsScreenController : ControllerBase
{
    private readonly ILogger _logger;
    private readonly IMapper _mapper;
    private readonly IContractMapper _contractMapper;
    private readonly IChatsClient _chatsClient;
    private readonly IConnectionClient _connectionClient;
    private readonly IProfileClient _profileClient;

    public AllChatsScreenController(
        ILogger<AllChatsScreenController> logger,
        IMapper mapper,
        IContractMapper contractMapper,
        IChatsClient chatsClient,
        IConnectionClient connectionClient,
        IProfileClient profileClient)
    {
        _logger = logger;
        _mapper = mapper;
        _contractMapper = contractMapper;
        _chatsClient = chatsClient;
        _connectionClient = connectionClient;
        _profileClient = profileClient;
    }

    [Authorize(Policy = "user")]
    [HttpGet(Name = "GetAllChatsScreen")]
    [ProducesResponseType(typeof(GetAllChatsScreenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllChatsScreen([FromQuery][BindRequired] GetAllChatsScreenRequest request, CancellationToken ct)
    {
        if (!HttpContext.TryGetUserClaimsAndAccessToken(_logger, out UserClaims? userClaims, out string? accessToken))
        {
            return Unauthorized();
        }

        Task<IReadOnlyCollection<Contracts.Chats.ChatState>> chatsTask = _chatsClient.GetUserChats(userClaims.UserId, request.ChatsNewerThan, accessToken, ct);
        Task<IReadOnlyCollection<Contracts.User.Connection>> connectionsTask = _connectionClient.GetConnections(userClaims.UserId, accessToken, ct);

        await Task.WhenAll(chatsTask, connectionsTask);

        IReadOnlyCollection<Contracts.Chats.ChatState> serviceChats = chatsTask.Result;
        IReadOnlyCollection<Contracts.User.Connection> serviceConnections = connectionsTask.Result;

        ProfilePublic[] profiles = await GetProfiles(request.IncludeProfiles, serviceChats, serviceConnections, userClaims, accessToken, ct);
        ChatState[] chats = serviceChats.Select(chat => _contractMapper.MapChat(chat)).ToArray();
        Connection[] connections = _mapper.Map<Connection[]>(serviceConnections);

        _logger.LogTrace("Responding with {ChatCount} chats newer than {NewerThan}, {ConnectionCount} connections and {ProfileCount} profiles for all-chats-screen requested by user {UserId}",
            chats.Length, request.ChatsNewerThan, connections.Length, profiles.Length, userClaims.UserId);
        return Ok(new GetAllChatsScreenResponse
        {
            Chats = chats,
            Connections = connections,
            Profiles = profiles
        });
    }

    private async Task<ProfilePublic[]> GetProfiles(
        bool includeProfiles,
        IReadOnlyCollection<Contracts.Chats.ChatState> chats,
        IReadOnlyCollection<Contracts.User.Connection> connections,
        UserClaims userClaims,
        string accessToken,
        CancellationToken ct)
    {
        if (!includeProfiles || (chats.Count == 0 && connections.Count == 0))
        {
            return Array.Empty<ProfilePublic>();
        }

        long[] userIds = chats.Select(chat => chat.OtherUserId)
            .Union(connections.Select(conn => conn.ConnectionId))
            .Distinct()
            .ToArray();
        IReadOnlyCollection<Contracts.User.ProfilePublic> serviceProfiles = await _profileClient.GetPublicProfiles(userClaims.UserId, userIds, accessToken, ct);
        ProfilePublic[] profiles = _mapper.Map<ProfilePublic[]>(serviceProfiles);

        return profiles;
    }
}
