# UsualAudioConverter

[日本語](#日本語) | [English](#english)

## 日本語

**UsualAudioConverter** は、FFmpeg、exhale、fdkaac を活用した高速・軽量な並列オーディオバッチ変換ツールです。
Windows 専用の WPF 版（v1.1.6）と、Windows / Linux に対応したクロスプラットフォームの Avalonia 版（v1.2.5）を提供しています（いずれも最新 LTS の **.NET 10** ベースで動作します）。

### 主な特徴

* **多彩な音声フォーマット対応**:

  * **ロッシー (VBR/ABR)**: MP3 (LAME VBR), AAC-LC (libfdk_aac / 内蔵 AAC フォールバック), HE-AAC v1/v2, Ogg Vorbis, Opus, xHE-AAC / USAC (exhale VBR)

  * **ロスレス**: FLAC, ALAC (.m4a), WavPack (.wv)

* **並列バッチ処理**: CPU コア数に応じたマルチスレッド同時変換に対応。

* **メタデータ＆カバーアートの完全維持**: アルバムアート、ID3v2 / MP4 タグ、同階層の関連ファイル（画像、cue、log）の自動引き継ぎ。

* **柔軟な出力先・階層維持**: 元ファイルと同じフォルダまたは任意フォルダへの出力、元ディレクトリ階層の保持。

* **外部ツールパスの永続化**: `ffmpeg`, `exhale`, `fdkaac` の実行ファイルパスを `%AppData%` または `~/.config` に自動保存し、次回起動時も自動ロード。

* **複数フォルダの一括選択**: ファイルダイアログから `Ctrl` / `Shift` キーで複数の音源フォルダをまとめて登録可能。

* **単一実行ファイル配布**: 依存ランタイム・デバッグ情報を内包した自己完結型の単一バイナリとしてビルド可能。

### 動作環境

| エディション | 対象 OS | フレームワーク / UI | 
| ----- | ----- | ----- | 
| **Avalonia 版 (v1.2.5)** | Windows / Linux (Ubuntu, KDE, Xfce 等) | .NET 10 / Avalonia 11 | 
| **WPF 版 (v1.1.6)** | Windows 10 / 11 (x64) | .NET 10 / WPF | 

### 外部依存ツール

本ツールの変換機能を利用するには、以下のバイナリが必要です。アプリ内の「⚙ 外部ツール設定」から各ツールのパスを指定してください（一度設定すると保持されます）。

1. **FFmpeg** (`ffmpeg` / `ffmpeg.exe`): 必須。

2. **exhale** (`exhale` / `exhale.exe`): xHE-AAC 変換時に必要（任意）。

3. **fdkaac** (`fdkaac` / `fdkaac.exe`): Linux 標準環境など FFmpeg に `libfdk_aac` が組み込まれていない場合にパイプライン処理で利用（任意）。

### ビルド手順

#### Avalonia 版 (Cross-platform / .NET 10)

```bash
# ビルド & 実行
dotnet build
dotnet run

# 単一ファイル自己完結バイナリの発行 (Linux x64)
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o ./publish/linux

# 単一ファイル自己完結バイナリの発行 (Windows x64)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o ./publish/win
```

#### WPF 版 (Windows 専用 / .NET 10)

```powershell
# ビルド & 実行
dotnet build
dotnet run

# 単一ファイル自己完結バイナリの発行
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o ./publish
```

### ライセンス

本プロジェクトは [MIT License](LICENSE) のもとで公開されています。

---

## English

**UsualAudioConverter** is a lightweight, high-performance parallel audio batch converter powered by FFmpeg, exhale, and fdkaac.
Available as a Windows-native WPF edition (v1.1.6) and a cross-platform Avalonia edition (v1.2.5) supporting both Windows and Linux, both powered by **.NET 10 (LTS)**.

### Key Features

* **Broad Audio Format Support**:

  * **Lossy (VBR/ABR)**: MP3 (LAME VBR), AAC-LC (libfdk_aac / native FFmpeg AAC fallback), HE-AAC v1/v2, Ogg Vorbis, Opus, xHE-AAC / USAC (exhale VBR).

  * **Lossless**: FLAC, ALAC (.m4a), WavPack (.wv).

* **Parallel Batch Processing**: Multi-threaded concurrent processing optimized for your CPU cores.

* **Preserve Metadata & Artwork**: Seamlessly preserves album artwork, ID3v2/MP4 tags, and copies companion files (images, .cue, .log) automatically.

* **Directory Structure Retention**: Output to source folders or a custom directory while maintaining the original nested folder layout.

* **Tool Path Persistence**: Executable paths for `ffmpeg`, `exhale`, and `fdkaac` are automatically saved to `%AppData%` or `~/.config` and restored across sessions.

* **Multi-Folder Selection**: Easily import multiple albums/folders simultaneously using `Ctrl` / `Shift` via the native directory picker.

* **Single-File Distribution**: Compiles into a self-contained single executable with embedded runtime and symbols.

### System Requirements

| Edition | Supported OS | Framework / UI | 
| ----- | ----- | ----- | 
| **Avalonia Edition (v1.2.5)** | Windows / Linux (Ubuntu, KDE, Xfce, etc.) | .NET 10 / Avalonia 11 | 
| **WPF Edition (v1.1.6)** | Windows 10 / 11 (x64) | .NET 10 / WPF | 

### External Dependencies

This tool requires the following command-line binaries. Set their locations via "⚙ Settings" inside the application (paths are saved automatically):

1. **FFmpeg** (`ffmpeg` / `ffmpeg.exe`): Required for core decoding, encoding, and tagging.

2. **exhale** (`exhale` / `exhale.exe`): Required for xHE-AAC / USAC encoding (optional).

3. **fdkaac** (`fdkaac` / `fdkaac.exe`): Optional CLI wrapper used as a pipeline fallback when FFmpeg lacks `libfdk_aac` support (e.g., standard Linux package builds).

### Build Instructions

#### Avalonia Edition (Cross-platform / .NET 10)

```bash
# Build & Run
dotnet build
dotnet run

# Publish single-file binary for Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o ./publish/linux

# Publish single-file binary for Windows x64
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o ./publish/win
```

#### WPF Edition (Windows Only / .NET 10)

```powershell
# Build & Run
dotnet build
dotnet run

# Publish self-contained single-file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o ./publish
```

### License

This project is licensed under the [MIT License](LICENSE).