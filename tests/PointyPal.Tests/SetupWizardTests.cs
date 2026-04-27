using System;
using Xunit;
using PointyPal.UI;

namespace PointyPal.Tests;

public class SetupWizardTests
{
    [Fact]
    public void SetupWizardState_Initializes_WithDefaults()
    {
        var state = new SetupWizardState();
        Assert.Equal("", state.WorkerBaseUrl);
        Assert.Equal("", state.WorkerClientKey);
        Assert.False(state.WorkerReachable);
        Assert.False(state.PrivacySafeDefaultsApplied);
    }

    [Fact]
    public void SetupWizardResult_Initializes_False()
    {
        var result = new SetupWizardResult();
        Assert.False(result.Completed);
        Assert.False(result.Skipped);
    }
}
