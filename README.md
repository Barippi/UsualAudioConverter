# Usual Audio Converter

[English](#english) | [日本語](#japanese)

---

<a name="japanese"></a>
## 日本語 (Japanese)

Usual Audio Converter は、**FFmpeg** と **exhale** をバックエンドに採用した、Windows 向けの軽量かつ高速なバッチ音声変換デスクトップアプリケーション（WPF / C# / .NET 8）です。

日常的な音楽ライブラリの省容量化・高品質化から、アーカイブ向けのロスレス変換まで、主要なオーディオコーデックを網羅し、直感的な操作で一括変換できます。

### 主な特徴

* **多彩なコーデック対応**
  * **ロッシー（音質優先の VBR / ABR）**: xHE-AAC (USAC), AAC-LC, HE-AAC v1, HE-AAC v2, MP3 (LAME), Ogg Vorbis, Opus
  * **ロスレス（可逆圧縮）**: FLAC, ALAC (Apple Lossless), WavPack (.wv)
* **VBR / ABR 最適化**
  * MP3 の LAME VBR（V0〜V6）、libfdk_aac の VBR（Mode 1〜5）、Vorbis のネイティブ VBR（q4〜q10）、Opus の動的 VBR など、コーデック本来の音響心理モデルを最大限に活かす可変レート設計を採用。
* **メタデータ＆アルバムアートの完全保持**
  * MP3 や AAC-LC はもちろん、中間 WAV を介する **xHE-AAC (exhale)** においても、変換後に FFmpeg のストリームコピー（`-c copy`）を用いて元ファイルの曲情報・ジャケット画像を無劣化で自動統合・転送。
* **マルチスレッド並列処理**
  * `Parallel.ForEachAsync` によるマルチコア CPU をフル活用した高速バッチエンコード。
  * CPU の論理コア数に応じた並列度（スレッド数）の自由な切り替え。
* **大容量ライブラリに耐える UI 設計**
  * WPF の UI 仮想化（`VirtualizingStackPanel`）により、数万曲規模のキュー投入でも軽快に動作。
  * 元フォルダの階層構造を維持したまま出力先に展開可能。
* **ポータブル＆スタンドアロン実行**
  * 単一ファイル（SingleFile）発行に対応。インストーラー不要で解凍後すぐに利用可能。

### 対応コーデック一覧

| 形式 / コーデック | エンジン | 出力拡張子 | レート / 品質指定 | 備考 |
| :--- | :--- | :--- | :--- | :--- |
| **xHE-AAC / USAC** | exhale | `.m4a` | プリセット a〜c, 1〜9 | 次世代高圧縮規格。メタデータ・画像自動保持 |
| **AAC-LC** | FFmpeg | `.m4a` | VBR Mode 1〜5 (fdk) | 最高音質 Mode 5 (~220-260k) 推奨 |
| **HE-AAC v1** | FFmpeg | `.m4a` | ABR 48k〜96k | 中低ビットレート向け（SBR） |
| **HE-AAC v2** | FFmpeg | `.m4a` | ABR 24k〜48k | 超低ビットレート向け（SBR + PS） |
| **MP3** | FFmpeg | `.mp3` | VBR V0〜V6 (LAME) | 標準推奨 V2 (~190k)、最高品質 V0 (~245k) |
| **Ogg Vorbis** | FFmpeg | `.ogg` | VBR q4〜q10 | 推奨 q6 (~192k) |
| **Opus** | FFmpeg | `.opus` | VBR 64k〜256k | 音楽向けモード (`-application audio`) 固定 |
| **FLAC** | FFmpeg | `.flac` | N/A (ロスレス) | 完全可逆圧縮 |
| **ALAC** | FFmpeg | `.m4a` | N/A (ロスレス) | Apple Lossless 可逆圧縮 |
| **WavPack** | FFmpeg | `.wv` | N/A (ロスレス) | 高圧縮率可逆形式 |

### 必要な外部バイナリ

本アプリケーションの実行には、以下のバイナリが必要です。  
アプリケーションと同じディレクトリ、またはシステム環境変数 `PATH` の通った場所に配置するか、アプリ内の「設定」ボタンから各バイナリのパスを指定してください。

1. **`ffmpeg.exe`**
   * ※ AAC-LC / HE-AAC の高品質エンコードには、`libfdk_aac` が有効化されたビルドを推奨します。[media-autobuild_suite](https://github.com/m-ab-s/media-autobuild_suite) 等でセルフビルドするか、互換ビルドをご用意ください。
2. **`exhale.exe`**
   * xHE-AAC (Extended HE-AAC / USAC) エンコード用バイナリ（v1.2.2 以降）。

### 使い方

1. **ファイルの追加**: 音楽ファイルやフォルダをリストにドラッグ＆ドロップするか、「ファイル追加」から選択。
2. **プロファイル設定**: コーデック、品質・ビットレート、並列スレッド数を指定。
3. **出力先の指定**: 保存先フォルダと「フォルダ階層を維持する」の有無を選択。
4. **変換開始**: 「変換開始」をクリックしてバッチ処理を実行。

---

<a name="english"></a>
## English

**Usual Audio Converter** is a lightweight, high-performance batch audio converter for Windows built with WPF, C#, and .NET 8, powered by **FFmpeg** and **exhale**.

It covers a comprehensive lineup of modern lossy and lossless audio codecs, offering an intuitive GUI for converting music libraries with optimized VBR settings and preserved metadata.

### Key Features

* **Wide Codec Support**
  * **Lossy (Quality-Focused VBR/ABR)**: xHE-AAC (USAC), AAC-LC, HE-AAC v1, HE-AAC v2, MP3 (LAME), Ogg Vorbis, Opus
  * **Lossless (Bit-perfect)**: FLAC, ALAC (Apple Lossless), WavPack (.wv)
* **Optimized VBR / ABR Profiles**
  * Tuned for psychacoustic efficiency: LAME MP3 VBR (V0–V6), libfdk_aac VBR (Mode 1–5), Vorbis native VBR (q4–q10), and Opus dynamic VBR.
* **Full Metadata & Cover Art Preservation**
  * Preserves ID3/M4A tags and embedded artwork. For **xHE-AAC (exhale)**, a 2-step pipeline uses FFmpeg stream copy (`-c copy`) to merge the original metadata and album art without lossy re-encoding.
* **Multi-Threaded Batch Encoding**
  * Fully asynchronous execution via `Parallel.ForEachAsync` with customizable thread limits.
* **Engineered for Massive Libraries**
  * Virtualized list view (`VirtualizingStackPanel`) ensures smooth scrolling even with tens of thousands of queued tracks.
  * Preserves original directory structures upon export.
* **Portable & Standalone**
  * Single-file executable deployment (`PublishSingleFile`). No installer required.

### Supported Codecs

| Format / Codec | Engine | Container | Quality / Rate Mode | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **xHE-AAC / USAC** | exhale | `.m4a` | Presets a–c, 1–9 | Next-gen compression. Auto-merges tags & artwork |
| **AAC-LC** | FFmpeg | `.m4a` | VBR Mode 1–5 (fdk) | High quality Mode 5 (~220–260k) recommended |
| **HE-AAC v1** | FFmpeg | `.m4a` | ABR 48k–96k | Optimized for low-to-mid bitrates (SBR) |
| **HE-AAC v2** | FFmpeg | `.m4a` | ABR 24k–48k | Parametric Stereo for ultra-low bitrates |
| **MP3** | FFmpeg | `.mp3` | VBR V0–V6 (LAME) | Recommended V2 (~190k), Maximum V0 (~245k) |
| **Ogg Vorbis** | FFmpeg | `.ogg` | VBR q4–q10 | Recommended q6 (~192k) |
| **Opus** | FFmpeg | `.opus` | VBR 64k–256k | Music-optimized profile (`-application audio`) |
| **FLAC** | FFmpeg | `.flac` | N/A (Lossless) | Standard lossless compression |
| **ALAC** | FFmpeg | `.m4a` | N/A (Lossless) | Apple Lossless format |
| **WavPack** | FFmpeg | `.wv` | N/A (Lossless) | High-compression hybrid/lossless format |

### External Dependencies

The following binaries are required to run the converter. Place them in the application folder, include them in your system `PATH`, or configure their paths via the in-app "Settings" button.

1. **`ffmpeg.exe`**
   * *Note*: For AAC-LC and HE-AAC, a build with `libfdk_aac` enabled is recommended (e.g., self-built using [media-autobuild_suite](https://github.com/m-ab-s/media-autobuild_suite)).
2. **`exhale.exe`**
   * Required for xHE-AAC (USAC) encoding (v1.2.2 or later).

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