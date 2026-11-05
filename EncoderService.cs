using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UsualAudioConverter;

public static class EncoderService
{
    private static bool? _hasLibFdkAac;
    private static readonly SemaphoreSlim _fdkCheckLock = new(1, 1);

    /// <summary>
    /// FFmpeg が libfdk_aac をサポートしているか判定（結果をキャッシュ）
    /// </summary>
    public static async Task<bool> CheckLibFdkSupportAsync(string ffmpegPath, CancellationToken ct = default)
    {
        if (_hasLibFdkAac.HasValue) return _hasLibFdkAac.Value;

        await _fdkCheckLock.WaitAsync(ct);
        try
        {
            if (_hasLibFdkAac.HasValue) return _hasLibFdkAac.Value;

            if (!File.Exists(ffmpegPath) && !string.Equals(ffmpegPath, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            {
                _hasLibFdkAac = false;
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-encoders",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                string output = await proc.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);
                _hasLibFdkAac = output.Contains("libfdk_aac");
            }
            else
            {
                _hasLibFdkAac = false;
            }
        }
        catch
        {
            _hasLibFdkAac = false;
        }
        finally
        {
            _fdkCheckLock.Release();
        }

        return _hasLibFdkAac.Value;
    }

    public static void ResetFdkCache()
    {
        _hasLibFdkAac = null;
    }

    public static async Task EncodeAsync(
        AudioItem item,
        string ffmpegPath,
        string exhalePath,
        string fdkaacPath,
        EncoderProfile profile,
        object qualityValue,
        IProgress<string> progress,
        CancellationToken ct)
    {
        string tempDir = Path.GetTempPath();
        string tempOut = Path.Combine(tempDir, $"{Guid.NewGuid()}{profile.Extension}");

        try
        {
            // --- AAC-LC / HE-AAC の自動フォールバック判定 ---
            if (profile.Mode == "fdk_vbr" || (profile.Codec == "libfdk_aac" && profile.Mode == "bitrate"))
            {
                bool hasLibFdk = await CheckLibFdkSupportAsync(ffmpegPath, ct);
                bool hasFdkaacCli = !string.IsNullOrEmpty(fdkaacPath) && File.Exists(fdkaacPath);

                if (!hasLibFdk && hasFdkaacCli)
                {
                    // 分岐2: FFmpegにlibfdk_aacがないが、fdkaac.exeがある -> パイプ処理
                    await EncodeWithFdkaacCliAsync(item, ffmpegPath, fdkaacPath, profile, qualityValue, tempOut, progress, ct);
                    FinalizeOutputFile(item, tempOut, progress);
                    return;
                }
                else if (!hasLibFdk && !hasFdkaacCli)
                {
                    // 分岐3: どちらもない -> FFmpeg 内蔵 aac にフォールバック
                    await EncodeWithNativeAacAsync(item, ffmpegPath, profile, qualityValue, tempOut, progress, ct);
                    FinalizeOutputFile(item, tempOut, progress);
                    return;
                }
                // 分岐1: libfdk_aac がある場合はそのまま下の通常の FFmpeg 単体処理へ進む
            }

            // --- 通常の FFmpeg / exhale 処理 ---
            if (profile.Engine == "ffmpeg")
            {
                var args = $"-y -nostdin -i \"{item.SourcePath}\" -map 0:a:0 -c:a {profile.Codec} ";

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
                            args += "-vbr on -application audio ";
                        }
                        break;

                    case "none":
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

                    var ffPsi = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-y -nostdin -i \"{item.SourcePath}\" -vn -ac 2 -ar 44100 -f wav -",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    var exPsi = new ProcessStartInfo
                    {
                        FileName = exhalePath,
                        Arguments = $"{preset} \"{exhaleRawOut}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true
                    };

                    using (var procFf = new Process { StartInfo = ffPsi })
                    using (var procEx = new Process { StartInfo = exPsi })
                    {
                        procFf.Start();
                        procEx.Start();

                        var pipeTask = Task.Run(async () =>
                        {
                            try
                            {
                                await procFf.StandardOutput.BaseStream.CopyToAsync(procEx.StandardInput.BaseStream, ct);
                            }
                            finally
                            {
                                procEx.StandardInput.Close();
                            }
                        }, ct);

                        var ffErrTask = procFf.StandardError.ReadToEndAsync(ct);
                        var exErrTask = procEx.StandardError.ReadToEndAsync(ct);

                        await Task.WhenAll(procFf.WaitForExitAsync(ct), procEx.WaitForExitAsync(ct), pipeTask);

                        if (procFf.ExitCode != 0)
                        {
                            string ffErr = await ffErrTask;
                            throw new InvalidOperationException($"FFmpeg異常終了 (ExitCode: {procFf.ExitCode}):\n{ffErr}");
                        }
                        if (procEx.ExitCode != 0)
                        {
                            string exErr = await exErrTask;
                            throw new InvalidOperationException($"exhale異常終了 (ExitCode: {procEx.ExitCode}):\n{exErr}");
                        }
                    }

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

            FinalizeOutputFile(item, tempOut, progress);
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

    /// <summary>
    /// 分岐2: FFmpeg (WAVパイプ出力) -> fdkaac.exe (stdin) -> FFmpeg タグ・アート転送
    /// </summary>
    private static async Task EncodeWithFdkaacCliAsync(
        AudioItem item,
        string ffmpegPath,
        string fdkaacPath,
        EncoderProfile profile,
        object qualityValue,
        string finalTempOut,
        IProgress<string> progress,
        CancellationToken ct)
    {
        string rawM4a = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_fdkraw.m4a");

        try
        {
            string fdkArgs = string.Empty;

            if (profile.Profile == "aac_he")
                fdkArgs += "-p 5 "; // HE-AAC v1
            else if (profile.Profile == "aac_he_v2")
                fdkArgs += "-p 29 "; // HE-AAC v2
            else
                fdkArgs += "-p 2 "; // AAC-LC

            if (profile.Mode == "fdk_vbr")
            {
                string fdkStr = qualityValue.ToString() ?? "5";
                int fdkMode = 5;
                int idx = fdkStr.IndexOf("Mode ");
                if (idx >= 0 && idx + 5 < fdkStr.Length && char.IsDigit(fdkStr[idx + 5]))
                    fdkMode = fdkStr[idx + 5] - '0';
                fdkArgs += $"-m {fdkMode} ";
            }
            else
            {
                int kbps = qualityValue is int k ? k : 64;
                fdkArgs += $"-b {kbps} ";
            }

            fdkArgs += $"-o \"{rawM4a}\" -";

            progress.Report("fdkaacパイプ変換中");

            var ffPsi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -nostdin -i \"{item.SourcePath}\" -vn -ac 2 -ar 44100 -f wav -",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var fdkPsi = new ProcessStartInfo
            {
                FileName = fdkaacPath,
                Arguments = fdkArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            using (var procFf = new Process { StartInfo = ffPsi })
            using (var procFdk = new Process { StartInfo = fdkPsi })
            {
                procFf.Start();
                procFdk.Start();

                var pipeTask = Task.Run(async () =>
                {
                    try
                    {
                        await procFf.StandardOutput.BaseStream.CopyToAsync(procFdk.StandardInput.BaseStream, ct);
                    }
                    finally
                    {
                        procFdk.StandardInput.Close();
                    }
                }, ct);

                var ffErrTask = procFf.StandardError.ReadToEndAsync(ct);
                var fdkErrTask = procFdk.StandardError.ReadToEndAsync(ct);

                await Task.WhenAll(procFf.WaitForExitAsync(ct), procFdk.WaitForExitAsync(ct), pipeTask);

                if (procFf.ExitCode != 0)
                {
                    string ffErr = await ffErrTask;
                    throw new InvalidOperationException($"FFmpeg異常終了 (ExitCode: {procFf.ExitCode}):\n{ffErr}");
                }
                if (procFdk.ExitCode != 0)
                {
                    string fdkErr = await fdkErrTask;
                    throw new InvalidOperationException($"fdkaac異常終了 (ExitCode: {procFdk.ExitCode}):\n{fdkErr}");
                }
            }

            // メタデータ・アートワーク転送
            progress.Report("メタデータ転送中");
            string tagArgs = $"-y -nostdin -i \"{rawM4a}\" -i \"{item.SourcePath}\" " +
                             $"-map 0:a -map 1:v? -c copy -disposition:v:0 attached_pic " +
                             $"-map_metadata 1 -movflags +faststart \"{finalTempOut}\"";
            await RunProcessAsync(ffmpegPath, tagArgs, ct, null);
        }
        finally
        {
            if (File.Exists(rawM4a)) { try { File.Delete(rawM4a); } catch { } }
        }
    }

    /// <summary>
    /// 分岐3: FFmpeg 内蔵 aac エンコーダーにフォールバック (AAC-LC のみ)
    /// </summary>
    private static async Task EncodeWithNativeAacAsync(
        AudioItem item,
        string ffmpegPath,
        EncoderProfile profile,
        object qualityValue,
        string finalTempOut,
        IProgress<string> progress,
        CancellationToken ct)
    {
        progress.Report("内蔵AACフォールバック");

        int kbps = 192;
        if (profile.Mode == "fdk_vbr")
        {
            string fdkStr = qualityValue.ToString() ?? "5";
            kbps = fdkStr.Contains("Mode 5") ? 256 :
                   fdkStr.Contains("Mode 4") ? 192 :
                   fdkStr.Contains("Mode 3") ? 128 :
                   fdkStr.Contains("Mode 2") ? 96 : 64;
        }
        else if (qualityValue is int k)
        {
            kbps = k;
        }

        string args = $"-y -nostdin -i \"{item.SourcePath}\" -map 0:a:0 -c:a aac -b:a {kbps}k " +
                      $"-map 0:v? -c:v copy -disposition:v:0 attached_pic -map_metadata 0 -movflags +faststart \"{finalTempOut}\"";

        await RunProcessAsync(ffmpegPath, args, ct, progress);
    }

    private static void FinalizeOutputFile(AudioItem item, string tempOut, IProgress<string> progress)
    {
        string? destDir = Path.GetDirectoryName(item.DestinationPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        if (File.Exists(item.DestinationPath))
            File.Delete(item.DestinationPath);

        File.Move(tempOut, item.DestinationPath);

        item.Status = "完了";
        progress.Report("完了");
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
