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

using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Greet;
using Grpc.Core;
using Grpc.Net.Client.Internal;
using Grpc.Net.Client.Tests.Infrastructure;
using Grpc.Tests.Shared;
using NUnit.Framework;

namespace Grpc.Net.Client.Tests
{
    [TestFixture]
    public class CallCredentialTests
    {
        [Test]
        public async Task MetadataCredentials_PerCall()
        {
            // Arrange
            string? authorizationValue = null;
            var httpClient = TestHelpers.CreateTestClient(async request =>
            {
                authorizationValue = request.Headers.GetValues("authorization").Single();

                var reply = new HelloReply { Message = "Hello world" };
                var streamContent = await TestHelpers.CreateResponseContent(reply).DefaultTimeout();
                return ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            });
            var invoker = HttpClientCallInvokerFactory.Create(httpClient);

            // Act
            var callCredentials = CallCredentials.FromInterceptor(async (context, metadata) =>
            {
                // Make sure the operation is asynchronous to ensure delegate is awaited
                await Task.Delay(100);
                metadata.Add("authorization", "SECRET_TOKEN");
            });
            var call = invoker.AsyncUnaryCall<HelloRequest, HelloReply>(TestHelpers.ServiceMethod, string.Empty, new CallOptions(credentials: callCredentials), new HelloRequest());
            await call.ResponseAsync.DefaultTimeout();

            // Assert
            Assert.AreEqual("SECRET_TOKEN", authorizationValue);
        }

        [Test]
        public async Task MetadataCredentials_ComposedPerCall()
        {
            // Arrange
            HttpRequestHeaders? requestHeaders = null;
            var httpClient = TestHelpers.CreateTestClient(async request =>
            {
                requestHeaders = request.Headers;

                var reply = new HelloReply { Message = "Hello world" };
                var streamContent = await TestHelpers.CreateResponseContent(reply).DefaultTimeout();
                return ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            });
            var invoker = HttpClientCallInvokerFactory.Create(httpClient);

            var first = CallCredentials.FromInterceptor(new AsyncAuthInterceptor((context, metadata) => {
                metadata.Add("first_authorization", "FIRST_SECRET_TOKEN");
                return Task.CompletedTask;
            }));
            var second = CallCredentials.FromInterceptor(new AsyncAuthInterceptor((context, metadata) => {
                metadata.Add("second_authorization", "SECOND_SECRET_TOKEN");
                return Task.CompletedTask;
            }));
            var third = CallCredentials.FromInterceptor(new AsyncAuthInterceptor((context, metadata) => {
                metadata.Add("third_authorization", "THIRD_SECRET_TOKEN");
                return Task.CompletedTask;
            }));

            // Act
            var callCredentials = CallCredentials.Compose(first, second, third);
            var call = invoker.AsyncUnaryCall<HelloRequest, HelloReply>(TestHelpers.ServiceMethod, string.Empty, new CallOptions(credentials: callCredentials), new HelloRequest());
            await call.ResponseAsync.DefaultTimeout();

            // Assert
            Assert.AreEqual("FIRST_SECRET_TOKEN", requestHeaders!.GetValues("first_authorization").Single());
            Assert.AreEqual("SECOND_SECRET_TOKEN", requestHeaders!.GetValues("second_authorization").Single());
            Assert.AreEqual("THIRD_SECRET_TOKEN", requestHeaders!.GetValues("third_authorization").Single());
        }
    }
}
