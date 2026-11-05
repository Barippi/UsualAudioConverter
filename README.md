# Usual Audio Converter

[![Release](https://img.shields.io/github/v/release/Barippi/UsualAudioConverter?style=flat-square)](https://github.com/Barippi/UsualAudioConverter/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg?style=flat-square)](https://dotnet.microsoft.com/)

[English](#english) | [日本語](#japanese)

---

## 日本語 (Japanese)

Usual Audio Converter は、**FFmpeg**、**exhale**、および **fdkaac** をバックエンドに採用した、Windows 向けの軽量かつ高速なバッチ音声変換デスクトップアプリケーション（WPF / C# / .NET 8）です。

日常的な音楽ライブラリの省容量化・高品質化から、アーカイブ向けのロスレス変換まで、主要なオーディオコーデックを網羅し、直感的な操作で一括変換できます。

### 主な特徴

* **柔軟な AAC エンコード環境（3段階フォールバック）**
  * **第1優先（最速）**: FFmpeg が `libfdk_aac` に対応している場合は、FFmpeg 単体で高速処理。
  * **第2優先（高互換・高音質）**: 通常配布版の FFmpeg（`libfdk_aac` 非内蔵）でも、`fdkaac.exe` が指定・配置されていれば、.NET ネイティブパイプ（WAV 出力 ➔ `fdkaac.exe` ➔ タグ転送）経由でディスクレス FDK-AAC 変換を実行。
  * **第3優先（完全動作保証）**: どちらも利用できない最小環境では、FFmpeg 内蔵のネイティブ `aac` エンコーダーへ自動フォールバック。
  * **HE-AAC プロファイルの動的 UI 制御**: FFmpeg 内蔵 `aac` は HE-AAC (v1/v2) のエンコードに対応していないため、FDK 環境（`libfdk_aac` または `fdkaac.exe`）が存在しない場合は、コンボボックスの選択肢から `HE-AAC v1` / `HE-AAC v2` を自動的に非表示化し、設定ミスによるエラーを未然に防止。

* **ディスクレス＆高速ストリーミング（exhale & fdkaac ネイティブパイプ連携）**
  * xHE-AAC (exhale) および FDK-AAC (fdkaac) 変換時、中間 WAV ファイルをディスクに書き出すことなく、C# ネイティブの非同期ストリームパイプで FFmpeg と各エンコーダーをメモリ上で直結。
  * NVMe / SSD の寿命消費や一時容量圧迫を排除し、ネットワークドライブ（NAS / UNC パス `\\server\share` や割り当てドライブ `Z:\`）上の音源も高速かつ安定して直接変換可能。

* **アルバム付随ファイル（カバー画像・CUEシート等）の自動コピー**
  * 音源ファイル内に画像が埋め込まれていない環境でも、同フォルダ内に存在する `cover.jpg`, `folder.jpg`, `.cue`, `.log` などの関連ファイルを出力先フォルダへ自動的に複製・追従配置。
  * 並列エンコード開始前に重複排除して一括処理するため、ファイルロック競合が発生しません。

* **メタデータ＆アルバムアートの完全保持**
  * MP3 や AAC-LC はもちろん、外部 CLI を用いたストリーミング変換（xHE-AAC / fdkaac）においても、変換後に FFmpeg のストリームコピー（`-c copy`）を用いて元ファイルの曲情報・ジャケット画像を無劣化で自動統合・転送。

* **VBR / ABR 最適化**
  * MP3 の LAME VBR（V0〜V6）、libfdk_aac の VBR（Mode 1〜5）、Vorbis のネイティブ VBR（q4〜q10）、Opus の動的 VBR など、コーデック本来の音響心理モデルを最大限に活かす可変レート設計を採用。

* **マルチスレッド並列処理**
  * `Parallel.ForEachAsync` によるマルチコア CPU をフル活用した高速バッチエンコード。
  * CPU の論理コア数に応じた並列度（スレッド数）の自由な切り替え。

* **大容量ライブラリに耐える UI 設計**
  * WPF の UI 仮想化（`VirtualizingStackPanel`）により、数万曲規模のキュー投入でも軽快に動作。
  * 元フォルダの階層構造を維持したまま出力先に展開可能。

* **ポータブル＆スタンドアロン実行**
  * 単一ファイル（SingleFile）発行に対応。インストーラー不要で解凍後すぐに利用可能。

---

### 対応コーデック・プロファイル一覧

| 形式 / コーデック | エンジン | 出力拡張子 | レート / 品質指定 | 備考 |
| :--- | :--- | :--- | :--- | :--- |
| **MP3 (LAME VBR)** | FFmpeg | `.mp3` | VBR V0〜V6 (LAME) | 標準推奨 V2 (~190k)、最高品質 V0 (~245k) |
| **AAC-LC (FDK-AAC / 内蔵AAC)** | FFmpeg / `fdkaac` / 内蔵 `aac` | `.m4a` | VBR Mode 1〜5 (fdk) / 任意 | 3段階フォールバック対応 |
| **HE-AAC v1** | FFmpeg / `fdkaac` | `.m4a` | ABR 48k〜96k | ※FDK環境（`libfdk_aac` または `fdkaac.exe`）のみ表示 |
| **HE-AAC v2** | FFmpeg / `fdkaac` | `.m4a` | ABR 24k〜48k | ※FDK環境（`libfdk_aac` または `fdkaac.exe`）のみ表示 |
| **xHE-AAC / USAC** | exhale | `.m4a` | プリセット a〜c, 1〜9 | ネイティブパイプ接続。メタデータ・画像自動保持 |
| **Ogg Vorbis** | FFmpeg | `.ogg` | VBR q4〜q10 | 推奨 q6 (~192k) |
| **Opus** | FFmpeg | `.opus` | VBR 64k〜256k | 音楽向けモード (`-application audio`) 固定 |
| **FLAC** | FFmpeg | `.flac` | N/A (ロスレス) | 完全可逆圧縮 |
| **ALAC** | FFmpeg | `.m4a` | N/A (ロスレス) | Apple Lossless 可逆圧縮 |
| **WavPack** | FFmpeg | `.wv` | N/A (ロスレス) | 高圧縮率可逆形式 |

---

### 必要な外部バイナリ

本アプリケーションの実行には、以下のバイナリが必要です。アプリケーションと同じディレクトリ、またはシステム環境変数 `PATH` の通った場所に配置するか、アプリ内の「設定」ボタンから各バイナリのパスを指定してください。

1. **`ffmpeg.exe`** (必須)
   * 公式ビルド、Gyan.dev ビルド、自前ビルドのいずれも使用可能です。
2. **`fdkaac.exe`** (任意・推奨)
   * 通常配布版 FFmpeg（`libfdk_aac` 非内蔵）を利用しつつ、最高品質の FDK-AAC / HE-AAC を利用したい場合に配置。
3. **`exhale.exe`** (任意)
   * xHE-AAC (Extended HE-AAC / USAC) エンコード用バイナリ（v1.2.2 以降）。

---

### 使い方

1. **ファイルの追加**: 音楽ファイルやフォルダをリストにドラッグ＆ドロップするか、「ファイル追加」から選択。
2. **プロファイル設定**: コーデック、品質・ビットレート、並列スレッド数を指定。
3. **出力先の指定**: 保存先フォルダと「フォルダ階層を維持する」の有無を選択。
4. **変換開始**: 「変換開始」をクリックしてバッチ処理を実行。

---

## English

**Usual Audio Converter** is a lightweight, high-performance batch audio converter for Windows built with WPF, C#, and .NET 8, powered by **FFmpeg**, **exhale**, and **fdkaac**.

It covers a comprehensive lineup of modern lossy and lossless audio codecs, offering an intuitive GUI for converting music libraries with optimized VBR settings, zero disk-footprint pipe streaming, 3-tier AAC fallback, and preserved metadata.

### Key Features

* **3-Tier Robust AAC Encoding Fallback**
  * **Tier 1 (Fastest)**: Uses FFmpeg native `libfdk_aac` if supported by your FFmpeg build.
  * **Tier 2 (High Compatibility & Quality)**: If using standard FFmpeg builds without `libfdk_aac`, it pipes PCM audio to standalone `fdkaac.exe` via .NET asynchronous streams without intermediate disk writes.
  * **Tier 3 (Guaranteed Execution)**: Automatically falls back to FFmpeg's native internal `aac` encoder if neither FDK engine is detected.
  * **Dynamic HE-AAC Filtering**: Automatically hides `HE-AAC v1` and `HE-AAC v2` from the profile menu when FDK components are unavailable, preventing unsupported encoder failures.

* **Diskless Native Pipe Streaming (exhale & fdkaac)**
  * Streams decoded audio directly from FFmpeg to `exhale` or `fdkaac` standard input in memory using asynchronous C# stream piping without writing intermediate WAV files to disk.
  * Eliminates unnecessary SSD write endurance wear and temp storage consumption, while providing robust support for remote sources on network drives / UNC paths (`\\server\share`).

* **Automatic Album Companion Files Copying**
  * Automatically copies external cover art (`cover.jpg`, `folder.jpg`, etc.) and auxiliary files (`.cue`, `.log`) to the output directories, maintaining full album organization without multi-threading file locks.

* **Full Metadata & Cover Art Preservation**
  * Preserves ID3/M4A tags and embedded artwork. For external CLI encoders (`exhale` / `fdkaac`), a subsequent stage uses FFmpeg stream copy (`-c copy`) to merge the original metadata and album art without lossy re-encoding.

* **Optimized VBR / ABR Profiles**
  * Tuned for psychoacoustic efficiency: LAME MP3 VBR (V0–V6), libfdk_aac VBR (Mode 1–5), Vorbis native VBR (q4–q10), and Opus dynamic VBR.

* **Multi-Threaded Batch Encoding**
  * Fully asynchronous batch processing via `Parallel.ForEachAsync` with customizable thread limits.

* **Engineered for Massive Libraries**
  * Virtualized list view (`VirtualizingStackPanel`) ensures smooth scrolling even with tens of thousands of queued tracks.
  * Preserves original directory structures upon export.

* **Portable & Standalone**
  * Single-file executable deployment (`PublishSingleFile`). No installer required.

---

### Supported Codecs

| Format / Codec | Engine | Container | Quality / Rate Mode | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **MP3 (LAME VBR)** | FFmpeg | `.mp3` | VBR V0–V6 (LAME) | Recommended V2 (~190k), Maximum V0 (~245k) |
| **AAC-LC (FDK-AAC / Native AAC)** | FFmpeg / `fdkaac` / Native `aac` | `.m4a` | VBR Mode 1–5 (fdk) / Custom | 3-tier fallback support |
| **HE-AAC v1** | FFmpeg / `fdkaac` | `.m4a` | ABR 48k–96k | *Requires FDK environment (`libfdk_aac` or `fdkaac.exe`)* |
| **HE-AAC v2** | FFmpeg / `fdkaac` | `.m4a` | ABR 24k–48k | *Requires FDK environment (`libfdk_aac` or `fdkaac.exe`)* |
| **xHE-AAC / USAC** | exhale | `.m4a` | Presets a–c, 1–9 | Native pipe stream. Auto-merges tags & artwork |
| **Ogg Vorbis** | FFmpeg | `.ogg` | VBR q4–q10 | Recommended q6 (~192k) |
| **Opus** | FFmpeg | `.opus` | VBR 64k–256k | Music-optimized profile (`-application audio`) |
| **FLAC** | FFmpeg | `.flac` | N/A (Lossless) | Standard lossless compression |
| **ALAC** | FFmpeg | `.m4a` | N/A (Lossless) | Apple Lossless format |
| **WavPack** | FFmpeg | `.wv` | N/A (Lossless) | High-compression hybrid/lossless format |

---

### External Dependencies

The following binaries can be placed in the application folder, registered in your system `PATH`, or configured via the in-app "Settings" button.

1. **`ffmpeg.exe`** (Required)
   * Compatible with official builds, Gyan.dev builds, or custom builds.
2. **`fdkaac.exe`** (Optional / Recommended)
   * Recommended if using non-`libfdk_aac` FFmpeg builds while wanting high-quality FDK-AAC / HE-AAC output.
3. **`exhale.exe`** (Optional)
   * Required for xHE-AAC (USAC) encoding (v1.2.2 or later).

---

### Build Instructions

Requires Visual Studio 2022 or the .NET 8 SDK.

```powershell
# Clone the repository
git clone https://github.com/Barippi/UsualAudioConverter.git
cd UsualAudioConverter

# Publish as a single-file executable
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The output binary will be located at:
`bin\Release\net8.0-windows\win-x64\publish\UsualAudioConverter.exe`

---

## License

This project is licensed under the [MIT License](LICENSE).

Please check the individual licenses of external tools and libraries (FFmpeg, exhale, libfdk_aac, LAME, etc.) when distributing or compiling binaries.