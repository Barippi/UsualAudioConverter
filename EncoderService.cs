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
        int bitrateKbps,
        IProgress<string> progress,
        CancellationToken ct)
    {
        string tempDir = Path.GetTempPath();
        string tempOut = Path.Combine(tempDir, $"{Guid.NewGuid()}{profile.Extension}");

        try
        {
            if (profile.Engine == "ffmpeg")
            {
                var args = $"-y -nostdin -i \"{item.SourcePath}\" -map 0:a:0 -c:a {profile.Codec} -b:a {bitrateKbps}k ";
                if (!string.IsNullOrEmpty(profile.Profile)) args += $"-profile:a {profile.Profile} ";
                if (profile.Profile is "aac_he" or "aac_he_v2") args += "-ac 2 -ar 44100 ";

                if (profile.Extension == ".m4a")
                    args += "-map 0:v? -c:v copy -disposition:v:0 attached_pic -map_metadata 0 -movflags +faststart ";
                else if (profile.Extension == ".mp3")
                    args += "-map 0:v? -c:v copy -id3v2_version 3 -map_metadata 0 ";
                else
                    args += "-vn -map_metadata 0 ";

                args += $"\"{tempOut}\"";

                await RunProcessAsync(ffmpegPath, args, ct, progress);
            }
            else if (profile.Engine == "exhale")
            {
                // exhale はパイプ入力不可・事前出力ファイル存在不可のため、中間WAVを実体出力して渡す
                string tempWav = Path.Combine(tempDir, $"{Guid.NewGuid()}.wav");
                try
                {
                    progress.Report("WAV展開中");
                    string wavArgs = $"-y -nostdin -i \"{item.SourcePath}\" -vn -ac 2 -ar 44100 -c:a pcm_s16le \"{tempWav}\"";
                    await RunProcessAsync(ffmpegPath, wavArgs, ct, null);

                    string preset = bitrateKbps switch
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

                    progress.Report($"xHE変換中({preset})");
                    // exhale は既存ファイルが存在するとエラー終了するため、事前に削除
                    if (File.Exists(tempOut)) File.Delete(tempOut);

                    string exArgs = $"{preset} \"{tempWav}\" \"{tempOut}\"";
                    await RunProcessAsync(exhalePath, exArgs, ct, null);
                }
                finally
                {
                    if (File.Exists(tempWav))
                    {
                        try { File.Delete(tempWav); } catch { }
                    }
                }
            }

            // 最終保存先へ安全移動
            string? destDir = Path.GetDirectoryName(item.DestinationPath);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
            if (File.Exists(item.DestinationPath)) File.Delete(item.DestinationPath);

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
