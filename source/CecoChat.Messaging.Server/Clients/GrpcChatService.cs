﻿using System.Threading;
using System.Threading.Tasks;
using CecoChat.Contracts.Client;
using CecoChat.Messaging.Server.Servers.Production;
using CecoChat.Messaging.Server.Shared;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace CecoChat.Messaging.Server.Clients
{
    public sealed class GrpcChatService : Chat.ChatBase
    {
        private readonly ILogger _logger;
        private readonly IClientContainer _clientContainer;
        private readonly IBackendProducer _backendProducer;
        private readonly IClientBackendMapper _mapper;

        public GrpcChatService(
            ILogger<GrpcChatService> logger,
            IClientContainer clientContainer,
            IBackendProducer backendProducer,
            IClientBackendMapper mapper)
        {
            _logger = logger;
            _clientContainer = clientContainer;
            _backendProducer = backendProducer;
            _mapper = mapper;
        }

        public override async Task Listen(ListenRequest request, IServerStreamWriter<ListenResponse> responseStream, ServerCallContext context)
        {
            IStreamer<ListenResponse> streamer = new GrpcStreamer<ListenResponse>(_logger, responseStream);
            try
            {
                _clientContainer.AddClient(request.UserId, streamer);
                // TODO: pass a cancellation token
                await streamer.ProcessMessages(CancellationToken.None);
            }
            finally
            {
                _clientContainer.RemoveClient(request.UserId, streamer);
                streamer.Dispose();
            }
        }

        public override Task<SendMessageResponse> SendMessage(SendMessageRequest request, ServerCallContext context)
        {
            Message clientMessage = request.Message;
            Contracts.Backend.Message backendMessage = _mapper.MapClientToBackendMessage(clientMessage);
            _backendProducer.ProduceMessage(backendMessage);

            return Task.FromResult(new SendMessageResponse());
        }
    }
}
