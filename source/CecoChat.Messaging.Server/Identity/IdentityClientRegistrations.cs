﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace CecoChat.Messaging.Server.Identity
{
    public static class IdentityClientRegistrations
    {
        public static void AddIdentityClient(this IServiceCollection services, IIdentityClientOptions options)
        {
            services
                .AddGrpcClient<Contracts.Identity.Identity.IdentityClient>(grpc =>
                {
                    grpc.Address = options.Address;
                })
                .ConfigurePrimaryHttpMessageHandler(() => CreateMessageHandler(options))
                .AddPolicyHandler(_ => HandleFailure(options));
        }

        private static HttpMessageHandler CreateMessageHandler(IIdentityClientOptions options)
        {
            return new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = options.KeepAlivePingDelay,
                KeepAlivePingTimeout = options.KeepAlivePingTimeout,
                EnableMultipleHttp2Connections = true
            };
        }

        private static IAsyncPolicy<HttpResponseMessage> HandleFailure(IIdentityClientOptions options)
        {
            Random jitterGenerator = new();

            return Policy.HandleResult<HttpResponseMessage>(response =>
            {
                StatusCode? grpcStatus = GetGrpcStatusCode(response);

                bool isHttpError = grpcStatus == null && !response.IsSuccessStatusCode;
                bool isGrpcError = response.IsSuccessStatusCode && grpcStatus == StatusCode.OK;

                return !isHttpError && !isGrpcError;
            })
            .WaitAndRetryAsync(
                options.RetryCount,
                retryAttempt => SleepDurationProvider(retryAttempt, jitterGenerator, options),
                onRetry: (_, _, _, _) =>
                {
                    // if needed we can obtain new tokens and do other per-call stuff here
                });
        }

        private static StatusCode? GetGrpcStatusCode(HttpResponseMessage response)
        {
            HttpResponseHeaders headers = response.Headers;
            const string grpcStatusHeader = "grpc-status";

            if (!headers.Contains(grpcStatusHeader) && response.StatusCode == HttpStatusCode.OK)
            {
                return StatusCode.OK;
            }
            if (headers.TryGetValues(grpcStatusHeader, out IEnumerable<string> values))
            {
                return (StatusCode)int.Parse(values.First());
            }

            return null;
        }

        private static TimeSpan SleepDurationProvider(int retryAttempt, Random jitterGenerator, IIdentityClientOptions options)
        {
            TimeSpan sleepDuration;
            if (retryAttempt == 1)
            {
                sleepDuration = options.InitialBackOff;
            }
            else
            {
                // exponential delay
                sleepDuration = TimeSpan.FromSeconds(Math.Pow(options.BackOffMultiplier, retryAttempt));
                if (sleepDuration > options.MaxBackOff)
                {
                    sleepDuration = options.MaxBackOff;
                }
            }

            TimeSpan jitter = TimeSpan.FromMilliseconds(jitterGenerator.Next(0, options.MaxJitterMs));
            sleepDuration += jitter;

            return sleepDuration;
        }
    }
}
