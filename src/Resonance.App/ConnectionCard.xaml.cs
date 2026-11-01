using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Resonance.Core.Device;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

using static Resonance.Core.Localization.Loc;

namespace Resonance.App;

/// <summary>
/// The card that greets connected headphones near the notification area: the device floats in and its
/// battery rings fill up, then the card leaves on its own after a few seconds (not while hovered).
/// </summary>
public partial class ConnectionCard : Window
{
    private static readonly Duration In = TimeSpan.FromMilliseconds(380), Rings = TimeSpan.FromMilliseconds(1100);
    private readonly DispatcherTimer _dismiss = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly Action? _open;
    private readonly List<(BatteryRing Ring, int Level)> _rings = [];

    /// <param name="open">Called when the card is clicked (opens the main window).</param>
    public ConnectionCard(string name, HeadphoneForm form, Core.Protocol.ModelColor color, string? picture,
        IReadOnlyList<(string Label, BatteryInfo Battery)> batteries, Action? open, string? animation = null)
    {
        InitializeComponent();
        _open = open;
        TitleText.Text = name;
        Subtitle.Text = T("card.connected");
        Art.Form = form;
        Art.BodyColor = color;
        Art.ImagePath = picture;
        Art.AnimationFolder = animation;
        foreach (var (label, battery) in batteries) Batteries.Items.Add(BatteryItem(label, battery));
        _dismiss.Tick += (_, _) => { if (!IsMouseOver) Leave(); };
        MouseEnter += (_, _) => _dismiss.Stop();
        MouseLeave += (_, _) => _dismiss.Start();
        Loaded += (_, _) => { PlaceAboveTray(); Enter(); };
    }

    /// <summary>The card's content without animation, for documentation screenshots.</summary>
    internal FrameworkElement CardRoot => Root;

    internal void ShowFinalState()
    {
        Root.Opacity = 1;
        Slide.Y = 0;
        ArtScale.ScaleX = ArtScale.ScaleY = 1;
        foreach (var (ring, level) in _rings) ring.Value = level;
        Art.ShowAnimationEnd();
    }

    private FrameworkElement BatteryItem(string label, BatteryInfo b)
    {
        var fill = b.Level <= 20 ? Brush("SystemFillColorCriticalBrush", Colors.OrangeRed)
            : b.IsCharging ? Brush("SystemFillColorSuccessBrush", Colors.LimeGreen) : Brush("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        var ring = new BatteryRing { Width = 44, Height = 44, Thickness = 4, Fill = fill, Track = Brush("ControlStrokeColorDefaultBrush", Colors.Gray) };
        _rings.Add((ring, b.Level));
        var percent = new TextBlock
        {
            Text = $"{b.Level}%", FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("TextFillColorPrimaryBrush", Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var caption = new TextBlock
        {
            Text = b.IsCharging ? $"{label} ⚡" : label, FontSize = 11, Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brush("TextFillColorSecondaryBrush", Colors.Gray),
        };
        var stack = new StackPanel { Margin = new Thickness(14, 0, 14, 0) };
        stack.Children.Add(new Grid { Children = { ring, percent } });
        stack.Children.Add(caption);
        return stack;
    }

    private Brush Brush(string key, Color fallback) => TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private void PlaceAboveTray()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth;
        Top = area.Bottom - ActualHeight;
    }

    private void Enter()
    {
        Art.PlayAnimation();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, In));
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(40, 0, In) { EasingFunction = ease });
        var pop = new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(600)) { EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut } };
        ArtScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        ArtScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        // A slow hover, like the device floating above the card.
        ArtFloat.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -4, TimeSpan.FromSeconds(1.6))
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            BeginTime = TimeSpan.FromMilliseconds(600),
        });
        int i = 0;
        foreach (var (ring, level) in _rings)
            ring.BeginAnimation(BatteryRing.ValueProperty, new DoubleAnimation(0, level, Rings)
            {
                EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(250 + 120 * i++),
            });
        _dismiss.Start();
    }

    private void Leave()
    {
        _dismiss.Stop();
        var fade = new DoubleAnimation(Root.Opacity, 0, TimeSpan.FromMilliseconds(260));
        fade.Completed += (_, _) => Close();
        Root.BeginAnimation(OpacityProperty, fade);
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 24, TimeSpan.FromMilliseconds(260)));
    }

    private void OnClick(object sender, MouseButtonEventArgs e) { _open?.Invoke(); Leave(); }
    private void OnClose(object sender, RoutedEventArgs e) => Leave();
}
