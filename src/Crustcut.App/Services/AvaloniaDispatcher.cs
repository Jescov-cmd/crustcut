using Avalonia.Threading;
using Crustcut.Presentation;

namespace Crustcut.App.Services;

public sealed class AvaloniaDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
