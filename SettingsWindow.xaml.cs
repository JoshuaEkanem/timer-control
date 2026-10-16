using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ChurchTimer;

public partial class SettingsWindow : Window
{
    private readonly Action<AppSettings> _preview;
    private bool _loading = true;

    public AppSettings Result { get; private set; }

    public SettingsWindow(AppSettings current, Action<AppSettings> preview)
    {
        InitializeComponent();
        _preview = preview;
        Result = current.Clone();

        FontBox.ItemsSource = AppSettings.Fonts;
        BackgroundBox.ItemsSource = AppSettings.Backgrounds.Select(b => b.Name).ToList();
        DigitBox.ItemsSource = AppSettings.DigitColors.Select(c => c.Name).ToList();

        Show(current);
        _loading = false;
    }

    private void Show(AppSettings s)
    {
        FontBox.SelectedItem = AppSettings.Fonts.Contains(s.FontFamily)
            ? s.FontFamily : AppSettings.Fonts[0];
        BackgroundBox.SelectedItem = AppSettings.Backgrounds.Any(b => b.Name == s.Background)
            ? s.Background : AppSettings.Backgrounds[0].Name;
        DigitBox.SelectedItem = AppSettings.DigitColors.Any(c => c.Name == s.DigitColor)
            ? s.DigitColor : AppSettings.DigitColors[0].Name;
    }

    private AppSettings Current() => new()
    {
        FontFamily = FontBox.SelectedItem as string ?? AppSettings.Fonts[0],
        Background = BackgroundBox.SelectedItem as string ?? AppSettings.Backgrounds[0].Name,
        DigitColor = DigitBox.SelectedItem as string ?? AppSettings.DigitColors[0].Name
    };

    private void Option_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _preview(Current());     // shows the change live on the projector / preview
    }

    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        _loading = true;
        Show(new AppSettings());
        _loading = false;
        _preview(Current());
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = Current();
        DialogResult = true;
    }
}