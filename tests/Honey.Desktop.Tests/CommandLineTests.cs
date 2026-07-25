using Honey.Desktop.Startup;

namespace Honey.Desktop.Tests;

public sealed class CommandLineTests
{
    [Theory]
    [InlineData("--background", StartupCommand.Background)]
    [InlineData("--show", StartupCommand.Show)]
    [InlineData("--shutdown", StartupCommand.Shutdown)]
    public void Parse_识别生命周期命令(string argument, StartupCommand expected)
    {
        Assert.Equal(expected, StartupArguments.Parse([argument]).Command);
    }

    [Fact]
    public void Parse_未指定命令时默认显示()
    {
        Assert.Equal(StartupCommand.Show, StartupArguments.Parse([]).Command);
    }

    [Fact]
    public void Parse_读取隔离数据目录()
    {
        var parsed = StartupArguments.Parse(["--background", "--data-root", @"C:\临时\小玉"]);

        Assert.Equal(StartupCommand.Background, parsed.Command);
        Assert.Equal(@"C:\临时\小玉", parsed.DataRoot);
    }

    [Fact]
    public void Parse_拒绝互斥生命周期命令()
    {
        Assert.Throws<ArgumentException>(
            () => StartupArguments.Parse(["--show", "--shutdown"]));
    }

    [Fact]
    public void Parse_拒绝缺少值的数据目录()
    {
        Assert.Throws<ArgumentException>(() => StartupArguments.Parse(["--data-root"]));
    }
}
