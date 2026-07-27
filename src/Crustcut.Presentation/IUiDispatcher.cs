namespace Crustcut.Presentation;

/// <summary>
/// Marshals a callback onto the UI thread. Exists so view-models never reference a UI
/// framework directly — the WPF build reached for System.Windows.Application.Current.Dispatcher,
/// which both tied view-models to WPF and made them untestable.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Queues <paramref name="action"/> to run on the UI thread.</summary>
    void Post(Action action);
}
