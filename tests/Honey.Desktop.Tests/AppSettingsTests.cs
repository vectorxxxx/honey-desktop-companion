using Honey.Desktop.Settings;

namespace Honey.Desktop.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Normalize_限制尺寸并保留默认自启动关闭()
    {
        var normalized = new AppSettings { PetSize = 400 }.Normalize();

        Assert.Equal(240, normalized.PetSize);
        Assert.False(normalized.StartWithWindows);
    }

    [Fact]
    public void Normalize_修复非法选项与非有限音量且幂等()
    {
        var normalized = new AppSettings
        {
            PetSize = 10,
            ActivityLevel = "fast",
            ModePreference = "red",
            SoundVolume = double.NaN
        }.Normalize();

        Assert.Equal(60, normalized.PetSize);
        Assert.Equal("balanced", normalized.ActivityLevel);
        Assert.Equal("auto", normalized.ModePreference);
        Assert.Equal(0.35, normalized.SoundVolume);
        Assert.Equal(normalized, normalized.Normalize());
    }
}
