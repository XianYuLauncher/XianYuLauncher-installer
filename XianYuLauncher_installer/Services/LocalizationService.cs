using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

#nullable disable

namespace XianYuLauncher_installer.Services;

// 定义所有需要翻译的字符串属性
public partial class LocalizedStrings : ObservableObject
{
    // 欢迎页
    [ObservableProperty] private string _welcomeTitle;
    [ObservableProperty] private string _welcomeDescription;
    [ObservableProperty] private string _clickNextHint;
    
    // 安装页
    [ObservableProperty] private string _installingWait;
    [ObservableProperty] private string _progressTitle; 
    
    // 完成页
    [ObservableProperty] private string _completeTitle;
    [ObservableProperty] private string _completeDescription;
    [ObservableProperty] private string _runLauncher;
    
    // 按钮
    [ObservableProperty] private string _btnNext;
    [ObservableProperty] private string _btnPrev;
    [ObservableProperty] private string _btnFinish;
    [ObservableProperty] private string _btnExit;

    // 步骤标题
    [ObservableProperty] private string _stepWelcome;
    [ObservableProperty] private string _stepInstalling;
    [ObservableProperty] private string _stepCert;
    [ObservableProperty] private string _stepFinish;
    
    // 弹窗
    [ObservableProperty] private string _storeDialogTitle;
    [ObservableProperty] private string _storeDialogContent;
    [ObservableProperty] private string _storeDialogPrimary;
    [ObservableProperty] private string _storeDialogSecondary;

    // 错误信息前缀
    [ObservableProperty] private string _errCertificate;
    [ObservableProperty] private string _errScript;
    [ObservableProperty] private string _errUnknown;
    [ObservableProperty] private string _errPermission;
}

public partial class LocalizationService : ObservableObject
{
    public LocalizedStrings Strings { get; } = new LocalizedStrings();

    public enum Language { Chinese, English }
    
    [ObservableProperty]
    private Language _currentLanguage;

    public LocalizationService()
    {
        // 根据系统语言自动设置默认语言
        var currentCulture = System.Globalization.CultureInfo.CurrentUICulture;
        if (currentCulture.Name.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase))
        {
            SetLanguage(Language.Chinese);
        }
        else
        {
            SetLanguage(Language.English);
        }
    }

    public void ToggleLanguage()
    {
        SetLanguage(CurrentLanguage == Language.Chinese ? Language.English : Language.Chinese);
    }

    public void SetLanguage(Language lang)
    {
        CurrentLanguage = lang;
        if (lang == Language.Chinese)
        {
            Strings.WelcomeTitle = "欢迎使用 XianYuLauncher 安装程序";
            Strings.WelcomeDescription = "XianYuLauncher 是一款UI完美的 Minecraft 启动器。";
            Strings.ClickNextHint = "点击 '下一步' 继续安装。";
            
            Strings.InstallingWait = "正在安装中，请稍候...";
            Strings.ProgressTitle = "正在安装";

            Strings.CompleteTitle = "安装完成！";
            Strings.CompleteDescription = "XianYuLauncher 已成功安装到您的计算机上。";
            Strings.RunLauncher = "运行 XianYuLauncher";
            
            Strings.BtnNext = "下一步";
            Strings.BtnPrev = "上一步";
            Strings.BtnFinish = "完成";
            Strings.BtnExit = "退出";
            
            Strings.StepWelcome = "欢迎";
            Strings.StepInstalling = "安装";
            Strings.StepCert = "证书";
            Strings.StepFinish = "完成";

            Strings.StoreDialogTitle = "你知道吗";
            Strings.StoreDialogContent = "XianYuLauncher 现已上架微软商店！\n\n微软商店版本安装更简单,无需手动配置证书和开发者模式,推荐普通用户使用\n\n如果您熟悉侧载应用的安装流程,也可以继续使用本安装程序";
            Strings.StoreDialogPrimary = "前往微软商店";
            Strings.StoreDialogSecondary = "继续安装";

            Strings.ErrCertificate = "未找到证书文件";
            Strings.ErrScript = "未找到安装脚本";
            Strings.ErrUnknown = "发生未知错误";
            Strings.ErrPermission = "权限不足";
        }
        else
        {
            Strings.WelcomeTitle = "Welcome to Setup";
            Strings.WelcomeDescription = "XianYuLauncher is a UI-perfect Minecraft Launcher.";
            Strings.ClickNextHint = "Click 'Next' to continue.";
            
            Strings.InstallingWait = "Installing, please wait...";
            Strings.ProgressTitle = "Installing";

            Strings.CompleteTitle = "Installation Complete!";
            Strings.CompleteDescription = "XianYuLauncher has been successfully installed.";
            Strings.RunLauncher = "Launch XianYuLauncher";
            
            Strings.BtnNext = "Next";
            Strings.BtnPrev = "Back";
            Strings.BtnFinish = "Finish";
            Strings.BtnExit = "Exit";

            Strings.StepWelcome = "Welcome";
            Strings.StepInstalling = "Install";
            Strings.StepCert = "Cert";
            Strings.StepFinish = "Finish";
            
            Strings.StoreDialogTitle = "Did you know?";
            Strings.StoreDialogContent = "XianYuLauncher is now available on Microsoft Store!\n\nThe Store version is easier to install. Installation via this installer is for advanced users familiar with sideloading.";
            Strings.StoreDialogPrimary = "Go to Store";
            Strings.StoreDialogSecondary = "Continue";

            Strings.ErrCertificate = "Certificate not found";
            Strings.ErrScript = "Install script not found";
            Strings.ErrUnknown = "Unknown error";
            Strings.ErrPermission = "Permission denied";
        }
    }
}