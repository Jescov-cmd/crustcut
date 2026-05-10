namespace PrimeOSTuner.Core.Bloatware;

public enum SafetyTier
{
    Safe,    // pure consumer apps — uninstall freely
    Risky,   // some games / workflows depend on these — warn before uninstall
    Blocked  // required by Windows or other apps — uninstall disabled
}
