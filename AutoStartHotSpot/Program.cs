using System.Diagnostics;
using System.Security.Principal;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;
using Windows.Foundation;

class Program
{
    static string taskName = "开机自动开启热点 By 那年雪落";
    static void Main(string[] args)
    {
        // 检查是否以管理员身份运行
        if (!IsRunningAsAdministrator())
        {
            // 以管理员身份重新启动程序
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = AppDomain.CurrentDomain.FriendlyName,
                UseShellExecute = true,
                Verb = "runas" // 以管理员身份运行
            };
            Process.Start(startInfo);
            return;
        }

        // 检查是否已经设置了计划任务
        if (!IsTaskAlreadyExists(Program.taskName))
        {
            // 创建计划任务以管理员身份开机自启
            CreateStartupTask();
        }
        else
        {
            Console.WriteLine("计划任务已存在。");
        }

        // 尝试启动移动热点
        StartMobileHotspot();
    }

    static bool IsRunningAsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    static bool IsTaskAlreadyExists(string taskName)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "schtasks",
            Arguments = $"/query /tn \"{taskName}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(psi))
        {
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            return !string.IsNullOrWhiteSpace(output);
        }
    }

    static void CreateStartupTask()
    {
        string appPath = Process.GetCurrentProcess().MainModule.FileName;

        // 设置计划任务命令，包含延迟（15秒）
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "schtasks",
            Arguments = $"/create /tn \"{Program.taskName}\" /tr \"{appPath}\" /sc onlogon /rl highest /delay 0000:05",
            UseShellExecute = true,
            Verb = "runas" // 以管理员身份运行
        };

        Process.Start(psi);
        Console.WriteLine("已创建计划任务以管理员身份开机自启，延迟 5 秒。");
    }

    static void StartMobileHotspot()
    {
        var connectionProfile = NetworkInformation.GetInternetConnectionProfile();
        if (connectionProfile == null)
        {
            Console.WriteLine("未找到网络连接配置。");
            return;
        }

        var tetheringManager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(connectionProfile);
        var operationalState = tetheringManager.TetheringOperationalState;

        Console.WriteLine("当前热点状态: " + operationalState);

        if (operationalState != TetheringOperationalState.On)
        {
            Console.WriteLine("正在启动移动热点...");
            var result = AwaitTetheringOperation(tetheringManager.StartTetheringAsync());
            Console.WriteLine("热点已启动，状态: " + result.Status);
        }
        else
        {
            Console.WriteLine("移动热点已开启。");
        }
    }

    static T AwaitTetheringOperation<T>(IAsyncOperation<T> operation)
    {
        var task = operation.AsTask();
        task.Wait();
        return task.Result;
    }
}
