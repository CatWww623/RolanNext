using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using Rolan.Core;

namespace Rolan.App;

internal sealed record Theme(string Name, string Category, Color Background, Color Secondary, Color Surface, Color Accent, Color Text, Color AccentText, Color? Middle = null);

internal static class Themes
{
    private static Color ColorOf(string color) => (Color)ColorConverter.ConvertFromString(color);
    private static Theme Make(string name, string category, string background, string secondary, string accent, bool dark = false, string? middle = null)
    {
        return Compose(name, category, ColorOf(background), ColorOf(secondary), ColorOf(accent), dark, middle == null ? null : ColorOf(middle));
    }
    internal static readonly Theme[] All =
    [
        Make("极简白", "精品尝鲜", "#F5F6FA", "#FFFFFF", "#526174"),
        Make("经典蓝", "精品尝鲜", "#E5F1FF", "#BBDFFF", "#246EBE"),
        Make("地海蔚蓝", "精品尝鲜", "#DEF0FF", "#DDE0FF", "#2678BA"),
        Make("樱语红粉", "精品尝鲜", "#FFF0F5", "#F6D3E5", "#A94670"),
        Make("触手生春", "精品尝鲜", "#EFF8DC", "#F8E7D5", "#547329"),
        Make("典雅金", "水墨国风", "#F5EDD9", "#E8D3AC", "#876328"),
        Make("丁香紫", "水墨国风", "#F1EAF5", "#D9C4E2", "#78538E"),
        Make("芸豆青", "水墨国风", "#EAF0E6", "#CFD9C9", "#526B50"),
        Make("辰砂色", "水墨国风", "#F9EAE5", "#E8BCB4", "#A0433D"),
        Make("心想事橙", "神桃美学", "#FFE6A2", "#FFD5EE", "#A65A17", middle: "#FFD0B7"),
        Make("不再焦绿", "神桃美学", "#D7F6A5", "#F7E6F3", "#4B751D", middle: "#E4F5D7"),
        Make("蓝以为情", "神桃美学", "#A7E4FF", "#F4D3FC", "#3766A6", middle: "#DFEEFF"),
        Make("紫属于你", "神桃美学", "#F5C4EA", "#ADF0F1", "#8550AB", middle: "#EBD6F9"),
        Make("香芋啵啵", "缤纷时刻", "#BDA9FF", "#FFD1BB", "#8752B5", middle: "#F2A7F4"),
        Make("海盐粉荔", "缤纷时刻", "#A8C8FF", "#FFAFCB", "#5758A6", middle: "#D5B1E9"),
        Make("橘子汽水", "缤纷时刻", "#FFD1AF", "#EBC1ED", "#A95E3B", middle: "#FFB18F"),
        Make("绿豆冰沙", "缤纷时刻", "#8EDBB8", "#A9DBF4", "#266E66", middle: "#ACDFD0"),
        Make("霓虹灯光", "幻彩机核", "#D86AEF", "#A39AFF", "#7134AD", middle: "#EE78D9"),
        Make("粉墨都市", "幻彩机核", "#B46ACD", "#D7A0A6", "#813A64", middle: "#E5A06A"),
        Make("废土朋克", "幻彩机核", "#83B899", "#B8A568", "#496944", middle: "#D8B36C"),
        Make("魔幻丛林", "幻彩机核", "#46C2A8", "#75B7D6", "#155F60", middle: "#5BC9DC"),
        Make("星河夜色", "深夜工作室", "#101623", "#202B47", "#87A7FF", true),
        Make("极光青", "深夜工作室", "#111E2B", "#203F48", "#75E0D1", true),
        Make("落日余晖", "深夜工作室", "#251C30", "#503648", "#F6AC89", true)
    ];
    internal static Theme Find(string name, string custom, System.Collections.Generic.Dictionary<string, CustomPalette>? palettes = null)
    {
        if (name == "自选颜色" || name.StartsWith("自选颜色 · ", StringComparison.Ordinal))
        {
            try
            {
                var category = name == "自选颜色" ? "水墨国风" : name[7..];
                if (!Categories.Contains(category))
                {
                    return All[0];
                }
                var palette = palettes != null && palettes.TryGetValue(category, out var saved) ? saved : DefaultPalette(category, custom);
                return CustomTheme(category, palette) with { Name = name };
            }
            catch
            {
                return All[0];
            }
        }
        return All.FirstOrDefault(t => t.Name == name) ?? All[^2];
    }
    internal static string[] Categories => All.Select(t => t.Category).Distinct().ToArray();
    internal static Theme Current(Preferences settings) => Find(settings.Theme, settings.CustomColor, settings.CustomPalettes);
    internal static string CustomName(string category) => "自选颜色 · " + category;
    internal static CustomPalette DefaultPalette(string category, string? legacy = null)
    {
        var example = All.First(t => t.Category == category);
        return new CustomPalette { ColorCount = category == "水墨国风" || legacy != null ? 1 : category is "神桃美学" or "精品尝鲜" ? 2 : 3, Colors = [legacy ?? PaletteColors.Hex(example.Background), PaletteColors.Hex(example.Middle ?? example.Secondary), PaletteColors.Hex(example.Secondary)] };
    }
    internal static Theme CustomTheme(string category, CustomPalette palette)
    {
        if (palette.ColorCount < 1 || palette.ColorCount > 3 || palette.Colors.Count < palette.ColorCount)
        {
            throw new ArgumentException("自选配色的颜色数量无效。");
        }
        var colors = palette.Colors.Take(palette.ColorCount).Select(c => PaletteColors.TryHex(c, out var parsed) ? parsed : throw new ArgumentException("颜色值无效。")).ToArray();
        var primary = colors[0];
        var dark = category == "深夜工作室" || colors.Average(PaletteColors.Luminance) < .1;
        Color first;
        Color last;
        Color? middle = null;
        if (palette.ColorCount == 1)
        {
            var hsv = PaletteColors.ToHsv(primary);
            var related = PaletteColors.FromHsv(hsv.Hue + (category == "水墨国风" ? 8 : category == "幻彩机核" ? 70 : 35), hsv.Saturation, hsv.Value);
            var baseColor = dark ? ColorOf("#0D1523") : Colors.White;
            var softness = dark ? .72 : category == "水墨国风" ? .68 : category == "幻彩机核" ? .15 : .48;
            first = PaletteColors.Mix(primary, baseColor, softness);
            last = PaletteColors.Mix(related, baseColor, Math.Min(.88, softness + .13));
        }
        else
        {
            first = colors[0];
            last = colors[^1];
            middle = colors.Length == 3 ? colors[1] : null;
        }
        return Compose(CustomName(category), category, first, last, primary, dark, middle);
    }
    private static Theme Compose(string name, string category, Color first, Color last, Color accent, bool dark, Color? middle)
    {
        var text = ColorOf(dark ? "#F2F5FF" : "#202837");
        var neutral = dark ? Colors.Black : Colors.White;
        // 极端明暗混合时，协调背景明度，使渐变途中的文字也保持可读。
        for (var iteration = 0; iteration < 60; iteration++)
        {
            var readable = Enumerable.Range(0, 33).All(i => PaletteColors.Contrast(text, Sample(first, middle, last, i / 32.0)) >= 4.5);
            if (readable)
            {
                break;
            }
            first = PaletteColors.Mix(first, neutral, .05);
            last = PaletteColors.Mix(last, neutral, .05);
            middle = middle.HasValue ? PaletteColors.Mix(middle.Value, neutral, .05) : null;
        }
        var average = Sample(first, middle, last, .5);
        var surface = PaletteColors.Mix(average, dark ? ColorOf("#151C2A") : Colors.White, .85);
        var button = PaletteColors.EnsureContrast(accent, surface, 3);
        return new Theme(name, category, first, last, surface, button, text, PaletteColors.ReadableOn(button), middle);
    }
    private static Color Sample(Color first, Color? middle, Color last, double progress) => middle.HasValue ? progress < .5 ? PaletteColors.Mix(first, middle.Value, progress * 2) : PaletteColors.Mix(middle.Value, last, (progress - .5) * 2) : PaletteColors.Mix(first, last, progress);
    internal static LinearGradientBrush BackgroundBrush(Theme theme)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(theme.Background, 0));
        if (theme.Middle.HasValue)
        {
            brush.GradientStops.Add(new GradientStop(theme.Middle.Value, .5));
        }
        brush.GradientStops.Add(new GradientStop(theme.Secondary, 1));
        return brush;
    }
    internal static void Install(Application app)
    {
        var resources = (ResourceDictionary)XamlReader.Parse("""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <SolidColorBrush x:Key="TextColor" Color="#273345" />
 <SolidColorBrush x:Key="SurfaceColor" Color="#FFFFFF" />
 <SolidColorBrush x:Key="InputSurface" Color="#FFFFFF" />
 <SolidColorBrush x:Key="OutlineColor" Color="#8095A6" />
 <SolidColorBrush x:Key="AccentColor" Color="#246EBE" />
 <SolidColorBrush x:Key="AccentTextColor" Color="#FFFFFF" />
 <SolidColorBrush x:Key="WindowBackground" Color="#F5F6FA" />
 <SolidColorBrush x:Key="PopupSurface" Color="#FFFFFF" />
 <SolidColorBrush x:Key="HoverColor" Color="#E3EAF2" />
 <SolidColorBrush x:Key="ScrollThumb" Color="#7A8DA2" />
 <Style TargetType="Button">
  <Setter Property="Background" Value="{DynamicResource InputSurface}" />
  <Setter Property="BorderBrush" Value="{DynamicResource OutlineColor}" />
  <Setter Property="BorderThickness" Value="1" />
  <Setter Property="Foreground" Value="{DynamicResource TextColor}" />
  <Setter Property="Padding" Value="12,8" /><Setter Property="Margin" Value="3" />
  <Setter Property="Cursor" Value="Hand" /><Setter Property="SnapsToDevicePixels" Value="True" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
   <Grid>
    <Border Background="{TemplateBinding Background}" CornerRadius="9" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
     <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" />
    </Border>
    <Border x:Name="hover" IsHitTestVisible="False" CornerRadius="9" BorderThickness="1" BorderBrush="{DynamicResource AccentColor}" Opacity="0" />
    <Border x:Name="focus" IsHitTestVisible="False" CornerRadius="9" BorderThickness="2" BorderBrush="{DynamicResource AccentColor}" Opacity="0" />
   </Grid>
   <ControlTemplate.Triggers>
    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="hover" Property="Opacity" Value="1" /></Trigger>
    <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="focus" Property="Opacity" Value="1" /></Trigger>
    <Trigger Property="IsPressed" Value="True"><Setter TargetName="focus" Property="Opacity" Value="1" /></Trigger>
    <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45" /></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="TextBox">
  <Setter Property="Padding" Value="10,8" /><Setter Property="Margin" Value="0,5,0,10" />
  <Setter Property="Foreground" Value="{DynamicResource TextColor}" /><Setter Property="CaretBrush" Value="{DynamicResource TextColor}" />
  <Setter Property="Background" Value="{DynamicResource InputSurface}" /><Setter Property="BorderBrush" Value="{DynamicResource OutlineColor}" /><Setter Property="BorderThickness" Value="1" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="border" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1"><ScrollViewer x:Name="PART_ContentHost" /></Border><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="border" Property="BorderBrush" Value="{DynamicResource AccentColor}" /></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="CheckBox">
  <Setter Property="Margin" Value="0,9" /><Setter Property="Foreground" Value="{DynamicResource TextColor}" /><Setter Property="Cursor" Value="Hand" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><Grid><Grid.ColumnDefinitions><ColumnDefinition /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions><ContentPresenter VerticalAlignment="Center" Margin="0,0,16,0" /><Border x:Name="track" Grid.Column="1" Width="38" Height="22" CornerRadius="11" Background="{DynamicResource OutlineColor}"><Ellipse x:Name="thumb" Width="16" Height="16" Margin="3" HorizontalAlignment="Left" Fill="White" /></Border></Grid><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="track" Property="Background" Value="{DynamicResource AccentColor}" /><Setter TargetName="thumb" Property="HorizontalAlignment" Value="Right" /></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="track" Property="BorderBrush" Value="{DynamicResource TextColor}" /><Setter TargetName="track" Property="BorderThickness" Value="1" /></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBox">
  <Setter Property="Margin" Value="0,5,0,10" /><Setter Property="Padding" Value="11,8" /><Setter Property="Foreground" Value="{DynamicResource TextColor}" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid><ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border CornerRadius="8" Background="{DynamicResource InputSurface}" BorderBrush="{DynamicResource OutlineColor}" BorderThickness="1"><Grid Margin="11,8"><TextBlock Text="⌄" HorizontalAlignment="Right" Foreground="{DynamicResource TextColor}" /></Grid></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter IsHitTestVisible="False" Margin="11,8,30,8" Content="{TemplateBinding SelectionBoxItem}" VerticalAlignment="Center" /><Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False"><Border MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="320" CornerRadius="8" Padding="5" Background="{DynamicResource InputSurface}" BorderBrush="{DynamicResource OutlineColor}" BorderThickness="1"><ScrollViewer><ItemsPresenter /></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBoxItem"><Setter Property="Foreground" Value="{DynamicResource TextColor}" /><Setter Property="Padding" Value="10,8" /><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="selection" Background="Transparent" CornerRadius="6" Padding="{TemplateBinding Padding}"><ContentPresenter /></Border><ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="selection" Property="Background" Value="{DynamicResource SurfaceColor}" /></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="selection" Property="BorderBrush" Value="{DynamicResource AccentColor}" /><Setter TargetName="selection" Property="BorderThickness" Value="1" /></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>

 <Style TargetType="ContextMenu">
  <Setter Property="OverridesDefaultStyle" Value="True" /><Setter Property="Foreground" Value="{DynamicResource TextColor}" />
  <Setter Property="Background" Value="{DynamicResource PopupSurface}" /><Setter Property="BorderBrush" Value="{DynamicResource OutlineColor}" />
  <Setter Property="Padding" Value="6" /><Setter Property="MinWidth" Value="200" /><Setter Property="HasDropShadow" Value="False" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ContextMenu"><Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="11" Padding="{TemplateBinding Padding}"><ScrollViewer MaxHeight="720" CanContentScroll="True" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Cycle" SnapsToDevicePixels="True" /></ScrollViewer></Border></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="MenuItem">
  <Setter Property="OverridesDefaultStyle" Value="True" /><Setter Property="Foreground" Value="{DynamicResource TextColor}" />
  <Setter Property="Background" Value="Transparent" /><Setter Property="Padding" Value="10,8" /><Setter Property="MinHeight" Value="34" /><Setter Property="HorizontalContentAlignment" Value="Stretch" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="MenuItem">
   <Grid SnapsToDevicePixels="True">
    <Border x:Name="row" CornerRadius="7" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
     <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="18" /><ColumnDefinition Width="*" /><ColumnDefinition Width="22" /></Grid.ColumnDefinitions>
      <ContentPresenter ContentSource="Icon" VerticalAlignment="Center" />
      <TextBlock x:Name="check" Text="✓" Visibility="Collapsed" VerticalAlignment="Center" />
      <ContentPresenter Grid.Column="1" ContentSource="Header" RecognizesAccessKey="True" VerticalAlignment="Center" />
      <TextBlock x:Name="arrow" Grid.Column="2" Text="›" FontSize="17" Margin="10,-2,0,0" Visibility="Collapsed" VerticalAlignment="Center" HorizontalAlignment="Right" />
     </Grid>
    </Border>
    <Popup x:Name="PART_Popup" IsOpen="{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}" Placement="Right" HorizontalOffset="-1" AllowsTransparency="True" Focusable="False" PopupAnimation="None">
     <Border Background="{DynamicResource PopupSurface}" BorderBrush="{DynamicResource OutlineColor}" BorderThickness="1" CornerRadius="11" Padding="6" MinWidth="190"><ScrollViewer MaxHeight="650" CanContentScroll="True" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Cycle" /></ScrollViewer></Border>
    </Popup>
   </Grid>
   <ControlTemplate.Triggers>
    <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="row" Property="Background" Value="{DynamicResource HoverColor}" /></Trigger>
    <Trigger Property="IsSubmenuOpen" Value="True"><Setter TargetName="row" Property="Background" Value="{DynamicResource HoverColor}" /></Trigger>
    <Trigger Property="HasItems" Value="True"><Setter TargetName="arrow" Property="Visibility" Value="Visible" /></Trigger>
    <Trigger Property="IsChecked" Value="True"><Setter TargetName="check" Property="Visibility" Value="Visible" /></Trigger>
    <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45" /></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="Separator"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Separator"><Border Height="1" Margin="12,5" Background="{DynamicResource OutlineColor}" Opacity="0.5" /></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="{x:Static MenuItem.SeparatorStyleKey}" TargetType="Separator" BasedOn="{StaticResource {x:Type Separator}}" />
 <Style x:Key="ScrollPageButton" TargetType="RepeatButton"><Setter Property="Focusable" Value="False" /><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent" /></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="RoundedScrollThumb" TargetType="Thumb">
  <Setter Property="Background" Value="{DynamicResource ScrollThumb}" /><Setter Property="MinHeight" Value="24" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Thumb"><Border x:Name="grip" Margin="3,1" CornerRadius="3" Background="{TemplateBinding Background}" /><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="grip" Property="Background" Value="{DynamicResource AccentColor}" /></Trigger><Trigger Property="IsDragging" Value="True"><Setter TargetName="grip" Property="Background" Value="{DynamicResource AccentColor}" /></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ScrollBar">
  <Setter Property="OverridesDefaultStyle" Value="True" /><Setter Property="Width" Value="12" /><Setter Property="Background" Value="Transparent" /><Setter Property="Focusable" Value="False" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="Transparent"><Track x:Name="PART_Track" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="True" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" ViewportSize="{TemplateBinding ViewportSize}" Value="{Binding Value, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"><Track.DecreaseRepeatButton><RepeatButton Style="{StaticResource ScrollPageButton}" Command="{x:Static ScrollBar.PageUpCommand}" /></Track.DecreaseRepeatButton><Track.Thumb><Thumb Style="{StaticResource RoundedScrollThumb}" /></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Style="{StaticResource ScrollPageButton}" Command="{x:Static ScrollBar.PageDownCommand}" /></Track.IncreaseRepeatButton></Track></Grid><ControlTemplate.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False" /></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  <Style.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter Property="Width" Value="Auto" /><Setter Property="Height" Value="12" /></Trigger></Style.Triggers>
 </Style>
 <Style x:Key="SliderPage" TargetType="RepeatButton"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RepeatButton"><Border Height="4" VerticalAlignment="Center" CornerRadius="2" Background="{TemplateBinding Background}" /></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="Slider">
  <Setter Property="Foreground" Value="{DynamicResource AccentColor}" /><Setter Property="MinHeight" Value="24" /><Setter Property="IsMoveToPointEnabled" Value="True" />
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider"><Grid Margin="0,4"><Track x:Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{Binding Value, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}" Orientation="Horizontal"><Track.DecreaseRepeatButton><RepeatButton Style="{StaticResource SliderPage}" Background="{DynamicResource AccentColor}" Command="{x:Static Slider.DecreaseLarge}" /></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width="16" Height="16"><Thumb.Template><ControlTemplate TargetType="Thumb"><Ellipse Fill="{DynamicResource AccentColor}" Stroke="{DynamicResource InputSurface}" StrokeThickness="2" /></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Style="{StaticResource SliderPage}" Background="{DynamicResource OutlineColor}" Command="{x:Static Slider.IncreaseLarge}" /></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
</ResourceDictionary>
""");
        app.Resources.MergedDictionaries.Add(resources);
    }
}
