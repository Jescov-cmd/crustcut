namespace Crustcut.Presentation;

/// <summary>
/// Registers Crustcut with the OS — Start Menu shortcut and elevated start-at-boot.
/// Abstracted so SettingsViewModel stays framework- and platform-free.
/// </summary>
public interface IAppRegistration
{
    void EnsureStartMenuShortcut();

    /// <summary>Enables/disables start-at-boot. Returns true if the state was achieved.</summary>
    bool SetStartAtBoot(bool enabled);
}
