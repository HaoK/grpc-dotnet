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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Grpc.AspNetCore.Server.Internal
{
    /// <summary>
    /// Service binder that is passed to ServiceImpl.BindService(ServiceBinderBase, ServiceImpl).
    /// This will execute the call handler factory and create call handlers.
    /// </summary>
    internal class GrpcServiceBinder<TService> : ServiceBinderBase where TService : class
    {
        private readonly IEndpointRouteBuilder _builder;
        private readonly ServiceMethodsRegistry _serviceMethodsRegistry;
        private readonly ServerCallHandlerFactory<TService> _serverCallHandlerFactory;
        private readonly IGrpcMethodInvokerFactory<TService> _serviceInvokerFactory;

        internal IList<IEndpointConventionBuilder> EndpointConventionBuilders { get; } = new List<IEndpointConventionBuilder>();
        internal IList<IMethod> ServiceMethods { get; } = new List<IMethod>();

        internal GrpcServiceBinder(IEndpointRouteBuilder builder, IGrpcMethodInvokerFactory<TService> serviceInvokerFactory, ServerCallHandlerFactory<TService> serverCallHandlerFactory, ServiceMethodsRegistry serviceMethodsRegistry)
        {
            _builder = builder;
            _serviceMethodsRegistry = serviceMethodsRegistry;
            _serverCallHandlerFactory = serverCallHandlerFactory;
            _serviceInvokerFactory = serviceInvokerFactory;
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse> handler)
        {
            var callHandler = _serverCallHandlerFactory.CreateClientStreaming(method, _serviceInvokerFactory.CreateClientStreamingInvoker(method));
            AddMethodCore(method, callHandler.HandleCallAsync);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
        {
            var callHandler = _serverCallHandlerFactory.CreateDuplexStreaming(method, _serviceInvokerFactory.CreateDuplexStreamingInvoker(method));
            AddMethodCore(method, callHandler.HandleCallAsync);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse> handler)
        {
            var callHandler = _serverCallHandlerFactory.CreateServerStreaming(method, _serviceInvokerFactory.CreateServerStreamingInvoker(method));
            AddMethodCore(method, callHandler.HandleCallAsync);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        {
            var callHandler = _serverCallHandlerFactory.CreateUnary(method, _serviceInvokerFactory.CreateUnaryInvoker(method));
            AddMethodCore(method, callHandler.HandleCallAsync);
        }

        private void AddMethodCore(IMethod method, RequestDelegate requestDelegate)
        {
            ServiceMethods.Add(method);
            EndpointConventionBuilders.Add(_builder.MapPost(method.FullName, requestDelegate, new object[] { method }));
        }

        internal void CreateUnimplementedEndpoints()
        {
            // Return UNIMPLEMENTED status for requests with a URL that has two segments and a grpc/application content-type

            // Return UNIMPLEMENTED status for missing service:
            // - /{service}/{method}
            if (_serviceMethodsRegistry.Methods.Count == 0)
            {
                EndpointConventionBuilders.Add(CreateUnimplementedEndpoint("{unimplementedService}/{unimplementedMethod}", _serverCallHandlerFactory.CreateUnimplementedService()));
            }

            // Return UNIMPLEMENTED status for missing method:
            // - /Package.Service/{method}

            // Typically there should be one service name for a type
            // In case the bind method sets up multiple services in one call we'll loop over them
            var serviceNames = ServiceMethods.Select(m => m.ServiceName).Distinct().ToList();

            foreach (var serviceName in serviceNames)
            {
                if (_serviceMethodsRegistry.Methods.Any(m => string.Equals(m.ServiceName, serviceName, StringComparison.Ordinal)))
                {
                    // Unimplemented endpoint has already been added for this service
                    continue;
                }

                EndpointConventionBuilders.Add(CreateUnimplementedEndpoint(serviceName + "/{unimplementedMethod}", _serverCallHandlerFactory.CreateUnimplementedMethod()));
            }

            _serviceMethodsRegistry.Methods.AddRange(ServiceMethods);
        }

        private IEndpointConventionBuilder CreateUnimplementedEndpoint(string pattern, RequestDelegate requestDelegate)
        {
            var routePattern = RoutePatternFactory.Parse(pattern, null, new { contentType = GrpcContentTypeContraint.Instance });
            return _builder.Map(routePattern, requestDelegate, new HttpMethodMetadata(new[] { "POST" }));
        }

        private class GrpcContentTypeContraint : IRouteConstraint
        {
            public static readonly GrpcContentTypeContraint Instance = new GrpcContentTypeContraint();

            public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
            {
                if (httpContext == null)
                {
                    return false;
                }

                return GrpcProtocolHelpers.IsGrpcContentType(httpContext.Request.ContentType);
            }

            private GrpcContentTypeContraint()
            {
            }
        }
    }
}
