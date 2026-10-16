using System;
using System.Windows;

namespace ChurchTimer;

public partial class EditSessionWindow : Window
{
    private readonly string _origName;
    private readonly int _origMinutes;
    private readonly int _origSeconds;

    public string ResultName { get; private set; } = "";
    public int ResultMinutes { get; private set; }
    public int ResultSeconds { get; private set; }
    public bool DeleteRequested { get; private set; }

    public EditSessionWindow(string name, int minutes, int seconds, bool isNew)
    {
        InitializeComponent();
        _origName = name;
        _origMinutes = minutes;
        _origSeconds = seconds;

        Title = isNew ? "New session" : "Edit session";
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        NameBox.Text = name;
        MinBox.Text = minutes.ToString();
        SecBox.Text = seconds.ToString();
        Loaded += (s, e) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        int m = int.TryParse(MinBox.Text, out int mm) ? Math.Max(0, mm) : _origMinutes;
        int sec = int.TryParse(SecBox.Text, out int ss) ? Math.Max(0, ss) : _origSeconds;
        int total = m * 60 + sec;   // seconds over 59 roll into minutes
        ResultMinutes = total / 60;
        ResultSeconds = total % 60;
        ResultName = string.IsNullOrWhiteSpace(NameBox.Text) ? _origName : NameBox.Text.Trim();
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
        DialogResult = true;
    }
}