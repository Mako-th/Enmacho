using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;

internal partial class MainWindow : Window
{
    private ShellViewModel? _shell;
    private StreamPanelWindow? _streamPanel;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => AttachStreamPanel();
        Closed += (_, _) => _streamPanel?.Close();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void AttachStreamPanel()
    {
        if (_shell is not null) _shell.PropertyChanged -= OnShellPropertyChanged;
        _shell = DataContext as ShellViewModel;
        if (_shell is null) return;
        _shell.PropertyChanged += OnShellPropertyChanged;
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellViewModel.IsStreamPanelOpen) || _shell is null) return;
        if (_shell.IsStreamPanelOpen) { OpenStreamPanel(_shell); return; }
        if (_streamPanel is { IsOffscreen: true } offscreen)
        {
            offscreen.RestoreOnscreen();
            _shell.IsStreamPanelOpen = true;
            return;
        }
        _streamPanel?.Close();
    }

    private void OpenStreamPanel(ShellViewModel shell)
    {
        if (_streamPanel is not null) return;
        var window = new StreamPanelWindow { DataContext = shell.StreamPanel };
        window.ShowActivated = false;
        window.Closed += (_, _) =>
        {
            _streamPanel = null;
            shell.IsStreamPanelOpen = false;
        };
        _streamPanel = window;
        window.Show();
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;
        var p = e.GetCurrentPoint(this).Properties;
        if (p.IsXButton1Pressed) { shell.NavigateBack(); e.Handled = true; }
        else if (p.IsXButton2Pressed) { shell.NavigateForward(); e.Handled = true; }
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10)
        {
            RendererDiagnostics.DebugOverlays = RendererDiagnostics.DebugOverlays switch
            {
                RendererDebugOverlays.None => RendererDebugOverlays.Fps,
                RendererDebugOverlays.Fps => RendererDebugOverlays.Fps
                                             | RendererDebugOverlays.RenderTimeGraph
                                             | RendererDebugOverlays.LayoutTimeGraph,
                RendererDebugOverlays.Fps | RendererDebugOverlays.RenderTimeGraph
                    | RendererDebugOverlays.LayoutTimeGraph
                    => RendererDebugOverlays.Fps | RendererDebugOverlays.DirtyRects,
                _ => RendererDebugOverlays.None,
            };
            e.Handled = true;
            return;
        }
        if (e.Key is Key.F11 or Key.F12)
        {
            var names = e.Key == Key.F11
                ? new[] { "Board" }
                : ["Gauge", "StripTicks", "StripStrips"];
            int hit = 0;
            foreach (var c in this.GetVisualDescendants().OfType<Control>())
                if (Array.IndexOf(names, c.Name) >= 0) { c.IsVisible = !c.IsVisible; hit++; }
            e.Handled = hit > 0;
            return;
        }
        if (DataContext is not ShellViewModel shell) return;
        if (shell.Drive.MonitorToggleKey is Key toggle && e.Key == toggle)
        {
            shell.Drive.ToggleMonitorCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
        if (e.Key == Key.Left) { shell.NavigateBack(); e.Handled = true; }
        else if (e.Key == Key.Right) { shell.NavigateForward(); e.Handled = true; }
    }

    private async void OnAddScanDirectory(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var picked = await top.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "走査の対象にするフォルダ",
                    AllowMultiple = true,
                });
            foreach (var folder in picked)
                shell.Drive.Scan.AddDirectory(folder.TryGetLocalPath());
        }
        catch (Exception ex)
        {
            Data.LogSource.Error("起動", "フォルダを選べませんでした: " + ex.Message);
        }
    }

    private async void OnPickGameDir(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var picked = await top.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "ゲームのフォルダ（th09.exe の在るフォルダ）",
                    AllowMultiple = false,
                });
            if (picked.Count > 0) shell.Drive.Settings.SetGameDir(picked[0].TryGetLocalPath());
        }
        catch (Exception ex)
        {
            Data.LogSource.Error("起動", "フォルダを選べませんでした: " + ex.Message);
        }
    }

    private async void OnAddExtraReplayDir(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var picked = await top.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "追加のリプレイ監視フォルダ",
                    AllowMultiple = true,
                });
            foreach (var folder in picked)
                shell.Drive.Settings.AddExtraReplayDir(folder.TryGetLocalPath());
        }
        catch (Exception ex)
        {
            Data.LogSource.Error("起動", "フォルダを選べませんでした: " + ex.Message);
        }
    }

    public byte[] CapturePng()
    {
        var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
        using var target = new RenderTargetBitmap(size, new Vector(96, 96));
        target.Render(this);
        using var ms = new MemoryStream();
        target.Save(ms, new PngBitmapEncoderOptions());
        return ms.ToArray();
    }
}
