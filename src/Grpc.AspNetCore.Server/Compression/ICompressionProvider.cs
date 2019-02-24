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

using System.IO;
using System.IO.Compression;

namespace Grpc.AspNetCore.Server.Compression
{
    /// <summary>
    /// Provides a specific compression implementation to compress gRPC messages.
    /// </summary>
    public interface ICompressionProvider
    {
        /// <summary>
        /// The encoding name used in the 'grpc-encoding' and 'grpc-accept-encoding' request and response headers.
        /// </summary>
        string EncodingName { get; }

        /// <summary>
        /// Create a new compression stream.
        /// </summary>
        /// <param name="stream">
        /// The stream where the compressed data is written when <paramref name="compressionMode"/> is <c>Compress</c>,
        /// and where compressed data is copied from when <paramref name="compressionMode"/> is <c>Decompress</c>.
        /// </param>
        /// <param name="compressionMode">The compression mode.</param>
        /// <returns>A stream used to compress or decompress data.</returns>
        Stream CreateStream(Stream stream, CompressionMode compressionMode);
    }
}
