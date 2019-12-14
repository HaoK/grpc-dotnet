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
using System.Buffers;
using System.Buffers.Text;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.AspNetCore.Web.Internal
{
    /// <summary>
    /// Reads and decodes base64 encoded bytes from the inner reader.
    /// </summary>
    internal class Base64PipeReader : PipeReader
    {
        private readonly PipeReader _inner;
        private ReadOnlySequence<byte> _currentInnerBuffer;
        private ReadOnlySequence<byte> _currentDecodedBuffer;

        public Base64PipeReader(PipeReader inner)
        {
            _inner = inner;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            var consumedPosition = ResolvePosition(consumed);

            _inner.AdvanceTo(consumedPosition);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            var consumedPosition = ResolvePosition(consumed);
            var examinedPosition = ResolvePosition(examined);

            _inner.AdvanceTo(consumedPosition, examinedPosition);
        }

        private SequencePosition ResolvePosition(SequencePosition base64Position)
        {
            // TODO - is there a better way to get the index of a position in a sequence?
            var length = _currentDecodedBuffer.Slice(0, base64Position).Length;

            var resolvedPosition = _currentInnerBuffer.GetPosition(Base64.GetMaxEncodedToUtf8Length((int)length));
            return resolvedPosition;
        }

        public override void CancelPendingRead()
        {
            _inner.CancelPendingRead();
        }

        public override void Complete(Exception? exception = null)
        {
            _inner.Complete(exception);
        }

        public async override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            var innerResult = await _inner.ReadAsync(cancellationToken);
            if (innerResult.Buffer.IsEmpty)
            {
                _currentDecodedBuffer = innerResult.Buffer;
                _currentInnerBuffer = innerResult.Buffer;
                return innerResult;
            }

            // Minimum valid base64 length is 4. Read until we have at least that much content
            while (innerResult.Buffer.Length < 4)
            {
                _inner.AdvanceTo(innerResult.Buffer.Start, innerResult.Buffer.End);
                innerResult = await _inner.ReadAsync(cancellationToken);
            }

            // Limit result to complete base64 segments (multiples of 4)
            var buffer = innerResult.Buffer.Slice(0, (innerResult.Buffer.Length / 4) * 4);

            // The content can contain multiple fragments of base64 content
            // Check for padding, and limit returned data to one fragment at a time
            var paddingIndex = PositionOf(buffer, (byte)'=');
            if (paddingIndex != null)
            {
                _currentInnerBuffer = buffer.Slice(0, ((paddingIndex.Value / 4) + 1) * 4);
            }
            else
            {
                _currentInnerBuffer = buffer;
            }

            // TODO - optimize this
            // Get array from a pool, or attempt to reuse array inside readonly sequence
            var data = _currentInnerBuffer.ToArray();
            var validLength = (data.Length / 4) * 4;
            var status = Base64.DecodeFromUtf8InPlace(data.AsSpan(0, validLength), out var bytesWritten);
            if (status == OperationStatus.Done || status == OperationStatus.NeedMoreData)
            {
                _currentDecodedBuffer = new ReadOnlySequence<byte>(data, 0, bytesWritten);
                return new ReadResult(
                    _currentDecodedBuffer,
                    innerResult.IsCanceled,
                    innerResult.IsCompleted);
            }

            throw new InvalidOperationException("Unexpected status: " + status);
        }

        public override bool TryRead(out ReadResult result)
        {
            throw new NotImplementedException();
        }

        private static int? PositionOf<T>(in ReadOnlySequence<T> source, T value) where T : IEquatable<T>
        {
            if (source.IsSingleSegment)
            {
                int index = source.First.Span.IndexOf(value);
                if (index != -1)
                {
                    return index;
                }

                return null;
            }
            else
            {
                return PositionOfMultiSegment(source, value);
            }
        }

        private static int? PositionOfMultiSegment<T>(in ReadOnlySequence<T> source, T value) where T : IEquatable<T>
        {
            SequencePosition position = source.Start;
            int total = 0;
            while (source.TryGet(ref position, out ReadOnlyMemory<T> memory))
            {
                int index = memory.Span.IndexOf(value);
                if (index != -1)
                {
                    return total + index;
                }
                else if (position.GetObject() == null)
                {
                    break;
                }

                total += memory.Length;
            }

            return null;
        }
    }
}
