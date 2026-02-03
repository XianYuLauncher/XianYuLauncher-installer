using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Windows.Management.Deployment;
using Windows.ApplicationModel;
using XianYuLauncher_installer.Services;

namespace XianYuLauncher_installer.ViewModels;

// JSON数据模型类
public class DownloadMirror
{
    [Newtonsoft.Json.JsonProperty("name")]
    public string Name { get; set; }
    
    [Newtonsoft.Json.JsonProperty("url")]
    public string Url { get; set; }
    
    [Newtonsoft.Json.JsonProperty("arch_urls")]
    public Dictionary<string, string> ArchUrls { get; set; }
}

public class LatestVersionInfo
{
    [Newtonsoft.Json.JsonProperty("version")]
    public string Version { get; set; }
    
    [Newtonsoft.Json.JsonProperty("release_time")]
    public string ReleaseTime { get; set; }
    
    [Newtonsoft.Json.JsonProperty("download_mirrors")]
    public List<DownloadMirror> DownloadMirrors { get; set; }
    
    [Newtonsoft.Json.JsonProperty("changelog")]
    public List<string> Changelog { get; set; }
    
    [Newtonsoft.Json.JsonProperty("important_update")]
    public bool ImportantUpdate { get; set; }
}

public partial class MainViewModel : ObservableRecipient
{
    [ObservableProperty]
    private int _currentStep = 0;

    [ObservableProperty]
    private string _installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XianYuLauncher");

    [ObservableProperty]
    private int _progressValue = 0;

    [ObservableProperty]
    private string _progressMessage = "准备安装...";

    [ObservableProperty]
    private bool _isInstalling = false;

    [ObservableProperty]
    private bool _isInstallationComplete = false;

    [ObservableProperty]
    private bool _isJsonLoaded = false;

    [ObservableProperty]
    private string _loadingStatus = "正在检查更新...";

    private LatestVersionInfo _latestVersionInfo = null;
    private HttpClient _httpClient = new HttpClient();

    public LocalizationService Localization { get; }

    public string[] StepTitles => new[] { 
        Localization.Strings.StepWelcome, 
        Localization.Strings.StepInstalling, 
        Localization.Strings.StepCert, 
        Localization.Strings.StepInstalling, 
        Localization.Strings.StepFinish 
    };

    public MainViewModel(LocalizationService localizationService)
    {
        Localization = localizationService;
        Debug.WriteLine("MainViewModel initialized");
        
        _progressMessage = Localization.Strings.InstallingWait;
        _loadingStatus = Localization.Strings.InstallingWait;

        // 启动程序后预读取JSON内容
        Task.Run(() => LoadLatestVersionInfoAsync());
    }

    [RelayCommand]
    private void NextStep()
    {
        Debug.WriteLine($"NextStep called, current step: {CurrentStep}");
        if (CurrentStep < StepTitles.Length - 1)
        {
            CurrentStep++;
            Debug.WriteLine($"Step changed to: {CurrentStep} - {StepTitles[CurrentStep]}");
            if (CurrentStep == 1) // 进入安装步骤
            {
                Debug.WriteLine("Starting installation...");
                Task.Run(() => InstallAsync());
            }
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        Debug.WriteLine($"PreviousStep called, current step: {CurrentStep}");
        if (CurrentStep > 0)
        {
            CurrentStep--;
            Debug.WriteLine($"Step changed to: {CurrentStep} - {StepTitles[CurrentStep]}");
        }
    }

    private string _installedAumid = null;

    [RelayCommand]
    private async Task OpenLauncher()
    {
        Debug.WriteLine("OpenLauncher called");
        
        bool launched = false;

        if (!string.IsNullOrEmpty(_installedAumid))
        {
            Debug.WriteLine($"Launching AUMID: {_installedAumid}");
            try
            {
                var packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser(string.Empty);
                var pkg = packages.FirstOrDefault(p => 
                {
                    try { return p.Id.FamilyName == _installedAumid.Split('!')[0]; }
                    catch { return false; }
                });

                if (pkg != null)
                {
                    var entries = await pkg.GetAppListEntriesAsync();
                    var entry = entries.FirstOrDefault(e => e.AppUserModelId == _installedAumid);
                    if (entry != null)
                    {
                        var success = await entry.LaunchAsync();
                        launched = success;
                        Debug.WriteLine($"Launch success: {success}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Launch failed: {ex.Message}");
            }
        }
        else
        {
             Debug.WriteLine("No AUMID to launch.");
        }

        if (launched)
        {
             // 启动成功后推出安装器
             App.MainWindow.Close();
        }
        else
        {
             // 启动失败，可能用户需要手动启动
             Debug.WriteLine("Failed to launch app automatically.");
             // 也可以选择在这里关闭，或者提示用户
             App.MainWindow.Close();
        }
    }

    private async Task CreateDesktopShortcut(string aumid, string displayName)
    {
        try 
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktopPath, $"{displayName}.lnk");
            
            // 使用 PowerShell 创建指向 AppsFolder 的快捷方式
            // 这通常能保留正确的图标和关联
            string script = $@"
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
$Shortcut.TargetPath = 'shell:AppsFolder\{aumid}'
$Shortcut.Save()";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using (var process = Process.Start(psi))
            {
                await process.WaitForExitAsync();
            }
            Debug.WriteLine($"Shortcut created: {shortcutPath}");
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Failed to create shortcut: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Exit()
    {
        Debug.WriteLine("Exit called, closing main window");
        App.MainWindow.Close();
    }

    public async Task ShowWelcomeDialogAsync()
    {
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "你知道吗",
            Content = "XianYuLauncher 现已上架微软商店！\n\n" +
                     "微软商店版本安装更简单,无需手动配置证书和开发者模式,推荐普通用户使用\n\n" +
                     "如果您熟悉侧载应用的安装流程,也可以继续使用本安装程序",
            PrimaryButtonText = "前往微软商店",
            SecondaryButtonText = "继续安装",
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            // 点击"前往微软商店"，跳转到微软商店
            var uri = new Uri("ms-windows-store://pdp/?ProductId=9pcnpgl7j6ks");
            await Windows.System.Launcher.LaunchUriAsync(uri);
            
            // 关闭应用
            App.MainWindow.Close();
        }
        // 点击"继续安装"，只关闭对话框，继续使用应用
    }

    private async Task LoadLatestVersionInfoAsync()
    {
        try
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting LoadLatestVersionInfoAsync...");
            LoadingStatus = "正在检查更新...";
            
            // 设置超时时间为10秒
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] HttpClient configured with 10s timeout");
            
            // 下载JSON内容
            string jsonUrl = "https://gitee.com/spiritos/XianYuLauncher-Resource/raw/main/latest_version.json";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Downloading JSON from: {jsonUrl}");
            string jsonContent = await _httpClient.GetStringAsync(jsonUrl);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] JSON content downloaded successfully, length: {jsonContent.Length} characters");
            
            // 解析JSON
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting JSON parsing...");
            
            // 清理JSON中的反引号
            string cleanedJsonContent = jsonContent.Replace(" `", " ").Replace("` ", " ");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Cleaned JSON content: {cleanedJsonContent.Substring(0, Math.Min(cleanedJsonContent.Length, 100))}...");
            
            _latestVersionInfo = JsonConvert.DeserializeObject<LatestVersionInfo>(cleanedJsonContent);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] JSON parsed successfully");
            
            // 详细输出解析结果
            if (_latestVersionInfo != null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Version: {_latestVersionInfo.Version}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Release Time: {_latestVersionInfo.ReleaseTime}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download Mirrors Count: {_latestVersionInfo.DownloadMirrors?.Count ?? 0}");
                if (_latestVersionInfo.DownloadMirrors != null && _latestVersionInfo.DownloadMirrors.Count > 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download Mirrors: {string.Join(", ", _latestVersionInfo.DownloadMirrors.Select(m => m.Name))}");
                    var officialMirror = _latestVersionInfo.DownloadMirrors.FirstOrDefault(m => m.Name == "official");
                    if (officialMirror != null)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Official Mirror URL: {officialMirror.Url}");
                        if (officialMirror.ArchUrls != null)
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Arch URLs: {string.Join(", ", officialMirror.ArchUrls.Keys)}");
                            foreach (var archUrl in officialMirror.ArchUrls)
                            {
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]   - {archUrl.Key}: {archUrl.Value}");
                            }
                        }
                    }
                }
            }
            
            IsJsonLoaded = true;
            LoadingStatus = "更新检查完成";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] LoadLatestVersionInfoAsync completed successfully");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] HttpRequestException: {ex.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Status Code: {ex.StatusCode}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            LoadingStatus = "更新检查失败，将使用默认下载地址";
            IsJsonLoaded = true; // 即使失败也标记为加载完成，使用默认下载地址
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] JsonException: {ex.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            LoadingStatus = "更新检查失败，将使用默认下载地址";
            IsJsonLoaded = true; // 即使失败也标记为加载完成，使用默认下载地址
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] COMException: {ex.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error Code: {ex.ErrorCode}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            LoadingStatus = "更新检查失败，将使用默认下载地址";
            IsJsonLoaded = true; // 即使失败也标记为加载完成，使用默认下载地址
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Unexpected Exception: {ex.GetType().FullName}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Message: {ex.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            LoadingStatus = "更新检查失败，将使用默认下载地址";
            IsJsonLoaded = true; // 即使失败也标记为加载完成，使用默认下载地址
        }
    }

    private async Task InstallAsync()
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] InstallAsync started");
        IsInstalling = true;
        ProgressValue = 0;
        ProgressMessage = "正在准备安装文件...";
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Initial progress: {ProgressValue}%, message: {ProgressMessage}");

        string tempDir = string.Empty;
        string downloadedZipPath = string.Empty;

        try
        {
            // 等待JSON加载完成
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Checking if JSON is loaded...");
            while (!IsJsonLoaded)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Waiting for JSON to load...");
                await Task.Delay(100);
            }
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] JSON loading completed");

            // 获取当前架构
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Detecting current architecture...");
            string currentArch = Environment.Is64BitOperatingSystem ? (Environment.Is64BitProcess ? "x64" : "x86") : "x86";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Initial architecture guess: {currentArch}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RuntimeInformation.ProcessArchitecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
            {
                currentArch = "arm64";
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Architecture updated to: {currentArch}");
            }
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Final architecture: {currentArch}");

            // 根据架构获取下载链接
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Getting download URL for architecture: {currentArch}");
            string downloadUrl = GetDownloadUrlForArchitecture(currentArch);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download URL for {currentArch}: {downloadUrl}");

            // 创建临时目录
            tempDir = Path.Combine(Path.GetTempPath(), $"XianYuLauncher_Install_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Created temp directory: {tempDir}");

            // 更新进度
            ProgressValue = 5;
            await Task.Delay(100);

            // 下载文件
            downloadedZipPath = Path.Combine(tempDir, "XianYuLauncher.zip");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting download to: {downloadedZipPath}");
            ProgressMessage = $"正在下载启动器文件（架构：{currentArch}）...";
            await DownloadFileAsync(downloadUrl, downloadedZipPath);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download completed");

            // 更新进度
            ProgressValue = 40;
            ProgressMessage = "正在解压文件...";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting file extraction...");
            await ExtractZipFileAsync(downloadedZipPath, tempDir);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Extraction completed");

            // 更新进度
            ProgressValue = 60;
            ProgressMessage = "正在检查证书...";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Checking certificate installation...");

            // 查找证书文件
            string[] cerFiles = Directory.GetFiles(tempDir, "*.cer");
            if (cerFiles.Length == 0)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No certificate file found in extracted files");
                throw new Exception("未找到证书文件");
            }
            string cerFilePath = cerFiles[0];
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found certificate file: {cerFilePath}");

            // 检查证书是否已安装
            if (!IsCertificateInstalled(cerFilePath))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Certificate not installed, attempting auto-install...");
                
                // 1. 尝试自动安装证书 (请求管理员权限)
                ProgressMessage = "正在申请权限安装证书...";
                try
                {
                    var certPsi = new ProcessStartInfo
                    {
                        FileName = "certutil",
                        Arguments = $"-addstore \"Root\" \"{cerFilePath}\"",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (var p = Process.Start(certPsi))
                    {
                        await p.WaitForExitAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Certificate auto-install exception: {ex.Message}");
                }

                // 2. 如果自动安装失败（如用户取消UAC），回退到手动指引模式
                if (!IsCertificateInstalled(cerFilePath))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Certificate not installed, falling back to manual...");
                    CurrentStep = 2; // 进入证书配置步骤
                    ProgressMessage = "请手动安装：点击安装证书 -> 本地计算机 -> 将证书放入“受信任的根证书颁发机构”";
                    
                    // 打开证书文件
                    Process.Start(new ProcessStartInfo(cerFilePath) { UseShellExecute = true });
                    
                    // 轮询等待证书安装完成
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Waiting for certificate installation...");
                    while (!IsCertificateInstalled(cerFilePath))
                    {
                        await Task.Delay(1000); // 每秒检查一次
                    }
                }
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Certificate installed successfully");
            }

            // 继续安装流程
            CurrentStep = 3; // 回到安装步骤
            ProgressValue = 70;
            ProgressMessage = "正在准备执行安装...";
            await Task.Delay(100);

            // 使用原生API安装应用包 (替代 PowerShell 脚本)
            ProgressMessage = "正在安装应用程序...";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting native installation...");
            
            // 确保我们在 UI 线程更新 (虽然 InstallAsync 本身在 Task.Run 里跑，可能不是 UI 线程)
            // 但 ObservableProperty 只要 Binding 是 TwoWay 或者是异步安全的就行？
            // WinUI 3 对于后台线程更新 UI 属性通常会抛出 RPC 错误，如果不通过 Dispatcher。
            // 之前的 RunPowerShellScriptAsAdmin 也是异步等待，没有直接更新 UI。
            
            await InstallAppPackageNativeAsync(tempDir);
            
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Native installation completed successfully");

            // 4. 安装后处理：获取 AUMID 并创建快捷方式
            try 
            {
                Debug.WriteLine("[Post-Install] Locating installed package...");
                var packageManager = new PackageManager();
                // 模糊匹配包名 (XianYuLauncher)
                var packages = packageManager.FindPackagesForUser(string.Empty)
                    .Where(p => p.Id.Name.Contains("XianYuLauncher", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.InstalledDate) // 取最新安装的
                    .ToList();

                if (packages.Count > 0)
                {
                    var pkg = packages[0];
                    Debug.WriteLine($"Found package: {pkg.Id.FullName}");
                    
                    var entries = await pkg.GetAppListEntriesAsync();
                    if (entries.Count > 0)
                    {
                        var entry = entries[0];
                        _installedAumid = entry.AppUserModelId;
                        Debug.WriteLine($"Resolved AUMID: {_installedAumid}");
                        
                        // 创建桌面快捷方式
                        ProgressMessage = "正在创建桌面快捷方式...";
                        await CreateDesktopShortcut(_installedAumid, "XianYu Launcher");
                    }
                }
            }
            catch (Exception ex)
            {
                 Debug.WriteLine($"Post-install setup failed: {ex.Message}");
            }

            // 更新进度
            ProgressValue = 100;
            ProgressMessage = "安装完成！";
            IsInstallationComplete = true;
            CurrentStep = 4; // 切换到完成页面
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Installation completed successfully");
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] COMException in InstallAsync: {ex.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error Code: {ex.ErrorCode}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            ProgressMessage = $"安装失败: 发生COM错误。\n{ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] UnauthorizedAccessException: {ex.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            ProgressMessage = $"安装失败: 权限不足，请以管理员身份运行安装程序。\n{ex.Message}";
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] IOException: {ex.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            ProgressMessage = $"安装失败: 文件操作错误。\n{ex.Message}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Unexpected Exception in InstallAsync: {ex.GetType().FullName}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Message: {ex.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            ProgressMessage = $"安装失败: 发生未知错误。\n{ex.Message}";
        }
        finally
        {
            IsInstalling = false;
            
            // 清理临时文件
            if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Cleared temp directory: {tempDir}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Failed to clear temp directory: {ex.Message}");
                }
            }
            
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] InstallAsync finished, IsInstalling: {IsInstalling}");
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath)
    {
        using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            
            long? totalBytes = response.Content.Headers.ContentLength;
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;
                int lastProgressUpdate = -1;
                
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    
                    if (totalBytes.HasValue)
                    {
                        int progress = (int)((totalRead * 100) / totalBytes.Value);
                        // 进度范围：10% - 40%
                        int newProgressValue = 10 + (int)((progress * 30) / 100);
                        
                        // 计算剩余字节数
                        long remainingBytes = totalBytes.Value - totalRead;
                        // 将剩余字节数转换为友好格式（KB, MB, GB）
                        string remainingSize = FormatFileSize(remainingBytes);
                        
                        // 更新进度消息，显示进度百分比和剩余大小
                        ProgressMessage = $"进度:{progress}% , 剩余 {remainingSize}";
                        
                        // 确保进度值递增且只在变化时更新，避免频繁更新UI
                        if (newProgressValue > ProgressValue && newProgressValue != lastProgressUpdate)
                        {
                            ProgressValue = newProgressValue;
                            lastProgressUpdate = newProgressValue;
                            
                            // 添加调试日志，便于查看进度更新情况
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download progress: {progress}% (UI progress: {ProgressValue}%), remaining: {remainingSize}");
                        }
                    }
                }
                
                // 确保下载完成后进度值和消息正确
                if (totalBytes.HasValue)
                {
                    ProgressValue = 40;
                    ProgressMessage = "下载完成，正在解压文件...";
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download completed, final progress: 40%");
                }
            }
        }
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        double number = bytes;
        
        while (number >= 1024 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:F1}{suffixes[counter]}";
    }

    private async Task ExtractZipFileAsync(string zipPath, string extractPath)
    {
        // 将解压操作放在后台线程执行，避免阻塞UI线程
        await Task.Run(() =>
        {
            using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
            {
                // 计算总文件数，用于进度计算
                int totalEntries = archive.Entries.Count;
                int processedEntries = 0;
                int lastProgressUpdate = -1;
                
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting extraction, total entries: {totalEntries}");
                
                foreach (var entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(extractPath, entry.FullName);
                    
                    // 创建目录结构
                    string destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }
                    
                    // 提取文件
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }
                    
                    // 更新处理的文件数
                    processedEntries++;
                    
                    // 计算进度：40% - 60%
                    int progress = (int)((processedEntries * 100) / totalEntries);
                    int newProgressValue = 40 + (int)((progress * 20) / 100);
                    
                    // 更新进度消息，显示解压进度
                    ProgressMessage = $"进度:{40 + progress}%,正在解压文件...";
                    
                    // 确保进度值递增且只在变化时更新，避免频繁更新UI
                    if (newProgressValue > ProgressValue && newProgressValue != lastProgressUpdate)
                    {
                        ProgressValue = newProgressValue;
                        lastProgressUpdate = newProgressValue;
                        
                        // 添加调试日志，便于查看进度更新情况
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Extraction progress: {progress}% (UI progress: {ProgressValue}%)");
                    }
                }
                
                // 确保解压完成后进度值和消息正确
                ProgressValue = 60;
                ProgressMessage = "解压完成，正在检查证书...";
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Extraction completed, final progress: 60%");
            }
        });
    }

    private bool IsCertificateInstalled(string cerFilePath)
    {
        try
        {
            // 读取证书文件
            byte[] certData = File.ReadAllBytes(cerFilePath);
            using (var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certData))
            {
                // 打开本地计算机的受信任根证书存储
                using (var store = new System.Security.Cryptography.X509Certificates.X509Store(System.Security.Cryptography.X509Certificates.StoreName.Root, 
                                                                                              System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine))
                {
                    store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
                    
                    // 查找匹配的证书
                    var certificates = store.Certificates.Find(System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint, 
                                                              cert.Thumbprint, false);
                    
                    return certificates.Count > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error checking certificate installation: {ex.Message}");
            return false;
        }
    }

    private async Task InstallAppPackageNativeAsync(string sourceDir)
    {
        // 1. 查找主要的 Appx/Msix 包 (优先级：Bundle > Package)
        var pkgFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".appxbundle", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".appx", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.EndsWith("bundle", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (pkgFiles.Length == 0)
        {
            throw new FileNotFoundException("未在安装目录中找到应用程序包 (.msix/.appx)");
        }

        string mainPackagePath = pkgFiles[0];
        Uri mainPackageUri = new Uri(mainPackagePath);
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found package: {mainPackagePath}");

        // 2. 查找依赖包
        var dependencyUris = new List<Uri>();
        string dependenciesRoot = Path.Combine(sourceDir, "Dependencies");
        
        if (Directory.Exists(dependenciesRoot))
        {
            // 简单架构判断：主要基于包名，辅以系统环境
            string pkgName = Path.GetFileName(mainPackagePath).ToLower();
            bool isArm64 = pkgName.Contains("arm64");
            bool isX64 = pkgName.Contains("x64") && !isArm64;
            bool isX86 = pkgName.Contains("x86") && !isX64 && !isArm64;

            // 扫描所有依赖文件
            var allDepFiles = Directory.GetFiles(dependenciesRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".appx", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));

            foreach (var depFile in allDepFiles)
            {
                string depFileName = Path.GetFileName(depFile).ToLower();
                string depDirName = Path.GetFileName(Path.GetDirectoryName(depFile)).ToLower();

                // 筛选逻辑：
                // 1. 根目录下的直接包含
                // 2. 文件夹名为 "neutral"
                // 3. 文件夹名与包架构匹配
                bool shouldInclude = depDirName == "dependencies" || // 直接在Dependencies根目录下
                                     depDirName == "neutral" ||
                                     (isX64 && depDirName == "x64") ||
                                     (isX86 && depDirName == "x86") ||
                                     (isArm64 && depDirName == "arm64");
                
                if (shouldInclude)
                {
                    dependencyUris.Add(new Uri(depFile));
                }
            }
        }
        
        Debug.WriteLine($"Found {dependencyUris.Count} dependencies");

        // 3. 执行安装
        var packageManager = new PackageManager();
        // DeploymentOptions 是枚举(Enum)，不是类，需要使用位运算组合
        var options = DeploymentOptions.ForceApplicationShutdown | DeploymentOptions.ForceTargetApplicationShutdown;
        
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Calling PackageManager.AddPackageAsync...");
        
        // 注意：AddPackageAsync 会安装到当前用户（如果以管理员运行，则是管理员用户）
        // 如果此Installer始终以管理员运行，这通常是可以接受的，或者需要注意用户上下文
        var deploymentOperation = packageManager.AddPackageAsync(mainPackageUri, dependencyUris, options);
        
        deploymentOperation.Progress = (res, progress) =>
        {
            // 将安装过程映射到进度条的 70% - 99%
            int mappedProgress = 70 + (int)((progress.percentage * 29) / 100.0);
            
            // 使用 DispatcherQueue 在 UI 线程更新进度，避免跨线程异常
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (mappedProgress > ProgressValue)
                {
                    ProgressValue = mappedProgress;
                }
            });
        };

        var result = await deploymentOperation;
        
        if (!result.IsRegistered)
        {
            Debug.WriteLine($"Package deployment failed: {result.ErrorText}");
            throw new Exception($"应用安装失败: {result.ErrorText} (HRESULT: {result.ExtendedErrorCode})");
        }
        
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AddPackageAsync finished successfully");
    }

    private string GetDownloadUrlForArchitecture(string architecture)
    {
        // 默认下载链接（旧版本兼容）
        string defaultUrl = "https://spiritstudio.com.cn/files/XianYuLauncher/XianYuLauncher_1.2.4.0_x64.zip";
        
        if (_latestVersionInfo != null && _latestVersionInfo.DownloadMirrors != null && _latestVersionInfo.DownloadMirrors.Count > 0)
        {
            // 1. 尝试查找名为 "official" 的镜像源（优先）
            var mirror = _latestVersionInfo.DownloadMirrors.FirstOrDefault(m => m.Name == "official");

            // 2. 如果没找到 "official"，则使用列表中的第一个镜像源
            if (mirror == null)
            {
                mirror = _latestVersionInfo.DownloadMirrors.FirstOrDefault();
            }

            if (mirror != null)
            {
                // 优先使用ArchUrls中的对应架构链接
                if (mirror.ArchUrls != null && mirror.ArchUrls.TryGetValue(architecture, out string archUrl))
                {
                    return archUrl;
                }
                // 如果没有对应架构的链接，使用默认Url
                if (!string.IsNullOrEmpty(mirror.Url))
                {
                    return mirror.Url;
                }
            }
        }
        
        // 如果解析失败或没有找到对应架构的链接，返回默认下载链接
        return defaultUrl;
    }
}
