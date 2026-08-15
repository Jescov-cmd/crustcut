using Avalonia.Controls;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class WhatsNewView : UserControl
{
    public WhatsNewView()
    {
        InitializeComponent();
        ReleaseList.ItemsSource = WhatsNewCatalog.Releases;
    }
}
