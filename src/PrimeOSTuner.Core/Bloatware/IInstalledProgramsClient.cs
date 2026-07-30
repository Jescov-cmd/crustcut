namespace PrimeOSTuner.Core.Bloatware;

/// <summary>Lists user-visible installed desktop programs. Windows impl reads the
/// Uninstall registry roots; a macOS impl would scan /Applications.</summary>
public interface IInstalledProgramsClient
{
    IReadOnlyList<DesktopProgram> ListInstalled();
}
