using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CecoChat.Data.Config.Partitioning;
using CecoChat.Jwt;
using CecoChat.Server.Backplane;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CecoChat.Server.Bff.Controllers
{
    [ApiController]
    [Route("api/connect")]
    public class ConnectController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly JwtOptions _jwtOptions;
        private readonly IClock _clock;
        private readonly IPartitionUtility _partitionUtility;
        private readonly IPartitioningConfig _partitioningConfig;

        private readonly SigningCredentials _signingCredentials;
        private readonly JwtSecurityTokenHandler _jwtTokenHandler;

        public ConnectController(
            ILogger<ConnectController> logger,
            IOptions<JwtOptions> jwtOptions,
            IClock clock,
            IPartitionUtility partitionUtility,
            IPartitioningConfig partitioningConfig)
        {
            _logger = logger;
            _jwtOptions = jwtOptions.Value;
            _clock = clock;
            _partitionUtility = partitionUtility;
            _partitioningConfig = partitioningConfig;

            byte[] secret = Encoding.UTF8.GetBytes(_jwtOptions.Secret);
            _signingCredentials = new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256Signature);
            _jwtTokenHandler = new();
            _jwtTokenHandler.OutboundClaimTypeMap.Clear();
        }

        [HttpPost(Name = "Connect")]
        [ProducesResponseType(typeof(ConnectResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<ConnectResponse> Connect([FromBody][BindRequired] ConnectRequest request)
        {
            if (!_userIDMap.TryGetValue(request.Username, out long userID))
            {
                return Unauthorized();
            }
            Activity.Current?.AddTag("user.id", userID);

            (Guid clientID, string accessToken) = CreateSession(userID); 
            _logger.LogInformation("User {0} authenticated and assigned user ID {1} and client ID {2}.", request.Username, userID, clientID);

            int partition = _partitionUtility.ChoosePartition(userID, _partitioningConfig.PartitionCount);
            string messagingServerAddress = _partitioningConfig.GetServerAddress(partition);
            _logger.LogInformation("User with ID {0} in partition {1} assigned to messaging server {2}.", userID, partition, messagingServerAddress);

            ConnectResponse response = new()
            {
                ClientID = clientID,
                AccessToken = accessToken,
                MessagingServerAddress = messagingServerAddress
            };
            return Ok(response);
        }

        private readonly Dictionary<string, long> _userIDMap = new()
        {
            {"bob", 1},
            {"alice", 2},
            {"peter", 1200}
        };

        private (Guid, string) CreateSession(long userID)
        {
            Guid clientID = Guid.NewGuid();
            Claim[] claims =
            {
                new(JwtRegisteredClaimNames.Sub, userID.ToString(), ClaimValueTypes.Integer64),
                new(ClaimTypes.Actor, clientID.ToString()),
                new(ClaimTypes.Role, "user")
            };

            DateTime expiration = _clock.GetNowUtc().Add(_jwtOptions.AccessTokenExpiration);
            JwtSecurityToken jwtToken = new(_jwtOptions.Issuer, _jwtOptions.Audience, claims, null, expiration, _signingCredentials);
            string accessToken = _jwtTokenHandler.WriteToken(jwtToken);

            return (clientID, accessToken);
        }
    }
}