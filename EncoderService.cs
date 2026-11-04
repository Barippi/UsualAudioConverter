using System.Diagnostics;
using System.IO;

namespace UsualAudioConverter;

public static class EncoderService
{
    public static async Task EncodeAsync(
        AudioItem item,
        string ffmpegPath,
        string exhalePath,
        EncoderProfile profile,
        object qualityValue,
        IProgress<string> progress,
        CancellationToken ct)
    {
        string tempDir = Path.GetTempPath();
        string tempOut = Path.Combine(tempDir, $"{Guid.NewGuid()}{profile.Extension}");

        try
        {
            if (profile.Engine == "ffmpeg")
            {
                var args = $"-y -nostdin -i \"{item.SourcePath}\" -map 0:a:0 -c:a {profile.Codec} ";

                // プロファイルごとの品質 / レート指定（VBR / ABR）
                switch (profile.Mode)
                {
                    case "lame_vbr":
                        string qStr = qualityValue.ToString() ?? "V2";
                        int vbrNum = 2;
                        if (qStr.StartsWith("V") && char.IsDigit(qStr[1]))
                            vbrNum = qStr[1] - '0';
                        args += $"-q:a {vbrNum} ";
                        break;

                    case "fdk_vbr":
                        string fdkStr = qualityValue.ToString() ?? "5";
                        int fdkMode = 5;
                        int idx = fdkStr.IndexOf("Mode ");
                        if (idx >= 0 && idx + 5 < fdkStr.Length && char.IsDigit(fdkStr[idx + 5]))
                            fdkMode = fdkStr[idx + 5] - '0';
                        args += $"-vbr {fdkMode} ";
                        break;

                    case "vorbis_vbr":
                        string vorbisStr = qualityValue.ToString() ?? "q6";
                        int qLevel = 6;
                        if (vorbisStr.StartsWith("q") && char.IsDigit(vorbisStr[1]))
                        {
                            if (vorbisStr.StartsWith("q10")) qLevel = 10;
                            else qLevel = vorbisStr[1] - '0';
                        }
                        args += $"-q:a {qLevel} ";
                        break;

                    case "bitrate":
                        int kbps = qualityValue is int k ? k : 128;
                        args += $"-b:a {kbps}k ";
                        if (profile.Codec == "libopus")
                        {
                            // Opus はデフォルトでVBR、音楽向けモードを明示
                            args += "-vbr on -application audio ";
                        }
                        break;

                    case "none":
                        // ロスレス形式 (FLAC / ALAC / WavPack)
                        break;
                }

                if (!string.IsNullOrEmpty(profile.Profile))
                    args += $"-profile:a {profile.Profile} ";

                if (profile.Profile is "aac_he" or "aac_he_v2")
                    args += "-ac 2 -ar 44100 ";

                if (profile.Extension == ".m4a")
                    args += "-map 0:v? -c:v copy -disposition:v:0 attached_pic -map_metadata 0 -movflags +faststart ";
                else if (profile.Extension == ".mp3")
                    args += "-map 0:v? -c:v copy -id3v2_version 3 -map_metadata 0 ";
                else if (profile.Extension is ".flac" or ".wv")
                    args += "-map 0:v? -c:v copy -map_metadata 0 ";
                else
                    args += "-vn -map_metadata 0 ";

                args += $"\"{tempOut}\"";

                await RunProcessAsync(ffmpegPath, args, ct, progress);
            }
            else if (profile.Engine == "exhale")
            {
                string exhaleRawOut = Path.Combine(tempDir, $"{Guid.NewGuid()}_raw.m4a");

                try
                {
                    int kbps = qualityValue is int k ? k : 64;
                    string preset = kbps switch
                    {
                        <= 24 => "a",
                        <= 36 => "b",
                        <= 48 => "c",
                        <= 64 => "1",
                        <= 80 => "2",
                        <= 96 => "3",
                        <= 112 => "4",
                        <= 128 => "5",
                        <= 144 => "6",
                        <= 160 => "7",
                        <= 176 => "8",
                        _ => "9"
                    };

                    progress.Report($"xHEパイプ変換中({preset})");
                    if (File.Exists(exhaleRawOut)) File.Delete(exhaleRawOut);

                    // cmd /c 経由で ffmpeg の stdout (WAV) を exhale の stdin へ直接流し込む
                    // 中間 WAV ファイルのディスク書き込みを完全スキップ
                    string pipeArgs = $"/c \"\"{ffmpegPath}\" -y -nostdin -i \"{item.SourcePath}\" -vn -ac 2 -ar 44100 -f wav - | \"{exhalePath}\" {preset} \"{exhaleRawOut}\"\"";
                    await RunProcessAsync("cmd.exe", pipeArgs, ct, null);

                    progress.Report("メタデータ転送中");
                    if (File.Exists(tempOut)) File.Delete(tempOut);

                    string tagArgs = $"-y -nostdin -i \"{exhaleRawOut}\" -i \"{item.SourcePath}\" " +
                                     $"-map 0:a -map 1:v? -c copy -disposition:v:0 attached_pic " +
                                     $"-map_metadata 1 -movflags +faststart \"{tempOut}\"";
                    await RunProcessAsync(ffmpegPath, tagArgs, ct, null);
                }
                finally
                {
                    if (File.Exists(exhaleRawOut)) { try { File.Delete(exhaleRawOut); } catch { } }
                }
            }

            string? destDir = Path.GetDirectoryName(item.DestinationPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            if (File.Exists(item.DestinationPath))
                File.Delete(item.DestinationPath);

            File.Move(tempOut, item.DestinationPath);

            item.Status = "完了";
            progress.Report("完了");
        }
        catch (OperationCanceledException)
        {
            item.Status = "中断";
            throw;
        }
        catch (Exception ex)
        {
            item.Status = "失敗";
            item.ErrorLog = ex.Message;
            throw;
        }
        finally
        {
            if (File.Exists(tempOut))
            {
                try { File.Delete(tempOut); } catch { }
            }
        }
    }

    private static async Task RunProcessAsync(string exe, string args, CancellationToken ct, IProgress<string>? progress)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var readErrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        string err = await readErrTask;

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException($"プロセス異常終了 (ExitCode: {proc.ExitCode}):\n{err}");
        }
    }
}
