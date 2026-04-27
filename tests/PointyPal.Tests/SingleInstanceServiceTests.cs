using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class SingleInstanceServiceTests
{
    [Fact]
    public void TryAcquirePrimary_FirstInstance_ReturnsTrue()
    {
        string mutexName = "Local\\PointyPal.Test." + Guid.NewGuid().ToString();
        string signalName = "Local\\PointyPal.Test.Signal." + Guid.NewGuid().ToString();

        using var service = new SingleInstanceService(mutexName, signalName);
        
        bool acquired = service.TryAcquirePrimary();
        
        acquired.Should().BeTrue();
        service.IsPrimaryInstance.Should().BeTrue();
    }

    [Fact]
    public void TryAcquirePrimary_SecondInstance_ReturnsFalse_AndSignalsFirst()
    {
        string mutexName = "Local\\PointyPal.Test." + Guid.NewGuid().ToString();
        string signalName = "Local\\PointyPal.Test.Signal." + Guid.NewGuid().ToString();

        var signalReceived = new ManualResetEventSlim(false);

        using var primary = new SingleInstanceService(mutexName, signalName);
        primary.TryAcquirePrimary(() => signalReceived.Set());

        using var secondary = new SingleInstanceService(mutexName, signalName);
        
        bool acquired = secondary.TryAcquirePrimary();
        
        acquired.Should().BeFalse();
        secondary.IsPrimaryInstance.Should().BeFalse();

        // Wait up to 1 second for the signal to be received by the primary
        signalReceived.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        primary.SecondInstanceDetectedCount.Should().Be(1);
    }
}
