using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

/// <summary>
/// Groups the integration tests that mutate shared, real HKCU keys (Game Bar, Control
/// Panel, etc.) so xUnit runs them serially instead of in parallel. Without this they
/// stomp on each other's registry writes and fail intermittently.
/// </summary>
[CollectionDefinition("RealSystemRegistry")]
public sealed class RealSystemRegistryCollection { }
