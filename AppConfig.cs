using System;
using System.IO;
using System.Text.Json;

namespace UsualAudioConverter;

public class AppConfig
{
    public string FfmpegPath { get; set; } = "ffmpeg.exe";
    public string ExhalePath { get; set; } = "exhale.exe";
    public string FdkaacPath { get; set; } = "fdkaac.exe";

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UsualAudioConverter",
        "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }

        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}
