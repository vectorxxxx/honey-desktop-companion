using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace Honey.Desktop.Tests;

[CollectionDefinition("单实例进程测试", DisableParallelization = true)]
public sealed class 单实例进程测试集合;

[Collection("单实例进程测试")]
public sealed class DpiAwarenessTests
{
    private static readonly IntPtr PerMonitorAware = new(-3);
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    [Fact]
    public void 项目引用的应用清单声明逐显示器V2和非管理员权限()
    {
        var repositoryRoot = FindRepositoryRoot();
        var project = XDocument.Load(
            Path.Combine(repositoryRoot, "src", "Honey.Desktop", "Honey.Desktop.csproj"));
        var manifestPath = project
            .Descendants("ApplicationManifest")
            .Select(element => element.Value)
            .Single();
        var manifest = XDocument.Load(
            Path.Combine(repositoryRoot, "src", "Honey.Desktop", manifestPath));
        XNamespace assemblyV1 = "urn:schemas-microsoft-com:asm.v1";
        XNamespace assemblyV3 = "urn:schemas-microsoft-com:asm.v3";
        XNamespace settings = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
        XNamespace legacySettings = "http://schemas.microsoft.com/SMI/2005/WindowsSettings";

        Assert.Equal(
            "asInvoker",
            manifest.Descendants(assemblyV3 + "requestedExecutionLevel")
                .Single()
                .Attribute("level")?
                .Value);
        Assert.Equal(
            "PerMonitorV2, PerMonitor",
            manifest.Descendants(settings + "dpiAwareness").Single().Value.Trim());
        Assert.Equal(
            "true/pm",
            manifest.Descendants(legacySettings + "dpiAware").Single().Value.Trim());
        Assert.NotNull(manifest.Root?.Attribute("manifestVersion"));
        Assert.Equal(assemblyV1.NamespaceName, manifest.Root?.Name.NamespaceName);
    }

    [Fact]
    public void Release产物实际嵌入逐显示器应用清单()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "Honey.Desktop.exe");

        var manifest = ReadEmbeddedManifest(executable);

        Assert.Contains("PerMonitorV2, PerMonitor", manifest, StringComparison.Ordinal);
        Assert.Contains("true/pm", manifest, StringComparison.Ordinal);
        Assert.Contains("asInvoker", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Overlay窗口实际运行于逐显示器Dpi上下文()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "Honey.Desktop.exe");
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("无法启动 Honey.Desktop。");

        try
        {
            var window = await WaitForVisibleWindowAsync(
                process,
                TimeSpan.FromSeconds(8),
                TestContext.Current.CancellationToken);
            var context = GetWindowDpiAwarenessContext(window);

            Assert.True(
                AreDpiAwarenessContextsEqual(context, PerMonitorAwareV2)
                || AreDpiAwarenessContextsEqual(context, PerMonitorAware),
                "Overlay HWND 必须实际运行于 PerMonitorV2 或 PerMonitor DPI 上下文。");
        }
        finally
        {
            process.Refresh();
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(5000), "记录的 Honey 进程未在限定时间内退出。");
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Honey.slnx")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("无法定位 Honey 仓库根目录。");
    }

    private static string ReadEmbeddedManifest(string executable)
    {
        const uint loadLibraryAsDataFile = 0x00000002;
        var module = LoadLibraryEx(executable, IntPtr.Zero, loadLibraryAsDataFile);
        if (module == IntPtr.Zero)
        {
            throw new InvalidOperationException("无法以数据文件加载 Honey.Desktop.exe。");
        }

        try
        {
            var resource = FindResource(module, new IntPtr(1), new IntPtr(24));
            Assert.NotEqual(IntPtr.Zero, resource);
            var size = SizeofResource(module, resource);
            var loaded = LoadResource(module, resource);
            var pointer = LockResource(loaded);
            Assert.NotEqual(0u, size);
            Assert.NotEqual(IntPtr.Zero, pointer);
            var bytes = new byte[size];
            Marshal.Copy(pointer, bytes, 0, checked((int)size));
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    private static async Task<IntPtr> WaitForVisibleWindowAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            process.Refresh();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Honey.Desktop 提前退出，退出码 {process.ExitCode}。");
            }

            var window = FindVisibleWindow(process.Id);
            if (window != IntPtr.Zero)
            {
                return window;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("限定时间内未找到 Honey Overlay HWND。");
    }

    private static IntPtr FindVisibleWindow(int processId)
    {
        var result = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var ownerProcessId);
            if (ownerProcessId == (uint)processId && IsWindowVisible(window))
            {
                result = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(
        string fileName,
        IntPtr file,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr FindResource(
        IntPtr module,
        IntPtr name,
        IntPtr type);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LockResource(IntPtr resource);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AreDpiAwarenessContextsEqual(
        IntPtr first,
        IntPtr second);
}
