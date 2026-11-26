using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Crustcut.App.Views;

/// <summary>
/// "Is this page actually on screen?" Pages stay attached to the window while hidden
/// (switching tabs flips IsVisible — see MainWindow.ShowActivePage), so attach/detach no
/// longer means shown/hidden. A page that polls or animates should follow THIS instead:
/// active = attached to a window AND visible. The callback fires only on changes.
/// </summary>
public static class PageActivity
{
    public static void Track(Control page, Action<bool> activeChanged)
    {
        var active = false;
        var attached = page.IsAttachedToVisualTree();
        void Update()
        {
            var now = page.IsVisible && attached;
            if (now == active) return;
            active = now;
            activeChanged(now);
        }

        page.AttachedToVisualTree += (_, _) => { attached = true; Update(); };
        page.DetachedFromVisualTree += (_, _) => { attached = false; Update(); };
        page.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty) Update();
        };
    }
}
