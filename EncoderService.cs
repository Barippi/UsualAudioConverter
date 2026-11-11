using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UsualAudioConverter;

public static class EncoderService
{
    private static bool? _hasLibFdkAac;
    private static readonly SemaphoreSlim _fdkCheckLock = new(1, 1);

    public static async Task<bool> CheckLibFdkSupportAsync(string ffmpegPath, CancellationToken ct = default)
    {
        if (_hasLibFdkAac.HasValue) return _hasLibFdkAac.Value;

        await _fdkCheckLock.WaitAsync(ct);
        try
        {
            if (_hasLibFdkAac.HasValue) return _hasLibFdkAac.Value;

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-encoders");

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
            if (profile.Mode == "fdk_vbr" || (profile.Codec == "libfdk_aac" && profile.Mode == "bitrate"))
            {
                bool hasLibFdk = await CheckLibFdkSupportAsync(ffmpegPath, ct);
                bool hasFdkaacCli = !string.IsNullOrEmpty(fdkaacPath) && (File.Exists(fdkaacPath) || !fdkaacPath.Contains(Path.DirectorySeparatorChar));

                if (!hasLibFdk && hasFdkaacCli)
                {
                    await EncodeWithFdkaacCliAsync(item, ffmpegPath, fdkaacPath, profile, qualityValue, tempOut, progress, ct);
                    FinalizeOutputFile(item, tempOut, progress);
                    return;
                }
                else if (!hasLibFdk && !hasFdkaacCli)
                {
                    await EncodeWithNativeAacAsync(item, ffmpegPath, profile, qualityValue, tempOut, progress, ct);
                    FinalizeOutputFile(item, tempOut, progress);
                    return;
                }
            }

            if (profile.Engine == "ffmpeg")
            {
                var args = new List<string> { "-y", "-nostdin", "-i", item.SourcePath, "-map", "0:a:0", "-c:a", profile.Codec ?? "copy" };

                switch (profile.Mode)
                {
                    case "lame_vbr":
                        string qStr = qualityValue.ToString() ?? "V2";
                        int vbrNum = 2;
                        if (qStr.StartsWith("V") && char.IsDigit(qStr[1]))
                            vbrNum = qStr[1] - '0';
                        args.Add("-q:a");
                        args.Add(vbrNum.ToString());
                        break;

                    case "fdk_vbr":
                        string fdkStr = qualityValue.ToString() ?? "5";
                        int fdkMode = 5;
                        int idx = fdkStr.IndexOf("Mode ");
                        if (idx >= 0 && idx + 5 < fdkStr.Length && char.IsDigit(fdkStr[idx + 5]))
                            fdkMode = fdkStr[idx + 5] - '0';
                        args.Add("-vbr");
                        args.Add(fdkMode.ToString());
                        break;

                    case "vorbis_vbr":
                        string vorbisStr = qualityValue.ToString() ?? "q6";
                        int qLevel = 6;
                        if (vorbisStr.StartsWith("q") && char.IsDigit(vorbisStr[1]))
                        {
                            if (vorbisStr.StartsWith("q10")) qLevel = 10;
                            else qLevel = vorbisStr[1] - '0';
                        }
                        args.Add("-q:a");
                        args.Add(qLevel.ToString());
                        break;

                    case "bitrate":
                        int kbps = qualityValue is int k ? k : 128;
                        args.Add("-b:a");
                        args.Add($"{kbps}k");
                        if (profile.Codec == "libopus")
                        {
                            args.Add("-vbr");
                            args.Add("on");
                            args.Add("-application");
                            args.Add("audio");
                        }
                        break;

                    case "none":
                        break;
                }

                if (!string.IsNullOrEmpty(profile.Profile))
                {
                    args.Add("-profile:a");
                    args.Add(profile.Profile);
                }

                if (profile.Profile is "aac_he" or "aac_he_v2")
                {
                    args.AddRange(new[] { "-ac", "2", "-ar", "44100" });
                }

                if (profile.Extension == ".m4a")
                {
                    args.AddRange(new[] { "-map", "0:v?", "-c:v", "copy", "-disposition:v:0", "attached_pic", "-map_metadata", "0", "-movflags", "+faststart" });
                }
                else if (profile.Extension == ".mp3")
                {
                    args.AddRange(new[] { "-map", "0:v?", "-c:v", "copy", "-id3v2_version", "3", "-map_metadata", "0" });
                }
                else if (profile.Extension is ".flac" or ".wv")
                {
                    args.AddRange(new[] { "-map", "0:v?", "-c:v", "copy", "-map_metadata", "0" });
                }
                else
                {
                    args.AddRange(new[] { "-vn", "-map_metadata", "0" });
                }

                args.Add(tempOut);

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
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    ffPsi.ArgumentList.Add("-y");
                    ffPsi.ArgumentList.Add("-nostdin");
                    ffPsi.ArgumentList.Add("-i");
                    ffPsi.ArgumentList.Add(item.SourcePath);
                    ffPsi.ArgumentList.Add("-vn");
                    ffPsi.ArgumentList.Add("-ac");
                    ffPsi.ArgumentList.Add("2");
                    ffPsi.ArgumentList.Add("-ar");
                    ffPsi.ArgumentList.Add("44100");
                    ffPsi.ArgumentList.Add("-f");
                    ffPsi.ArgumentList.Add("wav");
                    ffPsi.ArgumentList.Add("-");

                    var exPsi = new ProcessStartInfo
                    {
                        FileName = exhalePath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true
                    };
                    exPsi.ArgumentList.Add(preset);
                    exPsi.ArgumentList.Add(exhaleRawOut);

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

                    var tagArgs = new List<string>
                    {
                        "-y", "-nostdin",
                        "-i", exhaleRawOut,
                        "-i", item.SourcePath,
                        "-map", "0:a",
                        "-map", "1:v?",
                        "-c", "copy",
                        "-disposition:v:0", "attached_pic",
                        "-map_metadata", "1",
                        "-movflags", "+faststart",
                        tempOut
                    };
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
            var fdkArgs = new List<string>();

            if (profile.Profile == "aac_he")
            {
                fdkArgs.Add("-p");
                fdkArgs.Add("5");
            }
            else if (profile.Profile == "aac_he_v2")
            {
                fdkArgs.Add("-p");
                fdkArgs.Add("29");
            }
            else
            {
                fdkArgs.Add("-p");
                fdkArgs.Add("2");
            }

            if (profile.Mode == "fdk_vbr")
            {
                string fdkStr = qualityValue.ToString() ?? "5";
                int fdkMode = 5;
                int idx = fdkStr.IndexOf("Mode ");
                if (idx >= 0 && idx + 5 < fdkStr.Length && char.IsDigit(fdkStr[idx + 5]))
                    fdkMode = fdkStr[idx + 5] - '0';
                fdkArgs.Add("-m");
                fdkArgs.Add(fdkMode.ToString());
            }
            else
            {
                int kbps = qualityValue is int k ? k : 64;
                fdkArgs.Add("-b");
                fdkArgs.Add(kbps.ToString());
            }

            fdkArgs.Add("-o");
            fdkArgs.Add(rawM4a);
            fdkArgs.Add("-");

            progress.Report("fdkaacパイプ変換中");

            var ffPsi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            ffPsi.ArgumentList.Add("-y");
            ffPsi.ArgumentList.Add("-nostdin");
            ffPsi.ArgumentList.Add("-i");
            ffPsi.ArgumentList.Add(item.SourcePath);
            ffPsi.ArgumentList.Add("-vn");
            ffPsi.ArgumentList.Add("-ac");
            ffPsi.ArgumentList.Add("2");
            ffPsi.ArgumentList.Add("-ar");
            ffPsi.ArgumentList.Add("44100");
            ffPsi.ArgumentList.Add("-f");
            ffPsi.ArgumentList.Add("wav");
            ffPsi.ArgumentList.Add("-");

            var fdkPsi = new ProcessStartInfo
            {
                FileName = fdkaacPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };
            foreach (var arg in fdkArgs)
            {
                fdkPsi.ArgumentList.Add(arg);
            }

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

            progress.Report("メタデータ転送中");
            var tagArgs = new List<string>
            {
                "-y", "-nostdin",
                "-i", rawM4a,
                "-i", item.SourcePath,
                "-map", "0:a",
                "-map", "1:v?",
                "-c", "copy",
                "-disposition:v:0", "attached_pic",
                "-map_metadata", "1",
                "-movflags", "+faststart",
                finalTempOut
            };
            await RunProcessAsync(ffmpegPath, tagArgs, ct, null);
        }
        finally
        {
            if (File.Exists(rawM4a)) { try { File.Delete(rawM4a); } catch { } }
        }
    }

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

        var args = new List<string>
        {
            "-y", "-nostdin",
            "-i", item.SourcePath,
            "-map", "0:a:0",
            "-c:a", "aac",
            "-b:a", $"{kbps}k",
            "-map", "0:v?",
            "-c:v", "copy",
            "-disposition:v:0", "attached_pic",
            "-map_metadata", "0",
            "-movflags", "+faststart",
            finalTempOut
        };

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

    private static async Task RunProcessAsync(string exe, IEnumerable<string> args, CancellationToken ct, IProgress<string>? progress)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

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
