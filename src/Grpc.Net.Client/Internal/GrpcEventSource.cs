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

using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
#if HAVE_DIAGNOSTICS_COUNTERS
using System.Threading;
#endif
using Grpc.Core;

namespace Grpc.Net.Client.Internal
{
    internal class GrpcEventSource : EventSource
    {
        public static readonly GrpcEventSource Log = new GrpcEventSource();

#if HAVE_DIAGNOSTICS_COUNTERS
        private PollingCounter? _totalCallsCounter;
        private PollingCounter? _currentCallsCounter;
        private PollingCounter? _messagesSentCounter;
        private PollingCounter? _messagesReceivedCounter;
        private PollingCounter? _callsFailedCounter;
        private PollingCounter? _callsDeadlineExceededCounter;

        private long _totalCalls;
        private long _currentCalls;
        private long _messageSent;
        private long _messageReceived;
        private long _callsFailed;
        private long _callsDeadlineExceeded;
#endif

        internal GrpcEventSource()
            : base("Grpc.Net.Client")
        {
        }

        // Used for testing
        internal GrpcEventSource(string eventSourceName)
            : base(eventSourceName)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(eventId: 1, Level = EventLevel.Verbose)]
        public void CallStart(string method)
        {
#if HAVE_DIAGNOSTICS_COUNTERS
            Interlocked.Increment(ref _totalCalls);
            Interlocked.Increment(ref _currentCalls);
#endif

            WriteEvent(1, method);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(eventId: 2, Level = EventLevel.Verbose)]
        public void CallStop()
        {
#if HAVE_DIAGNOSTICS_COUNTERS
            Interlocked.Decrement(ref _currentCalls);
#endif

            WriteEvent(2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(eventId: 3, Level = EventLevel.Error)]
        public void CallFailed(StatusCode statusCode)
        {
#if HAVE_DIAGNOSTICS_COUNTERS
            Interlocked.Increment(ref _callsFailed);
#endif

            WriteEvent(3, (int)statusCode);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(eventId: 4, Level = EventLevel.Error)]
        public void CallDeadlineExceeded()
        {
#if HAVE_DIAGNOSTICS_COUNTERS
            Interlocked.Increment(ref _callsDeadlineExceeded);
#endif

            WriteEvent(4);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(eventId: 5, Level = EventLevel.Verbose)]
        public void MessageSent()
        {
#if HAVE_DIAGNOSTICS_COUNTERS
            Interlocked.Increment(ref _messageSent);
#endif

            WriteEvent(5);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(eventId: 6, Level = EventLevel.Verbose)]
        public void MessageReceived()
        {
#if HAVE_DIAGNOSTICS_COUNTERS
            Interlocked.Increment(ref _messageReceived);
#endif

            WriteEvent(6);
        }

#if HAVE_DIAGNOSTICS_COUNTERS
        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                // This is the convention for initializing counters in the RuntimeEventSource (lazily on the first enable command).
                // They aren't disabled afterwards...

                _totalCallsCounter ??= new PollingCounter("total-calls", this, () => _totalCalls)
                {
                    DisplayName = "Total Calls",
                };
                _currentCallsCounter ??= new PollingCounter("current-calls", this, () => _currentCalls)
                {
                    DisplayName = "Current Calls"
                };
                _callsFailedCounter ??= new PollingCounter("calls-failed", this, () => _callsFailed)
                {
                    DisplayName = "Total Calls Failed",
                };
                _callsDeadlineExceededCounter ??= new PollingCounter("calls-deadline-exceeded", this, () => _callsDeadlineExceeded)
                {
                    DisplayName = "Total Calls Deadline Exceeded",
                };
                _messagesSentCounter ??= new PollingCounter("messages-sent", this, () => _messageSent)
                {
                    DisplayName = "Total Messages Sent",
                };
                _messagesReceivedCounter ??= new PollingCounter("messages-received", this, () => _messageReceived)
                {
                    DisplayName = "Total Messages Received",
                };
            }
        }
#endif
    }
}