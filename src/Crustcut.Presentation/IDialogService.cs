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
}
