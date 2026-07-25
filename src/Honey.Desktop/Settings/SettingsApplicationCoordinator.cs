using Honey.Integrations.Windows;

namespace Honey.Desktop.Settings;

public sealed class SettingsApplicationCoordinator(
    ISettingsPersistence persistence,
    IAutoStartController autoStart)
{
    public async Task ApplyAsync(
        AppSettings current,
        AppSettings requested,
        string executablePath,
        CancellationToken cancellationToken)
    {
        var previous = current.Normalize();
        var next = requested.Normalize();
        var wasEnabled = autoStart.IsEnabled(executablePath);
        try
        {
            ApplyAutoStart(next.StartWithWindows, executablePath);
            await persistence.SaveAsync(next, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception original)
        {
            TryCompensate(() => ApplyAutoStart(wasEnabled, executablePath), original, "AutoStart");
            try
            {
                await persistence.SaveAsync(previous, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception compensation)
            {
                original.Data["Honey.Settings.PersistenceCompensation"] = compensation;
            }

            throw;
        }
    }

    private void ApplyAutoStart(bool enabled, string executablePath)
    {
        if (enabled)
        {
            autoStart.Enable(executablePath);
        }
        else
        {
            autoStart.Disable();
        }
    }

    private static void TryCompensate(
        Action compensation,
        Exception original,
        string key)
    {
        try
        {
            compensation();
        }
        catch (Exception exception)
        {
            original.Data[$"Honey.Settings.{key}Compensation"] = exception;
        }
    }
}
