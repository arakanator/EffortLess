using EffortLess.Core.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace EffortLess.App;

public sealed partial class MainWindow
{
    private readonly Flyout _subtitleSettingsFlyout = new()
    {
        Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight,
        ShouldConstrainToRootBounds = false
    };
    private readonly Dictionary<string, NumberBox> _subtitleNumbers = new();
    private readonly ComboBox _subtitleFont = new() { IsEditable = true, Header = "Font family", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _subtitleBold = new() { Content = "Bold" }, _subtitleItalic = new() { Content = "Italic" };
    private readonly CheckBox _subtitleBackgroundBox = new() { Content = "Background box" };
    private readonly ColorPicker _subtitleTextColor = new() { IsAlphaEnabled = false, IsColorChannelTextInputVisible = false };
    private readonly ColorPicker _subtitleOutlineColor = new() { IsAlphaEnabled = false, IsColorChannelTextInputVisible = false };
    private bool _syncingSubtitles;
    private SubtitleAppearance? _presentedSubtitles;

    private void InitializeSubtitleSettings()
    {
        var content = new StackPanel { Width = VisualSize("PlayerSettingsWidth"), Spacing = VisualSize("PlayerSmallSpacing") };
        content.Children.Add(new TextBlock { Text = "Subtitle Settings", Style = (Style)Application.Current.Resources["PlayerSectionTitle"] });
        content.Children.Add(new TextBlock { Text = "Applies live to text subtitles. Bitmap subtitles keep their original font and colors.", TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources["PlayerLabel"] });
        _subtitleFont.ItemsSource = new[] { "sans-serif", "Segoe UI", "Arial", "Calibri", "Tahoma", "Verdana", "Times New Roman", "Consolas" };
        AutomationProperties.SetName(_subtitleFont, "Subtitle font family");
        content.Children.Add(_subtitleFont);
        _subtitleFont.SelectionChanged += (_, _) => ApplySubtitleControls();
        _subtitleFont.TextSubmitted += (_, _) => ApplySubtitleControls();
        _subtitleFont.LostFocus += (_, _) => ApplySubtitleControls();
        AddSubtitleNumber(content, "FontSize", "Font size", 8, 120);
        AddSubtitleColor(content, "Text color", _subtitleTextColor);
        AddSubtitleNumber(content, "TextOpacity", "Text opacity · 0–1", 0, 1, .05);
        AddSubtitleNumber(content, "VerticalPosition", "Vertical position · 0 top / 100 bottom", 0, 100);
        content.Children.Add(_subtitleBold); content.Children.Add(_subtitleItalic);
        _subtitleBold.Checked += (_, _) => ApplySubtitleControls(); _subtitleBold.Unchecked += (_, _) => ApplySubtitleControls();
        _subtitleItalic.Checked += (_, _) => ApplySubtitleControls(); _subtitleItalic.Unchecked += (_, _) => ApplySubtitleControls();
        AddSubtitleNumber(content, "OutlineSize", "Outline size", 0, 10, .1);
        AddSubtitleColor(content, "Outline color", _subtitleOutlineColor);
        AddSubtitleNumber(content, "ShadowOffset", "Shadow offset", 0, 10, .1);
        content.Children.Add(_subtitleBackgroundBox);
        _subtitleBackgroundBox.Checked += (_, _) => ApplySubtitleControls();
        _subtitleBackgroundBox.Unchecked += (_, _) => ApplySubtitleControls();
        AddSubtitleNumber(content, "BackgroundOpacity", "Background box opacity · 0–1", 0, 1, .05);
        AddSubtitleNumber(content, "Scale", "Subtitle scale", .25, 4, .05);
        AddSubtitleNumber(content, "MarginBottom", "Bottom margin", 0, 200);
        var reset = new Button { Content = "Reset Subtitle Settings" };
        reset.Click += (_, _) => RunSessionAction(s => s.ResetSubtitleAppearanceAsync());
        content.Children.Add(reset);
        _subtitleSettingsFlyout.Content = new ScrollViewer { Content = content, MaxHeight = VisualSize("PlayerSettingsMaxHeight") };
        _subtitleSettingsFlyout.Opening += (_, _) => SyncSubtitleSettings(force: true);
        SyncSubtitleSettings(force: true);
    }

    private void AddSubtitleNumber(StackPanel content, string key, string label, double minimum, double maximum, double step = 1)
    {
        var number = new NumberBox { Header = label, Minimum = minimum, Maximum = maximum, SmallChange = step, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        AutomationProperties.SetName(number, "Subtitle " + label);
        _subtitleNumbers.Add(key, number); content.Children.Add(number);
        number.ValueChanged += (_, _) => ApplySubtitleControls();
    }
    private void AddSubtitleColor(StackPanel content, string label, ColorPicker picker)
    {
        AutomationProperties.SetName(picker, "Subtitle " + label);
        content.Children.Add(new Expander { Header = label, Content = picker, HorizontalAlignment = HorizontalAlignment.Stretch });
        picker.ColorChanged += (_, _) => ApplySubtitleControls();
    }
    private void SyncSubtitleSettings(bool force = false)
    {
        if (_subtitleNumbers.Count == 0) return;
        var settings = _session?.State.Settings.SubtitleAppearance ?? new();
        if (!force && settings == _presentedSubtitles) return;
        _presentedSubtitles = settings; _syncingSubtitles = true;
        try
        {
            _subtitleFont.SelectedItem = settings.FontFamily;
            _subtitleFont.Text = settings.FontFamily;
            _subtitleBold.IsChecked = settings.Bold; _subtitleItalic.IsChecked = settings.Italic;
            _subtitleBackgroundBox.IsChecked = settings.BackgroundBoxEnabled;
            _subtitleTextColor.Color = SubtitleColor(settings.TextColor);
            _subtitleOutlineColor.Color = SubtitleColor(settings.OutlineColor);
            _subtitleNumbers["FontSize"].Value = settings.FontSize;
            _subtitleNumbers["TextOpacity"].Value = settings.TextOpacity;
            _subtitleNumbers["VerticalPosition"].Value = settings.VerticalPosition;
            _subtitleNumbers["OutlineSize"].Value = settings.OutlineSize;
            _subtitleNumbers["ShadowOffset"].Value = settings.ShadowOffset;
            _subtitleNumbers["BackgroundOpacity"].Value = settings.BackgroundOpacity;
            _subtitleNumbers["Scale"].Value = settings.Scale;
            _subtitleNumbers["MarginBottom"].Value = settings.MarginBottom;
        }
        finally { _syncingSubtitles = false; }
    }
    private void ApplySubtitleControls()
    {
        if (_syncingSubtitles || _session is null || _closed) return;
        var appearance = new SubtitleAppearance
        {
            FontFamily = _subtitleFont.Text,
            FontSize = _subtitleNumbers["FontSize"].Value,
            TextColor = SubtitleHex(_subtitleTextColor.Color), OutlineColor = SubtitleHex(_subtitleOutlineColor.Color),
            TextOpacity = _subtitleNumbers["TextOpacity"].Value, VerticalPosition = _subtitleNumbers["VerticalPosition"].Value,
            Bold = _subtitleBold.IsChecked == true, Italic = _subtitleItalic.IsChecked == true,
            OutlineSize = _subtitleNumbers["OutlineSize"].Value, ShadowOffset = _subtitleNumbers["ShadowOffset"].Value,
            BackgroundOpacity = _subtitleNumbers["BackgroundOpacity"].Value,
            BackgroundBoxEnabled = _subtitleBackgroundBox.IsChecked == true,
            Scale = _subtitleNumbers["Scale"].Value, MarginBottom = _subtitleNumbers["MarginBottom"].Value
        }.Normalize();
        if (appearance != _session.State.Settings.SubtitleAppearance)
            RunSessionAction(s => s.SetSubtitleAppearanceAsync(appearance));
    }
    private static string SubtitleHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    private static Color SubtitleColor(string value) => Color.FromArgb(255,
        Convert.ToByte(value.Substring(1, 2), 16), Convert.ToByte(value.Substring(3, 2), 16), Convert.ToByte(value.Substring(5, 2), 16));
}
