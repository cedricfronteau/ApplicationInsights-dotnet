﻿namespace Microsoft.ApplicationInsights.AspNet.Tests
{
    using System;
    using Microsoft.ApplicationInsights.Channel;

    // TODO: Remove FakeTelemetryChannel when we can use a dynamic test isolation framework, like NSubstitute or Moq
    internal class FakeTelemetryChannel : ITelemetryChannel
    {
        public bool DeveloperMode { get; set; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Send(ITelemetry item)
        {
            throw new NotImplementedException();
        }
    }
}