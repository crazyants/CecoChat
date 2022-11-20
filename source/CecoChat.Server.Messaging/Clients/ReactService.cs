using System.Diagnostics;
using CecoChat.Contracts.Backplane;
using CecoChat.Contracts.Messaging;
using CecoChat.Server.Identity;
using CecoChat.Server.Messaging.Backplane;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace CecoChat.Server.Messaging.Clients;

public class ReactService : Reaction.ReactionBase
{
    private readonly ILogger _logger;
    private readonly ISendersProducer _sendersProducer;
    private readonly IContractDataMapper _mapper;

    public ReactService(
        ILogger<ReactService> logger,
        ISendersProducer sendersProducer,
        IContractDataMapper mapper)
    {
        _logger = logger;
        _sendersProducer = sendersProducer;
        _mapper = mapper;
    }

    [Authorize(Roles = "user")]
    public override Task<ReactResponse> React(ReactRequest request, ServerCallContext context)
    {
        UserClaims userClaims = GetUserClaims(context);
        _logger.LogTrace("User {UserId} with client {ClientId} reacted with {Reaction} to message {MessageId} sent by user {SenderId}",
            userClaims.UserId, userClaims.ClientId, request.Reaction, request.MessageId, request.SenderId);

        BackplaneMessage backplaneMessage = _mapper.CreateBackplaneMessage(request, userClaims.ClientId, userClaims.UserId);
        _sendersProducer.ProduceMessage(backplaneMessage);

        return Task.FromResult(new ReactResponse());
    }

    [Authorize(Roles = "user")]
    public override Task<UnReactResponse> UnReact(UnReactRequest request, ServerCallContext context)
    {
        UserClaims userClaims = GetUserClaims(context);
        _logger.LogTrace("User {UserId} with client {ClientId} un-reacted to message {MessageId} sent by user {SenderId}",
            userClaims.UserId, userClaims.ClientId, request.MessageId, request.SenderId);

        BackplaneMessage backplaneMessage = _mapper.CreateBackplaneMessage(request, userClaims.ClientId, userClaims.UserId);
        _sendersProducer.ProduceMessage(backplaneMessage);

        return Task.FromResult(new UnReactResponse());
    }

    private UserClaims GetUserClaims(ServerCallContext context)
    {
        if (!context.GetHttpContext().User.TryGetUserClaims(out UserClaims? userClaims))
        {
            _logger.LogError("Client from {ClientAddress} was authorized but has no parseable access token", context.Peer);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Access token could not be parsed."));
        }

        Activity.Current?.SetTag("reactor.id", userClaims.UserId);
        return userClaims;
    }
}