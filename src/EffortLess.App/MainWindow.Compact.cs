using EffortLess.Core.Library;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace EffortLess.App;

public sealed partial class MainWindow
{
    private bool _compact;
    private WindowPlacement? _beforeCompact;
    private bool _beforeCompactTopmost;
    private bool _beforeCompactQueue;

    private void ToggleCompactPlayer()
    {
        if (_closed || AppWindow.Presenter is not OverlappedPresenter presenter) return;
        if (_compact)
        {
            _compact = false;
            presenter.IsAlwaysOnTop = _beforeCompactTopmost;
            RestoreWindowPlacement(_beforeCompact);
            QueuePanel.Visibility = _beforeCompactQueue ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            _beforeCompact = CurrentWindowPlacement();
            _beforeCompactTopmost = presenter.IsAlwaysOnTop;
            _beforeCompactQueue = QueuePanel.Visibility == Visibility.Visible;
            if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore();
            _compact = true;
            QueuePanel.Visibility = Visibility.Collapsed;
            presenter.IsAlwaysOnTop = true;
            // Keep this same HWND, engine and XAML focus scope. Size for the
            // current display aspect rather than creating a second video window.
            double scale = Root.XamlRoot.RasterizationScale;
            double aspect = _state.VideoAspect > 0 ? _state.VideoAspect : 16.0 / 9;
            double chromeWidth = Math.Max(0, AppWindow.Size.Width - Root.ActualWidth * scale);
            double chromeHeight = Math.Max(0, AppWindow.Size.Height - Root.ActualHeight * scale);
            double shellHeight = (_appearanceResources.Dimensions?.HeaderHeight ?? VisualSize("PlayerHeaderHeight")) +
                (_appearanceResources.Dimensions?.TransportHeight ?? VisualSize("PlayerTransportHeight"));
            double contentWidth = _appearanceResources.Dimensions?.CompactWidth ?? VisualSize("PlayerCompactWidth");
            int width = (int)Math.Round(contentWidth * scale + chromeWidth);
            int height = (int)Math.Round(Math.Max(VisualSize("PlayerMinimumHeight"), contentWidth / aspect + shellHeight) * scale + chromeHeight);
            var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
            width = Math.Min(width, work.Width);
            height = Math.Min(height, work.Height);
            AppWindow.MoveAndResize(new RectInt32(Math.Clamp(AppWindow.Position.X, work.X, work.X + work.Width - width),
                Math.Clamp(AppWindow.Position.Y, work.Y, work.Y + work.Height - height), width, height));
        }
        QueueVideoHostLayout();
    }
}
