using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace Drawski;

// Saved to %AppData%\Drawski\settings.json. A missing or broken file just means defaults
public class Settings
{
    public static readonly Color[] DEFAULT_PALETTE =
    {
        Color.FromRgb(255, 59, 48), Color.FromRgb(255, 214, 10), Color.FromRgb(52, 199, 89),
        Color.FromRgb(10, 132, 255), Color.FromRgb(17, 17, 17), Color.FromRgb(255, 255, 255),
    };

    public const int MAX_SAVED_COLORS = 40;

    public readonly Color[] Palette = DEFAULT_PALETTE.ToArray();
    public readonly List<Color> SavedColors = new();
    public readonly Dictionary<Shortcut, Hotkey> Keys = ShortcutInfo.ALL.ToDictionary(info => info.Id, info => info.Default);
    public bool ShowOnStartup = true;

    readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Drawski", "settings.json");

    public void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            SettingsFile file = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(_path));
            if (file == null) return;

            ShowOnStartup = file.ShowOnStartup;
            for (int i = 0; i < Math.Min(file.Palette?.Count ?? 0, Palette.Length); i++)
                Palette[i] = (Color)ColorConverter.ConvertFromString(file.Palette[i]);
            foreach (string hex in (file.SavedColors ?? new List<string>()).Take(MAX_SAVED_COLORS))
                SavedColors.Add((Color)ColorConverter.ConvertFromString(hex));
            foreach ((string name, string keys) in file.Shortcuts ?? new Dictionary<string, string>())
                if (Enum.TryParse(name, out Shortcut id)) Keys[id] = Hotkey.Parse(keys);
        }
        catch { }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            SettingsFile file = new()
            {
                ShowOnStartup = ShowOnStartup,
                Palette = Palette.Select(ToHex).ToList(),
                SavedColors = SavedColors.Select(ToHex).ToList(),
                Shortcuts = Keys.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.Serialize()),
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    class SettingsFile
    {
        public bool ShowOnStartup { get; set; } = true;
        public List<string> Palette { get; set; }
        public List<string> SavedColors { get; set; }
        public Dictionary<string, string> Shortcuts { get; set; }
    }
}
