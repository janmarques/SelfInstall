using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace SelfInstall;

public static class Extensions
{
    private static SelfInstallOptions _options;

    public static void ConfigureSelfInstall(this IHostApplicationBuilder builder, SelfInstallOptions options = null)
    {
        builder.Services.AddWindowsService();
        _options = options ?? new SelfInstallOptions();
    }

    /// <summary>
    /// Runs the self installer if needed.
    /// Returns true is the installation is completed, and the program should be exited
    /// Returns false if there is no installation, and the program should be be continued
    /// </summary>
    /// <param name="host"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async Task<bool> RunSelfInstall(this IHost host, string[] args = null)
    {
        var firstArg = args?.FirstOrDefault();
        var name = _options.ServiceName ?? Process.GetCurrentProcess().ProcessName;
        async Task Stop() => await PowerShell($"net stop {name}");
        async Task Delete() => await PowerShell($"sc.exe delete {name}");

        if (firstArg == _options.DeleteArg)
        {
            await Stop();
            await Delete();
            return true;
        }

        if (_options.ShouldInstall && firstArg != _options.RunLocalArg)
        {
            var runningExe = Environment.ProcessPath;
            var targetExe = _options.ServiceCopyTargetPath ?? runningExe;
            _options.PreInstallHook?.Invoke((runningExe, targetExe));

            await Stop();
            await Delete();

            if (_options.ServiceCopyTargetPath != null)
            {
                CopyFile(_options.ServiceCopyTargetPath, runningExe);
            }

            await PowerShell($"sc.exe create {name} binpath=\"{targetExe}\" start=auto");
            await PowerShell($"sc.exe failure {name} reset=10 actions=restart/60000/restart/60000/restart/60000");

            if (_options.User != SelfInstallOptions.UserEnum.LocalSystem)
            {
                var username = _options.User == SelfInstallOptions.UserEnum.CurrentUser ? WindowsIdentity.GetCurrent().Name : _options.HostSpecifiedUser;
                var password = _options.Password;
                if (_options.ShouldPromptForUserPassword)
                {
                    Console.WriteLine($"Type the Windows password for user {username} and press enter");
                    password = Console.ReadLine();
                }
                if (!(string.IsNullOrEmpty(password) && _options.ShouldFallbackToLocalSystemWithoutPromptedPassword))
                {
                    await PowerShell(@$"sc.exe config {name} obj= ""{username}"" password= ""{password}""");
                }
            }

            await PowerShell($"net start {name}");

            return true;
        }

        if (firstArg == _options.RunLocalArg)
        {
            await Stop();
        }

        return false;
    }

    private static void CopyFile(string serviceCopyTargetPath, string runningExe)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(serviceCopyTargetPath));
        File.Delete(serviceCopyTargetPath);
        File.Copy(runningExe, serviceCopyTargetPath);
    }


    private static async Task PowerShell(string command)
    {
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                CreateNoWindow = false,
                Arguments = command,
                FileName = "powershell.exe"
            }
        };
        process.Start();
        process.ErrorDataReceived += (_, e) => Console.WriteLine(e.Data);
        process.OutputDataReceived += (_, e) => Console.WriteLine(e.Data);
        var printTask = process.WaitForExitAsync();
        await Task.Delay(_options.InstallActionsTimeout);
    }
}

public class SelfInstallOptions
{
    /// <summary>
    /// If true, the service will be installed.
    /// Don't just set this always to true, or otherwise the exe will try to install itself while already installed as a service.
    /// Defaults to Environment.UserInteractive
    /// </summary>
    public bool ShouldInstall { get; set; } = Environment.UserInteractive;
    /// <summary>
    /// Which runs the service?
    /// LocalSystem: default, requires no password
    /// CurrentUser: the result of System.Security.Principal.WindowsIdentity.GetCurrent().Name, requires a password
    /// HostSpecified: set the user via <see cref="HostSpecifiedUser">HostSpecifiedUser</see>, requires a password
    /// </summary>
    public UserEnum User { get; set; } = UserEnum.LocalSystem;
    public enum UserEnum { LocalSystem, CurrentUser, HostSpecified }
    /// <summary>
    /// If true, a Console.ReadLine() will be prompted for the password.
    /// Alternatively, set the password via <see cref="Password">Password</see>.
    /// When runnning as LocalSystem, no password is needed.
    /// </summary>
    public bool ShouldPromptForUserPassword { get; set; } = false;
    /// <summary>
    /// When <see cref="ShouldPromptForUserPassword">ShouldPromptForUserPassword</see> is set to true, and the user does not fill the prompted password, fallback to the LocalSystem user (without password).
    /// Defaults to false
    /// </summary>
    public bool ShouldFallbackToLocalSystemWithoutPromptedPassword { get; set; } = false;
    /// <summary>
    /// User that runs the service. Should be a full domain user name (eg company\admin123). Requires a password via <see cref="Password">Password</see>
    /// </summary>
    public string HostSpecifiedUser { get; set; } = null;
    /// <summary>
    /// Password for the user that runs the service
    /// </summary>
    public string Password { get; set; } = null;
    /// <summary>
    /// Name of the service, will default to the project name, Process.GetCurrentProcess().ProcessName
    /// </summary>
    public string ServiceName { get; set; } = null;
    /// <summary>
    /// If provided, the service will be copied to the target path and run for there. 
    /// Directory will be created if it does not exist
    /// If left `null`, the service will be run directly from itself.
    /// </summary>
    public string ServiceCopyTargetPath { get; set; } = null;
    /// <summary>
    /// Delay between each of the installation steps. Defaults to 3 seconds.
    /// </summary>
    public TimeSpan InstallActionsTimeout { get; set; } = TimeSpan.FromSeconds(3);
    /// <summary>
    /// If provided, when this is the first arg, the service will be stopped and deleted as a service. The program will then exit. No files are touched. 
    /// Defaults to "delete"
    /// </summary>
    public string DeleteArg { get; set; } = "delete";
    /// <summary>
    /// Intended for running the application code locally without installing (eg for easier debugging or during development).
    /// If provided, when this is the first arg, the service will be stopped, the service will not be installed and the rest of the application code will run.
    /// Defaults to local
    /// </summary>
    public string RunLocalArg { get; set; } = "local";
    /// <summary>
    /// Callback to check the sources before installing.
    /// </summary>
    public Action<(string runningExe, string targetExe)> PreInstallHook { get; set; }
}