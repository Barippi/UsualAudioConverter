# Usual Audio Converter

A high-performance Windows batch audio converter optimized for massive music libraries, built with C# (.NET 8) and WPF. Supports xHE-AAC (via exhale) and various lossy formats (via FFmpeg).

大量の音楽ライブラリ（数万曲規模）を高速に一括変換するために設計された Windows 向け WPF アプリケーションです。

---

## Features / 主な特徴

- **UI Virtualization (`VirtualizingStackPanel`)**
  - Handles tens of thousands of queued tracks with near-zero memory footprint and smooth scrolling.
  - 数万件のファイルを登録してもメモリを圧迫せず、即座に追加・スクロール可能。
- **Strict exhale (xHE-AAC) Integration**
  - Full adherence to exhale requirements: temporary PCM WAV extraction, avoidance of pre-allocated output collisions, and automatic preset mapping (`a`-`g` for low bitrates, `0`-`9` for standard rates).
  - exhale の制約（標準入力パイプ非対応・出力先同名ファイル上書き拒否）を遵守し、中間 WAV 経由の 2 段階処理で確実にエンコード。
- **Fast O(1) Common Root Calculation**
  - Instant directory structure preservation using head-and-tail path sampling.
  - 先頭と末尾のパス比較により、大量ファイルでも一瞬で共通ルートフォルダを特定し、階層構造を維持して保存。
- **Native Parallelism**
  - Powered by `Parallel.ForEachAsync` with automatic Windows thread pool balancing.
  - Windows の論理コア数に応じた並列ワーカー制御。

---

## Supported Formats / 対応フォーマット

| Format / Profile | Engine | Required Binary | Target Bitrates |
| :--- | :--- | :--- | :--- |
| **xHE-AAC** | `exhale.exe` + `ffmpeg.exe` | exhale, FFmpeg | 24 kbps - 256 kbps |
| **AAC-LC** | `ffmpeg.exe` (`libfdk_aac`) | FFmpeg | 96 kbps - 320 kbps |
| **HE-AAC v1** | `ffmpeg.exe` (`libfdk_aac`) | FFmpeg | 48 kbps - 96 kbps |
| **HE-AAC v2** | `ffmpeg.exe` (`libfdk_aac`) | FFmpeg | 24 kbps - 48 kbps |
| **MP3** | `ffmpeg.exe` (`libmp3lame`) | FFmpeg | 128 kbps - 320 kbps |
| **Ogg Vorbis** | `ffmpeg.exe` (`libvorbis`) | FFmpeg | 96 kbps - 320 kbps |
| **Opus** | `ffmpeg.exe` (`libopus`) | FFmpeg | 48 kbps - 192 kbps |

*Note: Binaries (`ffmpeg.exe` and `exhale.exe`) are not bundled. Please specify their paths in the settings dialog.*

> ⚠️ **Important FFmpeg Requirement:**  
> Since this tool supports AAC encoding via `libfdk_aac`, you must use an FFmpeg binary compiled with `--enable-nonfree` (and `--enable-gpl`). Standard LGPL builds of FFmpeg do not include `libfdk_aac` due to licensing restrictions.

*注意: `ffmpeg.exe` および `exhale.exe` は同梱されていません。設定画面からそれぞれのパスを指定してください。*

> ⚠️ **FFmpegに関する重要なお知らせ:**  
> 本ツールは `libfdk_aac` を使用したAACエンコードに対応しているため、`--enable-nonfree`（および `--enable-gpl`）を有効にしてビルドされたFFmpegバイナリが必要です。一般的なLGPL版のFFmpegバイナリには、ライセンスの制約上 `libfdk_aac` が含まれていませんのでご注意ください。

### FFmpeg with `libfdk_aac` Requirement
AAC-LC and HE-AAC profiles require an FFmpeg binary compiled with `--enable-libfdk-aac` (non-free). 

Due to licensing restrictions, pre-built binaries containing `libfdk_aac` cannot be distributed publicly. If you need to build your own custom FFmpeg binary on Windows, we recommend using:
- **[media-autobuild_suite](https://github.com/m-ab-s/media-autobuild_suite)** (An automated Windows compiler environment for FFmpeg)

*Note: For formats like xHE-AAC (`exhale.exe`), MP3, Opus, or Ogg Vorbis, standard public FFmpeg builds work out of the box without special flags.*
---

## Build Instructions / ビルド手順

### Prerequisites
- Windows 10 / 11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Single-File Publish
```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
