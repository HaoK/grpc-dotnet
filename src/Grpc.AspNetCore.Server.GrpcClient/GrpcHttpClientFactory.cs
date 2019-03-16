﻿#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Net.Http;
using System.Threading;
using Grpc.AspNetCore.Server.Feature;
using Grpc.Core;
using Grpc.NetCore.HttpClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Grpc.AspNetCore.Server.GrpcClient
{
    internal class GrpcHttpClientFactory<TClient> : INamedTypedHttpClientFactory<TClient> where TClient : ClientBase
    {
        private readonly Cache _cache;
        private readonly IServiceProvider _services;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IOptionsMonitor<GrpcClientOptions> _clientOptions;

        public GrpcHttpClientFactory(Cache cache, IServiceProvider services, IOptionsMonitor<GrpcClientOptions> clientOptions)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (clientOptions == null)
            {
                throw new ArgumentNullException(nameof(clientOptions));
            }

            _cache = cache;
            _services = services;
            _httpContextAccessor = services.GetService<IHttpContextAccessor>();
            _clientOptions = clientOptions;
        }

        public TClient CreateClient(HttpClient httpClient, string name)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            var httpContext = _httpContextAccessor.HttpContext;
            var serverCallContext = httpContext?.Features.Get<IServerCallContextFeature>().ServerCallContext;

            var namedOptions = _clientOptions.Get(name);

            var callInvoker = new HttpClientCallInvoker(httpClient);
            if (namedOptions.UseRequestCancellationToken)
            {
                if (serverCallContext == null)
                {
                    throw new InvalidOperationException("Cannot set the request cancellation token on the client because there is no HttpContext.");
                }

                callInvoker.CancellationToken = serverCallContext.CancellationToken;
            }
            if (namedOptions.UseRequestDeadline)
            {
                if (serverCallContext == null)
                {
                    throw new InvalidOperationException("Cannot set the request deadline on the client because there is no HttpContext.");
                }

                callInvoker.Deadline = serverCallContext.Deadline;
            }

            return (TClient)_cache.Activator(_services, new object[] { callInvoker });
        }

        // The Cache should be registered as a singleton, so it that it can
        // act as a cache for the Activator. This allows the outer class to be registered
        // as a transient, so that it doesn't close over the application root service provider.
        public class Cache
        {
            private readonly static Func<ObjectFactory> _createActivator = () => ActivatorUtilities.CreateFactory(typeof(TClient), new Type[] { typeof(CallInvoker), });

            private ObjectFactory _activator;
            private bool _initialized;
            private object _lock;

            public ObjectFactory Activator => LazyInitializer.EnsureInitialized(
                ref _activator,
                ref _initialized,
                ref _lock,
                _createActivator);
        }
    }
}
