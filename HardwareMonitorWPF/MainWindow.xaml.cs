using LibreHardwareMonitor.Hardware;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HardwareMonitorWPF;

public partial class MainWindow : Window
{
    private static readonly int[] RefreshRateOptions = [250, 500, 1000, 2000, 5000];
    private readonly DispatcherTimer timer = new();
    private readonly string settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HardwareMonitor",
        "settings.json");
    private readonly List<MenuItem> refreshRateItems = [];
    private readonly List<MenuItem> themeItems = [];
    private readonly ContextMenu settingsMenu = new();
    private Computer? computer;
    private AppSettings appSettings = new();

    public MainWindow()
    {
        InitializeComponent();
        string version = GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        lblVersion.Text = $"v{version}";
        Title = $"Hardware Monitor {version}";

        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            return;

        appSettings = LoadSettings();
        ApplyTheme(appSettings.Theme);
        Topmost = appSettings.AlwaysOnTop;
        UpdateAlwaysOnTopButton();

        computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true
        };
        computer.Open();

        timer.Interval = TimeSpan.FromMilliseconds(appSettings.RefreshInterval);
        timer.Tick += (_, _) => UpdateTemperatures();
        lblFooter.Text = $"Refresh every {appSettings.RefreshInterval} ms";
        ConfigureSettingsMenu();
        timer.Start();
        UpdateTemperatures();
    }

    private void ConfigureSettingsMenu()
    {
        settingsMenu.Items.Add(new MenuItem
        {
            Header = "Refresh interval:",
            IsEnabled = false
        });

        foreach (int interval in RefreshRateOptions)
        {
            MenuItem item = new()
            {
                Header = $"{interval} ms",
                IsCheckable = true,
                IsChecked = interval == appSettings.RefreshInterval,
                Tag = interval
            };
            item.Click += (_, _) =>
            {
                timer.Interval = TimeSpan.FromMilliseconds(interval);
                lblFooter.Text = $"Refresh every {interval} ms";
                appSettings.RefreshInterval = interval;
                SaveSettings();

                foreach (MenuItem rateItem in refreshRateItems)
                    rateItem.IsChecked = rateItem.Tag is int rate && rate == interval;
            };
            refreshRateItems.Add(item);
            settingsMenu.Items.Add(item);
        }

        settingsMenu.Items.Add(new Separator());
        settingsMenu.Items.Add(new MenuItem
        {
            Header = "Theme:",
            IsEnabled = false
        });

        foreach ((string name, string theme) in new[]
                 {
                     ("Light", "Light"),
                     ("Dark", "Dark")
                 })
        {
            MenuItem item = new()
            {
                Header = name,
                IsCheckable = true,
                IsChecked = string.Equals(appSettings.Theme, theme, StringComparison.OrdinalIgnoreCase),
                Tag = theme
            };
            item.Click += (_, _) => SetTheme(theme);
            themeItems.Add(item);
            settingsMenu.Items.Add(item);
        }

        settingsMenu.Items.Add(new Separator());
        MenuItem alwaysOnTopItem = new()
        {
            Header = "Always on top",
            IsCheckable = true,
            IsChecked = appSettings.AlwaysOnTop
        };
        alwaysOnTopItem.Click += (_, _) => SetAlwaysOnTop(alwaysOnTopItem.IsChecked);
        settingsMenu.Items.Add(alwaysOnTopItem);
    }

    private void SetTheme(string theme)
    {
        appSettings.Theme = theme;
        ApplyTheme(theme);
        SaveSettings();
    }

    private void ApplyTheme(string theme)
    {
        bool lightTheme = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        Resources["WindowBackgroundBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(242, 246, 252)
            : Color.FromRgb(16, 22, 32));
        Resources["CardBackgroundBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(255, 255, 255)
            : Color.FromRgb(28, 38, 53));
        Resources["CardBorderBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(213, 224, 238)
            : Color.FromRgb(50, 64, 84));
        Resources["PrimaryTextBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(33, 50, 74)
            : Color.FromRgb(243, 244, 246));
        Resources["SecondaryTextBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(83, 100, 124)
            : Color.FromRgb(181, 192, 208));
        Resources["MutedTextBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(103, 124, 149)
            : Color.FromRgb(137, 151, 170));
        Resources["CardSecondaryTextBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(82, 98, 120)
            : Color.FromRgb(181, 192, 208));
        Resources["CardMutedTextBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(101, 119, 143)
            : Color.FromRgb(137, 151, 170));
        Resources["AccentTextBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(22, 132, 234)
            : Color.FromRgb(66, 170, 245));
        Resources["StatusTextBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(5, 151, 75)
            : Color.FromRgb(85, 230, 176));
        Resources["StatusDotBrush"] = new SolidColorBrush(lightTheme
            ? Color.FromRgb(10, 170, 85)
            : Color.FromRgb(85, 230, 176));
        Resources["CpuTintBrush"] = new SolidColorBrush(Color.FromArgb(lightTheme ? (byte)36 : (byte)28, 59, 130, 246));
        Resources["GpuTintBrush"] = new SolidColorBrush(Color.FromArgb(lightTheme ? (byte)36 : (byte)28, 52, 211, 153));
        Resources["HotSpotTintBrush"] = new SolidColorBrush(Color.FromArgb(lightTheme ? (byte)36 : (byte)28, 245, 158, 11));
        Resources["MemoryTintBrush"] = new SolidColorBrush(Color.FromArgb(lightTheme ? (byte)36 : (byte)28, 139, 92, 246));
        Resources["FpsTintBrush"] = new SolidColorBrush(Color.FromArgb(lightTheme ? (byte)36 : (byte)28, 52, 211, 153));

        foreach (MenuItem item in themeItems)
            item.IsChecked = string.Equals(item.Tag as string, theme, StringComparison.OrdinalIgnoreCase);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        settingsMenu.PlacementTarget = btnSettings;
        settingsMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        settingsMenu.IsOpen = true;
    }

    private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
    {
        SetAlwaysOnTop(!Topmost);
        foreach (object menuItem in settingsMenu.Items)
        {
            if (menuItem is MenuItem { Header: "Always on top" } item)
                item.IsChecked = Topmost;
        }
    }

    private void SetAlwaysOnTop(bool enabled)
    {
        Topmost = enabled;
        appSettings.AlwaysOnTop = enabled;
        UpdateAlwaysOnTopButton();
        SaveSettings();
    }

    private void UpdateAlwaysOnTopButton()
    {
        btnAlwaysOnTop.Foreground = Topmost
            ? (Brush)Resources["AccentTextBrush"]
            : (Brush)Resources["SecondaryTextBrush"];
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not Button)
            DragMove();
    }

    private void UpdateTemperatures()
    {
        if (computer is null)
            return;

        float? cpuTemp = null;
        float? gpuCore = null;
        float? gpuHotSpot = null;
        float? gpuMemory = null;
        float? fps = null;

        foreach (IHardware hardware in computer.Hardware)
        {
            hardware.Update();

            if (hardware.HardwareType == HardwareType.Cpu)
            {
                lblCPUName.Text = hardware.Name;
                ISensor[] cpuTemperatureSensors = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                    .ToArray();

                cpuTemp = cpuTemperatureSensors
                    .FirstOrDefault(sensor => sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))?.Value
                    ?? cpuTemperatureSensors.FirstOrDefault(sensor => sensor.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase))?.Value
                    ?? cpuTemperatureSensors.FirstOrDefault(sensor =>
                        sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                        || sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase))?.Value
                    ?? cpuTemperatureSensors.FirstOrDefault()?.Value;
            }

            if (hardware.HardwareType != HardwareType.GpuAmd)
                continue;

            lblGPUName.Text = hardware.Name;
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Factor && sensor.Name == "Fullscreen FPS")
                {
                    fps = sensor.Value;
                    continue;
                }

                if (sensor.SensorType != SensorType.Temperature)
                    continue;

                switch (sensor.Name)
                {
                    case "GPU Core":
                        gpuCore = sensor.Value;
                        break;
                    case "GPU Hot Spot":
                        gpuHotSpot = sensor.Value;
                        break;
                    case "GPU Memory":
                        gpuMemory = sensor.Value;
                        break;
                }
            }
        }

        lblCPU.Text = FormatTemp(cpuTemp);
        lblGPU.Text = FormatTemp(gpuCore);
        lblHotSpot.Text = FormatTemp(gpuHotSpot);
        lblMemory.Text = FormatTemp(gpuMemory);
        lblFPS.Text = fps is > 0 ? $"{fps.Value:F0}" : "--";
        lblCPU.Foreground = new SolidColorBrush(GetCpuTemperatureColor(cpuTemp));
        lblGPU.Foreground = new SolidColorBrush(GetGpuCoreTemperatureColor(gpuCore));
        lblHotSpot.Foreground = new SolidColorBrush(GetGpuHotSpotTemperatureColor(gpuHotSpot));
        lblMemory.Foreground = new SolidColorBrush(GetGpuMemoryTemperatureColor(gpuMemory));
    }

    private static Color GetCpuTemperatureColor(float? value)
    {
        if (!value.HasValue) return Color.FromRgb(175, 175, 175);
        if (value.Value <= 45) return Color.FromRgb(74, 222, 128);
        if (value.Value <= 70) return Color.FromRgb(163, 230, 53);
        if (value.Value <= 85) return Color.FromRgb(250, 204, 21);
        if (value.Value < 100) return Color.FromRgb(251, 146, 60);
        return Color.FromRgb(248, 113, 113);
    }

    private static Color GetGpuCoreTemperatureColor(float? value)
    {
        if (!value.HasValue) return Color.FromRgb(175, 175, 175);
        if (value.Value <= 65) return Color.FromRgb(74, 222, 128);
        if (value.Value <= 80) return Color.FromRgb(163, 230, 53);
        if (value.Value <= 90) return Color.FromRgb(251, 146, 60);
        return Color.FromRgb(248, 113, 113);
    }

    private static Color GetGpuHotSpotTemperatureColor(float? value)
    {
        if (!value.HasValue) return Color.FromRgb(175, 175, 175);
        if (value.Value <= 80) return Color.FromRgb(74, 222, 128);
        if (value.Value <= 95) return Color.FromRgb(163, 230, 53);
        if (value.Value <= 105) return Color.FromRgb(251, 146, 60);
        return Color.FromRgb(248, 113, 113);
    }

    private static Color GetGpuMemoryTemperatureColor(float? value)
    {
        if (!value.HasValue) return Color.FromRgb(175, 175, 175);
        if (value.Value <= 65) return Color.FromRgb(74, 222, 128);
        if (value.Value <= 80) return Color.FromRgb(163, 230, 53);
        if (value.Value <= 90) return Color.FromRgb(250, 204, 21);
        if (value.Value <= 100) return Color.FromRgb(251, 146, 60);
        return Color.FromRgb(248, 113, 113);
    }

    private static string FormatTemp(float? value) => value.HasValue
        ? $"{value.Value:F0}°"
        : "--°";

    private AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsFilePath))
                return new AppSettings();

            AppSettings? loadedSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsFilePath));
            if (loadedSettings is null || !RefreshRateOptions.Contains(loadedSettings.RefreshInterval))
                return new AppSettings();

            if (!string.Equals(loadedSettings.Theme, "Light", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(loadedSettings.Theme, "Dark", StringComparison.OrdinalIgnoreCase))
                loadedSettings.Theme = "Dark";

            return loadedSettings;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
            File.WriteAllText(settingsFilePath, JsonSerializer.Serialize(appSettings));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettings();
        timer.Stop();
        computer?.Close();
        base.OnClosed(e);
    }

    private sealed class AppSettings
    {
        public int RefreshInterval { get; set; } = 1000;
        public bool AlwaysOnTop { get; set; }
        public string Theme { get; set; } = "Dark";
    }
}