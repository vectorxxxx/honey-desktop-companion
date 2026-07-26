using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Honey.Domain.Activity;
using Honey.Domain.Model;
using MediaColor = System.Windows.Media.Color;

namespace Honey.Desktop.Status;

public partial class PetCodexWindow : Window
{
    public PetCodexWindow(PetStatusSnapshot initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        InitializeComponent();
        Update(initial);
    }

    public void Update(PetStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        NameText.Text = snapshot.DisplayName;
        MoodText.Text = PetStatusText.Mood(snapshot.Mood);
        ModeText.Text = snapshot.Mode == PetMode.Berserk
            ? "狂暴 · 血玉"
            : "常态 · 白玉";
        ApplyGauge(HungerGauge, HungerValue, FindGauge(snapshot, "hunger"));
        ApplyGauge(EnergyGauge, EnergyValue, FindGauge(snapshot, "energy"));
        ApplyGauge(CuriosityGauge, CuriosityValue, FindGauge(snapshot, "curiosity"));
        ApplyGauge(AffectionGauge, AffectionValue, FindGauge(snapshot, "affection"));
        ApplyGauge(StressGauge, StressValue, FindGauge(snapshot, "stress"));
        BehaviorText.Text = PetStatusText.Behavior(snapshot.Behavior);
        PhaseText.Text = PetStatusText.Phase(snapshot.Phase);
        DurationText.Text = PetStatusText.Duration(snapshot.BehaviorDuration);
        OriginText.Text = PetStatusText.Origin(snapshot.Origin);
        OriginIcon.Data = (Geometry)FindResource(
            PetStatusText.OriginIconKey(snapshot.Origin));
        ApplyOriginColor(snapshot.Origin);
        ActivityList.ItemsSource = snapshot.RecentActivities
            .Select(PetStatusText.Activity)
            .ToArray();
    }

    private static PetNeedGauge FindGauge(PetStatusSnapshot snapshot, string key) =>
        snapshot.Needs.Single(gauge => string.Equals(
            gauge.Key,
            key,
            StringComparison.Ordinal));

    private static void ApplyGauge(
        System.Windows.Controls.ProgressBar bar,
        TextBlock valueText,
        PetNeedGauge gauge)
    {
        bar.Value = gauge.Value;
        bar.ToolTip = $"{gauge.Name} {gauge.Value} / 100 · {gauge.Description}";
        valueText.Text = $"{gauge.Value:00}";
    }

    private void ApplyOriginColor(BehaviorOrigin origin)
    {
        var color = origin switch
        {
            BehaviorOrigin.AiSuggestion => MediaColor.FromRgb(170, 163, 255),
            BehaviorOrigin.UserInteraction => MediaColor.FromRgb(224, 150, 190),
            BehaviorOrigin.SystemSchedule => MediaColor.FromRgb(130, 172, 206),
            _ => MediaColor.FromRgb(159, 233, 219)
        };
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        OriginIcon.Stroke = brush;
        OriginText.Foreground = brush;
        OriginBadge.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(
            150,
            color.R,
            color.G,
            color.B));
    }

    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Opacity = Topmost ? 1 : 0.68;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
