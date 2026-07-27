using Avalonia.Controls;

namespace Crustcut.App.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();

    public PlaceholderView(string title) : this() => TitleText.Text = title;
}
