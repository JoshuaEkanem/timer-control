using System.Windows;
using System.Windows.Media;

namespace ChurchTimer;

public partial class OutputWindow : Window
{
    public OutputWindow()
    {
        InitializeComponent();
    }

    public void SetDisplay(string text, Brush color, double opacity)
    {
        TimeText.Text = text;
        TimeText.Foreground = color;
        TimeText.Opacity = opacity;
    }

    public void SetMessage(string text)
    {
        MessageText.Text = text;
        MessageText.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }
    public void ApplyAppearance(string fontFamily, Brush background)
    {
        Background = background;
        TimeText.FontFamily = new System.Windows.Media.FontFamily(fontFamily);
    }

    public void ShowPreview()
    {
        Show();
    }

    public void ShowFullScreenOn(System.Windows.Forms.Screen screen, double dpiScale)
    {
        var b = screen.Bounds;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Cursor = System.Windows.Input.Cursors.None;
        Width = 200;
        Height = 200;
        // Place a small window in the middle of the target screen, then maximize it
        // so Windows stretches it to fill exactly that screen.
        Left = (b.Left + b.Width / 2.0) / dpiScale - 100;
        Top = (b.Top + b.Height / 2.0) / dpiScale - 100;
        Show();
        WindowState = WindowState.Maximized;
    }
}