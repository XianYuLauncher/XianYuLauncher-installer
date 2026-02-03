using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

using XianYuLauncher_installer.ViewModels;

namespace XianYuLauncher_installer.Views;

public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    public MainViewModel ViewModel
    {
        get;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    // 在构造函数中保存DispatcherQueue
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
        
        // 保存DispatcherQueue，确保在UI线程中获取
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Localization.PropertyChanged += Localization_PropertyChanged;
        UpdateUI();
        
        // 设置logo图片源
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets/Square150x150Logo.scale-200.png");
        LogoImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
        
        // 页面加载完成后显示欢迎弹窗
        this.Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // 显示欢迎弹窗
        await ViewModel.ShowWelcomeDialogAsync();
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        try
        {
            // 使用保存的DispatcherQueue确保在UI线程中执行
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    UpdateUI();
                });
            }
            else
            {
                // 作为最后的备选方案，使用Window的DispatcherQueue
                var windowDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (windowDispatcherQueue != null)
                {
                    windowDispatcherQueue.TryEnqueue(() => UpdateUI());
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("DispatcherQueue is null, cannot update UI");
                }
            }
        }
        catch (Exception ex)
        {
            // 忽略任何可能的异常，避免影响程序运行
            System.Diagnostics.Debug.WriteLine($"Error in ViewModel_PropertyChanged: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void UpdateUI()
    {
        // 更新步骤标题和进度
        StepTitleTextBlock.Text = ViewModel.StepTitles[ViewModel.CurrentStep];
        StepProgressBar.Maximum = ViewModel.StepTitles.Length - 1;
        StepProgressBar.Value = ViewModel.CurrentStep;

        // 更新安装进度
        InstallProgressBar.Value = ViewModel.ProgressValue;
        ProgressMessageTextBlock.Text = ViewModel.ProgressMessage;

        // 更新面板可见性
        WelcomePanel.Visibility = ViewModel.CurrentStep == 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        InstallingPanel.Visibility = (ViewModel.CurrentStep == 1 || ViewModel.CurrentStep == 2 || ViewModel.CurrentStep == 3) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        CompletePanel.Visibility = ViewModel.CurrentStep == 4 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        // 更新按钮状态
        PreviousButton.Visibility = ViewModel.CurrentStep > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        NextButton.Visibility = ViewModel.CurrentStep == 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        CompleteButton.Visibility = ViewModel.CurrentStep == 4 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        // 更新按钮可用性
        NextButton.IsEnabled = !ViewModel.IsInstalling;
    }

    private void PreviousButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.PreviousStepCommand.Execute(null);
    }

    private void NextButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.NextStepCommand.Execute(null);
    }

    

    private void CompleteButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (RunLauncherCheckBox.IsChecked == true)
        {
            ViewModel.OpenLauncherCommand.Execute(null);
        }
        else
        {
            ViewModel.ExitCommand.Execute(null);
        }
    }

    private void ExitButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.ExitCommand.Execute(null);
    }

    private void GitHubButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // 导航到指定的GitHub仓库URL
        var uri = new Uri("https://github.com/XianYuLauncher/XianYuLauncher");
        var launcherOptions = new Windows.System.LauncherOptions();
        launcherOptions.TreatAsUntrusted = false;
        Windows.System.Launcher.LaunchUriAsync(uri, launcherOptions);
    }

    private void LanguageButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Localization.ToggleLanguage();
    }

    private void Localization_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(UpdateUI);
        }
    }
}
