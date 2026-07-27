namespace Crustcut.Presentation;

public enum DialogKind { Info, Warning, Error }

/// <summary>
/// Shows a message to the user. Abstracted so view-model logic that needs to report a
/// failure can be unit-tested — the WPF build called MessageBox.Show inline, which is a
/// large part of why the Optimize toggle path had no tests at all.
/// </summary>
public interface IDialogService
{
    Task ShowAsync(string title, string message, DialogKind kind = DialogKind.Info);

    /// <summary>
    /// Asks the user to confirm a destructive action. Returns false unless they explicitly
    /// accept — callers must treat anything but true as "do not proceed".
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);
}
