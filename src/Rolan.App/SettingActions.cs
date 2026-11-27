using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using Rolan.Core;

namespace Rolan.App;

internal sealed partial class MainWindow
{
    internal void PreviewOpacity(double value)
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        if (frame.Background != null)
        {
            frame.Background.Opacity = Math.Clamp(value, .65, 1);
        }
    }
    internal double PanelBackgroundOpacity => frame.Background?.Opacity ?? 1;
    internal void ApplySetting(string field, object value)
    {
        var member = typeof(Preferences).GetField(field) ?? throw new ArgumentException("不存在的设置。");
        var previous = member.GetValue(Data.Settings)!;
        if (Equals(previous, value))
        {
            return;
        }
        member.SetValue(Data.Settings, value);
        try
        {
            if (field == nameof(Preferences.HoverDelay) && (Data.Settings.HoverDelay < 50 || Data.Settings.HoverDelay > 2000))
            {
                throw new ArgumentException("延迟应为50至2000毫秒。");
            }
            if (field == nameof(Preferences.Opacity) && (Data.Settings.Opacity < .65 || Data.Settings.Opacity > 1))
            {
                throw new ArgumentException("不透明度应为65%至100%。");
            }
            if (field == nameof(Preferences.AutoStart))
            {
                Native.SetAutoStart(Data.Settings.AutoStart);
            }
            if (field is nameof(Preferences.Hotkey) or nameof(Preferences.HotkeyEnabled))
            {
                RefreshDesktopSettings(true);
            }
            ApplySettingEffect(field);
            if (!Data.Settings.LockConfig || field == nameof(Preferences.LockConfig))
            {
                Store.Save(Data);
            }
        }
        catch
        {
            member.SetValue(Data.Settings, previous);
            if (field is nameof(Preferences.Hotkey) or nameof(Preferences.HotkeyEnabled))
            {
                RefreshDesktopSettings();
            }
            if (field == nameof(Preferences.AutoStart))
            {
                Native.SetAutoStart(Data.Settings.AutoStart);
            }
            ApplySettingEffect(field);
            throw;
        }
        SettingsChanged?.Invoke(field);
    }
    private void ApplySettingEffect(string field)
    {
        switch (field)
        {
            case nameof(Preferences.Animation): case nameof(Preferences.Docked): AdvancePanelMotion(); break;
            case nameof(Preferences.Opacity): PreviewOpacity(Data.Settings.Opacity); break;
            case nameof(Preferences.AlwaysOnTop): Topmost = Data.Settings.AlwaysOnTop; break;
            case nameof(Preferences.ShowTaskbar): ShowInTaskbar = Data.Settings.ShowTaskbar; break;
            case nameof(Preferences.LockSize): ResizeMode = Data.Settings.LockSize ? ResizeMode.NoResize : ResizeMode.CanResize; break;
            case nameof(Preferences.Theme): case nameof(Preferences.CustomColor): case nameof(Preferences.CustomPalettes): ApplyAppearance(false); break;
            case nameof(Preferences.TabPosition): case nameof(Preferences.Layout): case nameof(Preferences.IconSize): case nameof(Preferences.TextBelow): case nameof(Preferences.ShowSearch): case nameof(Preferences.ShowScrollbar): case nameof(Preferences.Tooltips): case nameof(Preferences.SortByCount): ApplyAppearance(); break;
        }
    }
}
