namespace PrimeOSTuner.Core.Tweaks;

public sealed record ServiceState(bool Exists, string CurrentStartType, bool IsRunning);

public interface IServiceClient
{
    ServiceState Read(string serviceName);
    void SetStartTypeDisabled(string serviceName);
    void SetStartType(string serviceName, string startType);  // "Auto", "Manual", "Disabled"
    void Stop(string serviceName);
}
