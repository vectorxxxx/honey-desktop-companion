using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Honey.Desktop.Settings;

public partial class SettingsWindow : Window
{
    private readonly Func<AppSettings, CancellationToken, Task> _save;

    public SettingsWindow(
        AppSettings settings,
        Func<AppSettings, CancellationToken, Task> save)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        InitializeComponent();
        Apply(settings.Normalize());
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private AppSettings Read() => new()
    {
        PetSize = (int)Math.Round(PetSizeSlider.Value),
        ActivityLevel = SelectedTag(ActivityCombo, "balanced"),
        ModePreference = SelectedTag(ModeCombo, "auto"),
        StartWithWindows = AutoStartCheck.IsChecked == true,
        FocusMode = FocusCheck.IsChecked == true,
        AiEnabled = AiCheck.IsChecked == true,
        SoundEnabled = SoundCheck.IsChecked == true,
        SoundVolume = VolumeSlider.Value
    };

    private void Apply(AppSettings settings)
    {
        PetSizeSlider.Value = settings.PetSize;
        SelectTag(ActivityCombo, settings.ActivityLevel);
        SelectTag(ModeCombo, settings.ModePreference);
        AutoStartCheck.IsChecked = settings.StartWithWindows;
        FocusCheck.IsChecked = settings.FocusMode;
        AiCheck.IsChecked = settings.AiEnabled;
        SoundCheck.IsChecked = settings.SoundEnabled;
        VolumeSlider.Value = settings.SoundVolume;
        PetSizeValue.Text = $"{settings.PetSize} px";
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        try
        {
            await _save(Read().Normalize(), CancellationToken.None);
            DialogResult = true;
            Close();
        }
        catch (Exception exception)
        {
            ErrorText.Text = $"保存失败：{exception.Message}";
        }
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e) =>
        Apply(new AppSettings());

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnPetSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PetSizeValue is not null)
        {
            PetSizeValue.Text = $"{Math.Round(e.NewValue):0} px";
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private static string SelectedTag(System.Windows.Controls.ComboBox comboBox, string fallback) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;

    private static void SelectTag(System.Windows.Controls.ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
            ?? comboBox.Items[0];
    }
}
