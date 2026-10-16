using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ChurchTimer;

public class Session : INotifyPropertyChanged
{
    private string _name = "";
    private int _minutes;
    private int _seconds;
    private bool _isActive;

    public string Name
    {
        get => _name;
        set { _name = value; Notify(nameof(Name)); }
    }

    public int Minutes
    {
        get => _minutes;
        set { _minutes = value; Notify(nameof(Minutes)); Notify(nameof(TimeDisplay)); }
    }

    public int Seconds
    {
        get => _seconds;
        set { _seconds = value; Notify(nameof(Seconds)); Notify(nameof(TimeDisplay)); }
    }

    [JsonIgnore]
    public string TimeDisplay => $"{Minutes:00}:{Seconds:00}";

    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; Notify(nameof(IsActive)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public partial class MainWindow : Window
{
    // ----- Settings you may want to tweak -----
    private const double AmberAtSeconds = 60;   // digits turn amber at/below this
    private const double RedAtSeconds = 30;     // digits turn red at/below this
    private const int FlashSeconds = 15;        // how long to blink at 00:00
    // ------------------------------------------

    private static readonly Brush AmberBrush = new SolidColorBrush(Color.FromRgb(255, 179, 0));
    private static readonly Brush RedBrush = new SolidColorBrush(Color.FromRgb(255, 59, 48));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _flashTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _displayDebounce = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private TimeSpan _total = TimeSpan.FromMinutes(5);
    private TimeSpan _remaining = TimeSpan.FromMinutes(5);   // negative = counting up past zero
    private DateTime _endTime;
    private bool _running;

    private DateTime _flashUntil;
    private bool _flashOn = true;
    private bool _zeroAlerted;

    private OutputWindow? _output;
    private bool _outputIsFullScreen;
    private string _message = "";
    private AppSettings _settings = new();
    private Brush _normalBrush = Brushes.White;

    private static readonly string SessionsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ChurchTimer", "sessions.json");
    private readonly ObservableCollection<Session> _sessions = new();
    private Session? _active;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        _flashTimer.Tick += FlashTick;
        _displayDebounce.Tick += (s, e) => { _displayDebounce.Stop(); RefreshOutput(); };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        PreviewKeyDown += Window_PreviewKeyDown;
        _settings = AppSettings.Load();
        _normalBrush = _settings.GetDigitBrush();
        Loaded += (s, e) => RefreshOutput();
        Closed += (s, e) =>
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            CloseOutput();
        };

        MessageInput.ItemsSource = new[]
        {
            "Please wrap up",
            "Time is almost up",
            "Please be seated",
            "Welcome"
        };

        LoadSessions();
        SessionList.ItemsSource = _sessions;
        if (_sessions.Count > 0) LoadSession(_sessions[0], false);
        UpdateDisplay();
    }

    // ---------- Keyboard shortcuts ----------

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase) return;      // typing in a text field
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Key is not (Key.Space or Key.N or Key.Up or Key.Down)) return;

        e.Handled = true;          // stops a focused button from also reacting to Space
        if (e.IsRepeat) return;    // holding a key down does nothing extra

        switch (e.Key)
        {
            case Key.Space: ToggleStartPause(); break;
            case Key.N: NextSession(); break;
            case Key.Up: Adjust(TimeSpan.FromMinutes(1)); break;
            case Key.Down: Adjust(TimeSpan.FromMinutes(-1)); break;
        }
    }

    // ---------- Screen message ----------

    private void SetMessage(string text)
    {
        _message = (text ?? "").Trim();
        MessageStatus.Text = _message.Length == 0
            ? "Screen message: none"
            : $"Screen message: {_message}";
        _output?.SetMessage(_message);
    }

    private void ShowMessage_Click(object sender, RoutedEventArgs e) => SetMessage(MessageInput.Text);

    private void ClearMessage_Click(object sender, RoutedEventArgs e)
    {
        SetMessage("");
        MessageInput.Text = "";
    }

    private void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SetMessage(MessageInput.Text);
            e.Handled = true;
        }
    }

    // ---------- Sessions ----------

    private void LoadSessions()
    {
        try
        {
            if (File.Exists(SessionsPath))
            {
                var list = JsonSerializer.Deserialize<System.Collections.Generic.List<Session>>(
                    File.ReadAllText(SessionsPath));
                if (list != null)
                    foreach (var s in list) _sessions.Add(s);
            }
        }
        catch { }

        if (_sessions.Count == 0)
        {
            _sessions.Add(new Session { Name = "Praise & Worship", Minutes = 20 });
            _sessions.Add(new Session { Name = "Announcements", Minutes = 5 });
            _sessions.Add(new Session { Name = "Sermon", Minutes = 40 });
            _sessions.Add(new Session { Name = "Offering", Minutes = 10 });
            SaveSessions();
        }
    }

    private void SaveSessions()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionsPath)!);
            File.WriteAllText(SessionsPath,
                JsonSerializer.Serialize(_sessions, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    // Loads a session into the timer. Only called from Play / Next, never from a plain click.
    private void LoadSession(Session s, bool start)
    {
        Stop();
        if (_active != null) _active.IsActive = false;
        _active = s;
        s.IsActive = true;
        _total = new TimeSpan(0, s.Minutes, s.Seconds);
        _remaining = _total;
        SessionNameText.Text = $"Now: {s.Name}";
        SetMessage("");      // a new session starts with a clean screen
        UpdateDisplay();
        if (start) Start();
    }

    private void PlaySession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not Session s) return;

        if (ReferenceEquals(s, _active))
        {
            // Already the loaded session: never restart it. Resume if it isn't running.
            if (!_running)
            {
                Start();
                UpdateDisplay();
            }
            return;
        }
        LoadSession(s, true);
    }

    private void Next_Click(object sender, RoutedEventArgs e) => NextSession();

    private void NextSession()
    {
        int i = _active == null ? -1 : _sessions.IndexOf(_active);
        if (i + 1 < _sessions.Count) LoadSession(_sessions[i + 1], false);
    }

    private void AddSession_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new EditSessionWindow("New session", 5, 0, true) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        _sessions.Add(new Session
        {
            Name = dlg.ResultName,
            Minutes = dlg.ResultMinutes,
            Seconds = dlg.ResultSeconds
        });
        SaveSessions();
    }

    private void EditSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not Session s) return;

        var dlg = new EditSessionWindow(s.Name, s.Minutes, s.Seconds, false) { Owner = this };
        if (dlg.ShowDialog() != true) return;     // Cancel: nothing changes

        if (dlg.DeleteRequested)
        {
            var answer = MessageBox.Show(this, $"Delete \"{s.Name}\"?", "Delete session",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            if (ReferenceEquals(s, _active)) _active = null;
            _sessions.Remove(s);
            SaveSessions();
            return;
        }

        ApplySessionEdit(s, dlg.ResultName, dlg.ResultMinutes, dlg.ResultSeconds);
    }

    private void ApplySessionEdit(Session s, string name, int m, int sec)
    {
        bool timeChanged = m != s.Minutes || sec != s.Seconds;

        if (ReferenceEquals(s, _active))
        {
            if (timeChanged)
            {
                // Keep elapsed time: shift what's remaining by the change in length.
                var newTotal = new TimeSpan(0, m, sec);
                Adjust(newTotal - _total);
                _total = newTotal;
            }
            SessionNameText.Text = $"Now: {name}";
        }

        s.Name = name;
        s.Minutes = m;
        s.Seconds = sec;
        SaveSessions();
    }

    // ---------- Display / output handling ----------

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _displayDebounce.Stop();
            _displayDebounce.Start();
        });
    }
    private void ApplyAppearance(AppSettings s)
    {
        _settings = s;
        _normalBrush = s.GetDigitBrush();
        _output?.ApplyAppearance(s.FontFamily, s.GetBackgroundBrush());
        UpdateDisplay();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var original = _settings.Clone();
        var dlg = new SettingsWindow(original.Clone(), ApplyAppearance) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            ApplyAppearance(dlg.Result);
            dlg.Result.Save();
        }
        else
        {
            ApplyAppearance(original);     // Cancel: back to how it was
        }
    }
    private OutputWindow CreateOutput()
    {
        var win = new OutputWindow();
        win.SetDisplay(TimeText.Text, TimeText.Foreground, TimeText.Opacity);
        win.SetMessage(_message);
        win.ApplyAppearance(_settings.FontFamily, _settings.GetBackgroundBrush());
        win.Closed += (s, e) =>
        {
            if (ReferenceEquals(_output, s))
            {
                _output = null;
                _outputIsFullScreen = false;
            }
        };
        return win;
    }

    private void CloseOutput()
    {
        var win = _output;
        _output = null;
        _outputIsFullScreen = false;
        win?.Close();
    }

    private void RefreshOutput()
    {
        var secondary = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => !s.Primary);
        if (secondary != null)
        {
            CloseOutput();
            _output = CreateOutput();
            _output.ShowFullScreenOn(secondary, VisualTreeHelper.GetDpi(this).DpiScaleX);
            _outputIsFullScreen = true;
        }
        else if (_outputIsFullScreen)
        {
            CloseOutput();
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_outputIsFullScreen) return;
        if (_output != null)
        {
            CloseOutput();
            return;
        }
        _output = CreateOutput();
        _output.ShowPreview();
    }

    // ---------- Timer logic ----------

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _remaining = _endTime - DateTime.UtcNow;
        if (_remaining <= TimeSpan.Zero && !_zeroAlerted)
        {
            _zeroAlerted = true;
            StartFlash();
        }
        UpdateDisplay();
    }

    private void StartFlash()
    {
        _flashUntil = DateTime.UtcNow.AddSeconds(FlashSeconds);
        _flashOn = true;
        _flashTimer.Start();
    }

    private void FlashTick(object? sender, EventArgs e)
    {
        if (DateTime.UtcNow >= _flashUntil)
        {
            _flashTimer.Stop();
            _flashOn = true;
        }
        else
        {
            _flashOn = !_flashOn;
        }
        UpdateDisplay();
    }

    private void Start()
    {
        _endTime = DateTime.UtcNow + _remaining;
        _running = true;
        _timer.Start();
        StartButton.Content = "Pause";
    }

    private void Stop()
    {
        if (_running) _remaining = _endTime - DateTime.UtcNow;
        _running = false;
        _timer.Stop();
        StartButton.Content = "Start";
    }

    private void ToggleStartPause()
    {
        if (_running) Stop(); else Start();
        UpdateDisplay();
    }

    private void StartPause_Click(object sender, RoutedEventArgs e) => ToggleStartPause();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Stop();
        _remaining = _total;
        UpdateDisplay();
    }

    private void Adjust(TimeSpan delta)
    {
        TimeSpan before = _running ? _endTime - DateTime.UtcNow : _remaining;
        TimeSpan after = before + delta;

        // Taking time away never pushes a countdown past zero; it just lands on 00:00.
        if (before >= TimeSpan.Zero && after < TimeSpan.Zero) after = TimeSpan.Zero;

        if (_running) _endTime = DateTime.UtcNow + after;
        _remaining = after;
        UpdateDisplay();
    }

    private void Plus_Click(object sender, RoutedEventArgs e) => Adjust(TimeSpan.FromMinutes(1));
    private void Minus_Click(object sender, RoutedEventArgs e) => Adjust(TimeSpan.FromMinutes(-1));

    private void UpdateDisplay()
    {
        // Back above zero: clear the alert so it can fire again later.
        if (_remaining > TimeSpan.Zero)
        {
            _zeroAlerted = false;
            if (_flashTimer.IsEnabled)
            {
                _flashTimer.Stop();
                _flashOn = true;
            }
        }

        string text;
        if (_remaining < TimeSpan.Zero)
        {
            int over = (int)Math.Floor(-_remaining.TotalSeconds);
            text = over == 0 ? "00:00" : $"+{over / 60:00}:{over % 60:00}";
        }
        else
        {
            int secs = (int)Math.Ceiling(_remaining.TotalSeconds);
            text = $"{secs / 60:00}:{secs % 60:00}";
        }

        bool started = _running || _remaining < _total;
        Brush color = _normalBrush;
        if (started)
        {
            if (_remaining.TotalSeconds <= RedAtSeconds) color = RedBrush;
            else if (_remaining.TotalSeconds <= AmberAtSeconds) color = AmberBrush;
        }

        double opacity = _flashOn ? 1.0 : 0.15;

        TimeText.Text = text;
        TimeText.Foreground = color;
        TimeText.Opacity = opacity;
        _output?.SetDisplay(text, color, opacity);
    }
}