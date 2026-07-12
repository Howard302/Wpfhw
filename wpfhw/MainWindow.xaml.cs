using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace wpfhw;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;
    private ModSearchHit? _selectedMod;
    private List<ModVersion> _currentVersions = new();
    private string _currentGameVer = "";
    private string _currentLoader = "";
    private string _currentProjectType = "mod";

    public MainWindow()
    {
        InitializeComponent();

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ModDownloader/1.0 (haodi0302@qq.com; Windows)");

        // 手动设置初始类型，避免 SelectionChanged 在初始化时触发
        _currentProjectType = "mod";
    }

    #region ========== 导航切换 ==========

    private void LstNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstNav.SelectedItem is not ListBoxItem item) return;
        _currentProjectType = item.Tag?.ToString() ?? "mod";

        // 加 null 检查，防止初始化时控件还没准备好
        if (cbbLoader != null)
            cbbLoader.IsEnabled = _currentProjectType != "modpack";

        if (txtStatusMsg != null)
            txtStatusMsg.Text = $"已切换至：{item.Content}，点击搜索";

        if (lstModResult != null)
            lstModResult.Items.Clear();
    }

    #endregion

    #region ========== 搜索面板 ==========

    private async void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        string keyword = txtSearchKey.Text.Trim();
        string gameVer = (cbbGameVersion.SelectedItem as ComboBoxItem)?.Content.ToString()
            ?? cbbGameVersion.Text.Trim();
        string loader = (cbbLoader.SelectedItem as ComboBoxItem)?.Content.ToString()
            ?? cbbLoader.Text.Trim();

        _currentGameVer = gameVer;
        _currentLoader = loader;

        if (string.IsNullOrWhiteSpace(keyword))
        {
            txtStatusMsg.Text = "请输入搜索关键词";
            return;
        }

        try
        {
            txtStatusMsg.Text = "正在搜索...";
            lstModResult.Items.Clear();

            var facetGroups = new List<string>
            {
                $"[\"project_type:{_currentProjectType}\"]"
            };

            if (!string.IsNullOrWhiteSpace(gameVer))
            {
                facetGroups.Add($"[\"versions:{gameVer}\"]");
            }

            if (!string.IsNullOrWhiteSpace(loader) && _currentProjectType != "modpack")
            {
                facetGroups.Add($"[\"categories:{loader}\"]");
            }

            string rawFacet = "[" + string.Join(",", facetGroups) + "]";
            string facetEnc = Uri.EscapeDataString(rawFacet);
            string queryEnc = Uri.EscapeDataString(keyword);

            string requestUrl = $"https://api.modrinth.com/v2/search?query={queryEnc}&facets={facetEnc}&limit=20";

            string jsonText = await _httpClient.GetStringAsync(requestUrl);

            var jsonOption = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var searchResult = JsonSerializer.Deserialize<ModSearchResponse>(jsonText, jsonOption);

            if (searchResult?.Hits != null && searchResult.Hits.Any())
            {
                foreach (var mod in searchResult.Hits)
                {
                    lstModResult.Items.Add(mod);
                }
                txtStatusMsg.Text = $"成功找到 {searchResult.Hits.Count} 个结果（双击进入版本选择）";
            }
            else
            {
                txtStatusMsg.Text = "未查询到相关结果";
            }
        }
        catch (TaskCanceledException)
        {
            txtStatusMsg.Text = "请求超时！国内直连Modrinth不稳定";
        }
        catch (HttpRequestException httpErr)
        {
            txtStatusMsg.Text = $"网络错误：{httpErr.StatusCode}";
        }
        catch (Exception ex)
        {
            txtStatusMsg.Text = $"搜索异常：{ex.Message}";
        }
    }

    private void LstModResult_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (lstModResult.SelectedItem is not ModSearchHit modHit) return;
        _selectedMod = modHit;
        ShowVersionDetail(modHit);
    }

    #endregion

    #region ========== 版本详情面板 ==========

    private async void ShowVersionDetail(ModSearchHit modHit)
    {
        panelSearch.Visibility = Visibility.Collapsed;
        panelVersionDetail.Visibility = Visibility.Visible;
        txtDownloadStatus.Text = "正在加载版本列表...";

        panelVersionDetail.DataContext = new { SelectedMod = modHit };

        try
        {
            string url = $"https://api.modrinth.com/v2/project/{modHit.ProjectId}/version";
            string json = await _httpClient.GetStringAsync(url);

            var jsonOption = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _currentVersions = JsonSerializer.Deserialize<List<ModVersion>>(json, jsonOption) ?? new();

            BuildVersionTags();
            FilterVersionsByTag(_currentGameVer);
        }
        catch (Exception ex)
        {
            txtDownloadStatus.Text = $"加载失败：{ex.Message}";
        }
    }

    private void BuildVersionTags()
    {
        panelVersionTags.Children.Clear();

        var allGameVersions = _currentVersions
            .SelectMany(v => v.GameVersions)
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();

        var btnAll = CreateTagButton("全部", string.IsNullOrWhiteSpace(_currentGameVer));
        btnAll.Click += (s, e) => FilterVersionsByTag("全部");
        panelVersionTags.Children.Add(btnAll);

        foreach (var ver in allGameVersions)
        {
            bool isActive = ver == _currentGameVer;
            var btn = CreateTagButton(ver, isActive);
            string capturedVer = ver;
            btn.Click += (s, e) => FilterVersionsByTag(capturedVer);
            panelVersionTags.Children.Add(btn);
        }
    }

    private Button CreateTagButton(string text, bool isActive)
    {
        return new Button
        {
            Content = text,
            Width = 60,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0),
            Background = isActive
                ? new SolidColorBrush(Color.FromRgb(156, 39, 176))
                : new SolidColorBrush(Color.FromRgb(68, 68, 68)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
    }

    private void FilterVersionsByTag(string gameVerTag)
    {
        var filtered = gameVerTag == "全部"
            ? _currentVersions
            : _currentVersions.Where(v => v.GameVersions.Contains(gameVerTag)).ToList();

        var groups = filtered
            .GroupBy(v => string.Join("/", v.Loaders))
            .Select(g => new VersionGroup
            {
                GroupName = $"{g.Key} {gameVerTag}".Trim(),
                Versions = g.ToList(),
                VersionInfo = $"版本号: {string.Join(", ", g.Select(v => v.VersionNumber).Take(3))}...",
                IsRecommended = g.Any(v =>
                    (!string.IsNullOrWhiteSpace(_currentGameVer) && v.GameVersions.Contains(_currentGameVer)) &&
                    (!string.IsNullOrWhiteSpace(_currentLoader) && v.Loaders.Contains(_currentLoader, StringComparer.OrdinalIgnoreCase)))
            })
            .ToList();

        itemsVersionGroups.ItemsSource = groups;
        txtDownloadStatus.Text = $"共 {filtered.Count} 个版本";
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        panelVersionDetail.Visibility = Visibility.Collapsed;
        panelSearch.Visibility = Visibility.Visible;
        _currentVersions.Clear();
        itemsVersionGroups.ItemsSource = null;
    }

    private void BtnOpenModrinth_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMod == null) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = $"https://modrinth.com/{_currentProjectType}/{_selectedMod.ProjectId}",
            UseShellExecute = true
        });
    }

    private void BtnCopyName_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMod == null) return;
        Clipboard.SetText(_selectedMod.Title);
        txtDownloadStatus.Text = "已复制名称到剪贴板";
    }

    private async void BtnDownloadVersion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not VersionGroup group) return;

        var version = group.Versions.FirstOrDefault(v =>
            (!string.IsNullOrWhiteSpace(_currentGameVer) && v.GameVersions.Contains(_currentGameVer)) &&
            (!string.IsNullOrWhiteSpace(_currentLoader) && v.Loaders.Contains(_currentLoader, StringComparer.OrdinalIgnoreCase)))
            ?? group.Versions.First();

        var mainFile = version.Files.FirstOrDefault(f => f.IsPrimary)
            ?? version.Files.FirstOrDefault();

        if (mainFile == null)
        {
            txtDownloadStatus.Text = "该版本无可下载文件";
            return;
        }

        string saveDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string fullSavePath = Path.Combine(saveDirectory, mainFile.FileName);

        txtDownloadStatus.Text = $"正在下载 {mainFile.FileName}...";
        progressBarDownload.Value = 0;

        try
        {
            using var response = await _httpClient.GetAsync(
                mainFile.Url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long receivedBytes = 0;
            byte[] buffer = new byte[8192];

            using var streamRemote = await response.Content.ReadAsStreamAsync();
            using var streamLocal = new FileStream(
                fullSavePath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

            int readCount;
            while ((readCount = await streamRemote.ReadAsync(buffer)) > 0)
            {
                await streamLocal.WriteAsync(buffer.AsMemory(0, readCount));
                receivedBytes += readCount;

                if (totalBytes > 0)
                {
                    progressBarDownload.Value = receivedBytes * 100.0 / totalBytes;
                }
            }

            txtDownloadStatus.Text = $"✅ 下载完成！{mainFile.FileName}";
        }
        catch (Exception ex)
        {
            txtDownloadStatus.Text = $"❌ 下载失败：{ex.Message}";
        }
    }

    #endregion
}

public class VersionGroup
{
    public string GroupName { get; set; } = "";
    public List<ModVersion> Versions { get; set; } = new();
    public string VersionInfo { get; set; } = "";
    public bool IsRecommended { get; set; }
}