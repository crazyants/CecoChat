﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cassandra;
using CecoChat.Contracts.Backend;
using CecoChat.ProtobufNet;
using Microsoft.Extensions.Logging;

namespace CecoChat.Data.Messaging
{
    public interface IDataUtility
    {
        PreparedStatement PrepareQuery(string cql);

        Task<List<Message>> GetMessages(IStatement query, int countHint);

        string CreateDialogID(long userID1, long userID2);
    }

    public sealed class DataUtility : IDataUtility
    {
        private readonly ILogger _logger;
        private readonly ICecoChatDbContext _dbContext;
        private readonly ProtobufSerializer _messageSerializer;

        public DataUtility(
            ILogger<DataUtility> logger,
            ICecoChatDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
            _messageSerializer = new ProtobufSerializer();
        }

        public PreparedStatement PrepareQuery(string cql)
        {
            PreparedStatement preparedQuery = _dbContext.Messaging.Prepare(cql);
            _logger.LogTrace("Prepared CQL '{0}'.", cql);
            return preparedQuery;
        }

        public async Task<List<Message>> GetMessages(IStatement query, int countHint)
        {
            RowSet rows = await _dbContext.Messaging.ExecuteAsync(query);
            List<Message> messages = new(capacity: countHint);

            foreach (Row row in rows)
            {
                byte[] messageBytes = row.GetValue<byte[]>("data");
                Message message = _messageSerializer.DeserializeFromSpan<Message>(messageBytes);
                messages.Add(message);
            }

            return messages;
        }

        public string CreateDialogID(long userID1, long userID2)
        {
            long min = Math.Min(userID1, userID2);
            long max = Math.Max(userID1, userID2);

            return $"{min}-{max}";
        }
    }
}