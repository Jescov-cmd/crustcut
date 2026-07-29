namespace Crustcut.Presentation;

/// <summary>
/// The overlay window, as seen from settings: re-sync visibility after a setting change,
/// and enter reposition (drag) mode. Abstracted so SettingsViewModel stays UI-free.
/// </summary>
public interface IOverlayControl
{
    /// <summary>Shows or hides the overlay to match current settings.</summary>
    void Sync();

    /// <summary>Toggles drag-to-reposition mode; position persists on exit.</summary>
    void ToggleEditMode();
}
