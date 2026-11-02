using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace UsualAudioConverter;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".flac", ".mp3", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aiff"
    };

    public ObservableCollection<AudioItem> Items { get; } = new();
    private readonly HashSet<string> _queuedPaths = new(StringComparer.OrdinalIgnoreCase);

    private string _ffmpegPath = "ffmpeg.exe";
    private string _exhalePath = "exhale.exe";
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        GridItems.ItemsSource = Items;

        InitializeControls();
    }

    private void InitializeControls()
    {
        // プロファイル設定
        var profiles = EncoderProfile.GetProfiles();
        ComboProfiles.ItemsSource = profiles;
        ComboProfiles.DisplayMemberPath = "Name";
        ComboProfiles.SelectedIndex = 0;

        // 論理コア数に応じた並列度
        int cores = Environment.ProcessorCount;
        int defaultThreads = cores <= 2 ? 1 : (cores <= 4 ? 2 : Math.Min(8, cores - 2));
        var threadOptions = new HashSet<int> { 1, 2, 4, 6, 8, cores, defaultThreads };
        ComboThreads.ItemsSource = threadOptions.Where(t => t <= cores).OrderBy(t => t).ToList();
        ComboThreads.SelectedItem = defaultThreads;
    }

    private void ComboProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboProfiles.SelectedItem is not EncoderProfile selected) return;
        ComboBitrate.ItemsSource = selected.Bitrates;
        ComboBitrate.SelectedItem = selected.DefaultBitrate;
    }

    private void GridItems_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void GridItems_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;

        AddPaths(files);
    }

    private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio Files|*.wav;*.flac;*.mp3;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aiff|All Files|*.*"
        };
        if (ofd.ShowDialog() == true)
        {
            AddPaths(ofd.FileNames);
        }
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        var collected = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path) && ValidExtensions.Contains(Path.GetExtension(path)))
                collected.Add(path);
            else if (Directory.Exists(path))
            {
                foreach (var f in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
                {
                    if (ValidExtensions.Contains(Path.GetExtension(f)))
                        collected.Add(f);
                }
            }
        }

        foreach (var file in collected)
        {
            if (_queuedPaths.Add(file))
            {
                Items.Add(new AudioItem
                {
                    FileName = Path.GetFileName(file),
                    SourcePath = file
                });
            }
        }

        TxtStatus.Text = $"{Items.Count} 件のファイルが登録されています";
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null)
        {
            MessageBox.Show("変換実行中はリストをクリアできません。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Items.Clear();
        _queuedPaths.Clear();
        ProgressTotal.Value = 0;
        TxtStatus.Text = "リストをクリアしました";
    }

    private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "保存先フォルダを選択" };
        if (dialog.ShowDialog() == true)
        {
            TxtCustomDir.Text = dialog.FolderName;
        }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var ofdFf = new OpenFileDialog { Title = "ffmpeg.exe を指定", Filter = "ffmpeg.exe|ffmpeg.exe|All Files|*.*" };
        if (ofdFf.ShowDialog() == true) _ffmpegPath = ofdFf.FileName;

        var ofdEx = new OpenFileDialog { Title = "exhale.exe を指定", Filter = "exhale.exe|exhale.exe|All Files|*.*" };
        if (ofdEx.ShowDialog() == true) _exhalePath = ofdEx.FileName;
    }

    // 先頭と末尾のサンプルから O(1) で共通ルートを算出する高速ロジック
    private static string? CalculateCommonRootFast(IList<AudioItem> items)
    {
        if (items.Count == 0) return null;
        if (items.Count == 1) return Path.GetDirectoryName(items[0].SourcePath);

        string p1 = Path.GetDirectoryName(items[0].SourcePath) ?? string.Empty;
        string p2 = Path.GetDirectoryName(items[^1].SourcePath) ?? string.Empty;

        int len = Math.Min(p1.Length, p2.Length);
        int commonLen = 0;
        for (int i = 0; i < len; i++)
        {
            if (char.ToUpperInvariant(p1[i]) == char.ToUpperInvariant(p2[i]))
                commonLen++;
            else
                break;
        }

        string common = p1[..commonLen];
        int lastSlash = common.LastIndexOfAny(new[] { '\\', '/' });
        return lastSlash > 0 ? common[..lastSlash] : common;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (Items.Count == 0) return;
        if (ComboProfiles.SelectedItem is not EncoderProfile profile) return;
        int bitrate = (int)ComboBitrate.SelectedItem;
        int maxThreads = (int)ComboThreads.SelectedItem;

        // 出力先パスの事前解決
        bool isCustom = RadioCustomDir.IsChecked == true;
        bool keepStructure = ChkKeepStructure.IsChecked == true;
        string customBase = TxtCustomDir.Text.Trim();
        string? commonRoot = (isCustom && keepStructure) ? CalculateCommonRootFast(Items) : null;

        foreach (var item in Items)
        {
            string outDir;
            if (isCustom)
            {
                string baseDir = string.IsNullOrEmpty(customBase)
                    ? (Path.GetDirectoryName(item.SourcePath) ?? string.Empty)
                    : customBase;

                if (keepStructure && !string.IsNullOrEmpty(commonRoot) && item.SourcePath.StartsWith(commonRoot, StringComparison.OrdinalIgnoreCase))
                {
                    string rel = Path.GetRelativePath(commonRoot, Path.GetDirectoryName(item.SourcePath) ?? string.Empty);
                    outDir = Path.Combine(baseDir, rel);
                }
                else
                {
                    outDir = baseDir;
                }
            }
            else
            {
                outDir = Path.GetDirectoryName(item.SourcePath) ?? string.Empty;
            }

            string baseName = Path.GetFileNameWithoutExtension(item.SourcePath);
            string targetPath = Path.Combine(outDir, $"{baseName}{profile.Extension}");
            if (string.Equals(item.SourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                targetPath = Path.Combine(outDir, $"{baseName}_converted{profile.Extension}");
            }
            item.DestinationPath = targetPath;
        }

        SetUiEncodingState(true);
        _cts = new CancellationTokenSource();
        int completed = 0;
        ProgressTotal.Maximum = Items.Count;
        ProgressTotal.Value = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxThreads,
            CancellationToken = _cts.Token
        };

        try
        {
            await Parallel.ForEachAsync(Items, parallelOptions, async (item, ct) =>
            {
                item.Status = "変換中";
                var progress = new Progress<string>(p => item.Progress = p);

                try
                {
                    await EncoderService.EncodeAsync(
                        item, _ffmpegPath, _exhalePath, profile, bitrate, progress, ct
                    );
                }
                catch
                {
                    // エラーは item 内に保存
                }
                finally
                {
                    int done = Interlocked.Increment(ref completed);
                    Dispatcher.Invoke(() =>
                    {
                        ProgressTotal.Value = done;
                        TxtStatus.Text = $"進捗: {done}/{Items.Count} 件完了";
                    });
                }
            });

            TxtStatus.Text = $"すべての変換が完了しました ({Items.Count}件)";
        }
        catch (OperationCanceledException)
        {
            TxtStatus.Text = "バッチ処理が中断されました。";
        }
        finally
        {
            _cts = null;
            SetUiEncodingState(false);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnCancel.IsEnabled = false;
        TxtStatus.Text = "中断処理中...";
    }

    private void SetUiEncodingState(bool isEncoding)
    {
        BtnStart.IsEnabled = !isEncoding;
        BtnClear.IsEnabled = !isEncoding;
        BtnAddFiles.IsEnabled = !isEncoding;
        BtnSettings.IsEnabled = !isEncoding;
        ComboProfiles.IsEnabled = !isEncoding;
        ComboBitrate.IsEnabled = !isEncoding;
        ComboThreads.IsEnabled = !isEncoding;
        RadioSameDir.IsEnabled = !isEncoding;
        RadioCustomDir.IsEnabled = !isEncoding;
        TxtCustomDir.IsEnabled = !isEncoding && RadioCustomDir.IsChecked == true;
        BtnBrowseDir.IsEnabled = !isEncoding && RadioCustomDir.IsChecked == true;
        ChkKeepStructure.IsEnabled = !isEncoding && RadioCustomDir.IsChecked == true;
        BtnCancel.IsEnabled = isEncoding;
    }

    private void GridItems_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridItems.SelectedItem is AudioItem selected && !string.IsNullOrEmpty(selected.ErrorLog))
        {
            MessageBox.Show(selected.ErrorLog, $"エラーログ - {selected.FileName}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
