namespace UsualAudioConverter;

public class EncoderProfile
{
    public string Name { get; set; } = string.Empty;
    public string Engine { get; set; } = "ffmpeg"; // "ffmpeg" or "exhale"
    public string Extension { get; set; } = ".m4a";
    public string? Codec { get; set; }
    public string? Profile { get; set; }
    public int[] Bitrates { get; set; } = Array.Empty<int>();
    public int DefaultBitrate { get; set; }

    public static List<EncoderProfile> GetProfiles() => new()
    {
        new EncoderProfile
        {
            Name = "AAC-LC (標準高音質)",
            Engine = "ffmpeg",
            Extension = ".m4a",
            Codec = "libfdk_aac",
            Profile = "aac_low",
            Bitrates = new[] { 96, 128, 160, 192, 256, 320 },
            DefaultBitrate = 192
        },
        new EncoderProfile
        {
            Name = "HE-AAC v1 (中低音質・SBR)",
            Engine = "ffmpeg",
            Extension = ".m4a",
            Codec = "libfdk_aac",
            Profile = "aac_he",
            Bitrates = new[] { 48, 64, 80, 96 },
            DefaultBitrate = 64
        },
        new EncoderProfile
        {
            Name = "HE-AAC v2 (超低音質・SBR+PS)",
            Engine = "ffmpeg",
            Extension = ".m4a",
            Codec = "libfdk_aac",
            Profile = "aac_he_v2",
            Bitrates = new[] { 24, 32, 40, 48 },
            DefaultBitrate = 32
        },
        new EncoderProfile
        {
            Name = "xHE-AAC (exhale・最大256k対応)",
            Engine = "exhale",
            Extension = ".m4a",
            Bitrates = new[] { 24, 32, 40, 48, 64, 80, 96, 112, 128, 144, 160, 176, 192, 256 },
            DefaultBitrate = 64
        },
        new EncoderProfile
        {
            Name = "MP3 (LAME CBR)",
            Engine = "ffmpeg",
            Extension = ".mp3",
            Codec = "libmp3lame",
            Bitrates = new[] { 128, 160, 192, 256, 320 },
            DefaultBitrate = 192
        },
        new EncoderProfile
        {
            Name = "Ogg Vorbis",
            Engine = "ffmpeg",
            Extension = ".ogg",
            Codec = "libvorbis",
            Bitrates = new[] { 96, 128, 160, 192, 256, 320 },
            DefaultBitrate = 160
        },
        new EncoderProfile
        {
            Name = "Opus (Ogg)",
            Engine = "ffmpeg",
            Extension = ".opus",
            Codec = "libopus",
            Bitrates = new[] { 48, 64, 96, 128, 160, 192 },
            DefaultBitrate = 96
        }
    };
}
