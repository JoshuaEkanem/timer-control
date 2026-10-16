using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;

namespace ChurchTimer;

public class AppSettings
{
    public string FontFamily { get; set; } = "Consolas";
    public string Background { get; set; } = "Black";
    public string DigitColor { get; set; } = "White";

    public static readonly string[] Fonts =
        { "Consolas", "Segoe UI", "Arial", "Calibri", "Verdana", "Georgia" };

    public static readonly (string Name, string Hex)[] Backgrounds =
    {
        ("Black", "#000000"),
        ("Dark blue", "#0A1F44"),
        ("Dark green", "#0B2B1A"),
        ("Dark gray", "#1A1A1A")
    };

    public static readonly (string Name, string Hex)[] DigitColors =
    {
        ("White", "#FFFFFF"),
        ("Warm white", "#FFF4D6"),
        ("Light blue", "#BFE3FF"),
        ("Light green", "#C8F7C5")
    };

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ChurchTimer", "settings.json");

    public AppSettings Clone() => new()
    {
        FontFamily = FontFamily,
        Background = Background,
        DigitColor = DigitColor
    };

    public Brush GetBackgroundBrush() => BrushFrom(Backgrounds, Background);
    public Brush GetDigitBrush() => BrushFrom(DigitColors, DigitColor);

    private static Brush BrushFrom((string Name, string Hex)[] list, string name)
    {
        string hex = list.FirstOrDefault(x => x.Name == name).Hex ?? list[0].Hex;
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null) return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}