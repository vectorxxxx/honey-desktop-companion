using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Honey.Integrations.Ai;

namespace Honey.Desktop.Settings;

public partial class SettingsWindow : Window
{
    private readonly Func<AiSettingsSubmission, CancellationToken, Task> _save;
    private readonly Func<AppSettings, string?, CancellationToken, Task<string>> _testAi;
    private readonly AiSettingsTestController _testController = new();
    private readonly AiSettingsBindingDraft _bindingDraft;
    private bool _hasStoredKey;
    private readonly bool _configurationMatched;
    private bool _clearKey;

    public SettingsWindow(
        AppSettings settings,
        bool hasStoredKey,
        bool configurationMatched,
        Func<AiSettingsSubmission, CancellationToken, Task> save,
        Func<AppSettings, string?, CancellationToken, Task<string>> testAi)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _testAi = testAi ?? throw new ArgumentNullException(nameof(testAi));
        _hasStoredKey = hasStoredKey;
        _configurationMatched = configurationMatched;
        _bindingDraft = new AiSettingsBindingDraft(settings.AiSecretBindingId);
        InitializeComponent();
        Apply(settings.Normalize());
        UpdateKeyStatus();
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += (_, _) => _testController.Cancel();
    }

    private AppSettings Read() => new()
    {
        PetSize = (int)Math.Round(PetSizeSlider.Value),
        ActivityLevel = SelectedTag(ActivityCombo, "balanced"),
        ModePreference = SelectedTag(ModeCombo, "auto"),
        StartWithWindows = AutoStartCheck.IsChecked == true,
        FocusMode = FocusCheck.IsChecked == true,
        AiEnabled = AiCheck.IsChecked == true,
        AiEndpoint = AiEndpointText.Text,
        AiModel = AiModelText.Text,
        AiSecretBindingId = _bindingDraft.BindingId,
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
        AiEndpointText.Text = settings.AiEndpoint;
        AiModelText.Text = settings.AiModel;
        _bindingDraft.Set(settings.AiSecretBindingId);
        SoundCheck.IsChecked = settings.SoundEnabled;
        VolumeSlider.Value = settings.SoundVolume;
        PetSizeValue.Text = $"{settings.PetSize} px";
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        try
        {
            var requested = Read();
            try
            {
                _ = AiEndpointValidator.StrictValidate(
                    requested.AiEndpoint,
                    requested.AiModel);
            }
            catch (AiConfigurationException exception)
            {
                ErrorText.Text = $"AI 配置无效：{exception.Message}";
                return;
            }

            var key = string.IsNullOrWhiteSpace(AiKeyPassword.Password)
                ? null
                : AiKeyPassword.Password;
            if (requested.AiEnabled && !_hasStoredKey && key is null)
            {
                ErrorText.Text = "启用 AI 前请填写 API 密钥。";
                return;
            }

            await _save(
                new AiSettingsSubmission(requested, key, _clearKey),
                CancellationToken.None);
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

    private void OnClearAiKeyClick(object sender, RoutedEventArgs e)
    {
        AiKeyPassword.Clear();
        _clearKey = true;
        _hasStoredKey = false;
        _bindingDraft.Clear();
        UpdateKeyStatus();
    }

    private async void OnTestAiClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "正在测试连接…";
        try
        {
            var key = string.IsNullOrWhiteSpace(AiKeyPassword.Password)
                ? null
                : AiKeyPassword.Password;
            if (_clearKey && key is null)
            {
                ErrorText.Text = "测试失败：密钥已标记清除，请填写新密钥后再测试。";
                return;
            }

            TestAiButton.IsEnabled = false;
            ErrorText.Text = await _testController.RunAsync(
                token => _testAi(Read(), key, token),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            ErrorText.Text = $"测试失败：{exception.Message}";
        }
        finally
        {
            TestAiButton.IsEnabled = !_testController.IsRunning;
        }
    }

    private void UpdateKeyStatus() =>
        AiKeyStatus.Text = _hasStoredKey && !_clearKey
            ? _configurationMatched
                ? "已由 Windows 当前用户安全保存，不会回显"
                : "密钥与配置不匹配，请重新保存"
            : "尚未保存密钥";

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

public sealed record AiSettingsSubmission(
    AppSettings Settings,
    string? NewApiKey,
    bool ClearApiKey);
