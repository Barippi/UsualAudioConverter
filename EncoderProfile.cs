using System;
using System.Collections.Generic;

namespace UsualAudioConverter;

public class EncoderProfile
{
    public string Name { get; set; } = string.Empty;
    public string Engine { get; set; } = "ffmpeg"; // "ffmpeg" or "exhale"
    public string Extension { get; set; } = ".m4a";
    public string? Codec { get; set; }
    public string? Profile { get; set; }
    public string Mode { get; set; } = "bitrate"; // "bitrate", "lame_vbr", "vorbis_vbr", "fdk_vbr", "none"
    public object[] QualityOptions { get; set; } = Array.Empty<object>();
    public object DefaultQuality { get; set; } = 0;

    public bool RequiresFdk => Profile is "aac_he" or "aac_he_v2";

    public static List<EncoderProfile> GetProfiles() => new()
    {
        new EncoderProfile
        {
            Name = "MP3 (LAME VBR)",
            Engine = "ffmpeg",
            Extension = ".mp3",
            Codec = "libmp3lame",
            Mode = "lame_vbr",
            QualityOptions = new object[] { "V0 (最高品質 ~245k)", "V1 (~225k)", "V2 (標準推奨 ~190k)", "V3 (~175k)", "V4 (~165k)", "V5 (~130k)", "V6 (~115k)" },
            DefaultQuality = "V2 (標準推奨 ~190k)"
        },
        new EncoderProfile
        {
            Name = "AAC-LC (FDK-AAC / 内蔵AAC)",
            Engine = "ffmpeg",
            Extension = ".m4a",
            Codec = "libfdk_aac",
            Profile = "aac_low",
            Mode = "fdk_vbr",
            QualityOptions = new object[] { "VBR Mode 5 (最高品質 ~220-260k)", "VBR Mode 4 (高音質 ~160-190k)", "VBR Mode 3 (標準 ~120-150k)", "VBR Mode 2 (~80-100k)", "VBR Mode 1 (~64k)" },
            DefaultQuality = "VBR Mode 5 (最高品質 ~220-260k)"
        },
        new EncoderProfile
        {
            Name = "HE-AAC v1 (libfdk_aac / fdkaac)",
            Engine = "ffmpeg",
            Extension = ".m4a",
            Codec = "libfdk_aac",
            Profile = "aac_he",
            Mode = "bitrate",
            QualityOptions = new object[] { 48, 64, 80, 96 },
            DefaultQuality = 64
        },
        new EncoderProfile
        {
            Name = "HE-AAC v2 (libfdk_aac / fdkaac)",
            Engine = "ffmpeg",
            Extension = ".m4a",
            Codec = "libfdk_aac",
            Profile = "aac_he_v2",
            Mode = "bitrate",
            QualityOptions = new object[] { 24, 32, 40, 48 },
            DefaultQuality = 32
        },
        new EncoderProfile
        {
            Name = "Ogg Vorbis (VBR)",
            Engine = "ffmpeg",
            Extension = ".ogg",
            Codec = "libvorbis",
            Mode = "vorbis_vbr",
            QualityOptions = new object[] { "q10 (最高品質 ~500k)", "q9 (~320k)", "q8 (~256k)", "q7 (~224k)", "q6 (推奨 ~192k)", "q5 (~160k)", "q4 (~128k)" },
            DefaultQuality = "q6 (推奨 ~192k)"
        },
        new EncoderProfile
        {
            Name = "Opus (VBR / ABR)",
            Engine = "ffmpeg",
            Extension = ".opus",
            Codec = "libopus",
            Mode = "bitrate",
            QualityOptions = new object[] { 64, 96, 128, 160, 192, 256 },
            DefaultQuality = 128
        },
        new EncoderProfile
        {
            Name = "xHE-AAC / USAC (exhale VBR)",
            Engine = "exhale",
            Extension = ".m4a",
            Mode = "bitrate",
            QualityOptions = new object[] { 24, 32, 40, 48, 64, 80, 96, 112, 128, 144, 160, 176, 192, 256 },
            DefaultQuality = 64
        },
        new EncoderProfile
        {
            Name = "FLAC (可逆圧縮)",
            Engine = "ffmpeg",
            Extension = ".flac",
            Codec = "flac",
            Mode = "none",
            QualityOptions = Array.Empty<object>(),
            DefaultQuality = 0
        },
        new EncoderProfile
        {
            Name = "ALAC (Apple Lossless / .m4a)",
            Engine = "ffmpeg",
            Extension = ".m4a",
            Codec = "alac",
            Mode = "none",
            QualityOptions = Array.Empty<object>(),
            DefaultQuality = 0
        },
        new EncoderProfile
        {
            Name = "WavPack (可逆圧縮 / .wv)",
            Engine = "ffmpeg",
            Extension = ".wv",
            Codec = "wavpack",
            Mode = "none",
            QualityOptions = Array.Empty<object>(),
            DefaultQuality = 0
        }
    };
}
