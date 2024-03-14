namespace Ryujinx.Horizon.UI
{
    public interface IHostUITheme
    {
        string FontFamily { get; }

        ThemeColor DefaultBackgroundColor { get; }
        ThemeColor DefaultForegroundColor { get; }
        ThemeColor DefaultBorderColor { get; }
        ThemeColor SelectionBackgroundColor { get; }
        ThemeColor SelectionForegroundColor { get; }
    }
}
