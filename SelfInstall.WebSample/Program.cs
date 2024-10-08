using SelfInstall;
using static SelfInstall.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.ConfigureSelfInstall(new SelfInstallOptions
{
    User = SelfInstallOptions.UserEnum.CurrentUser,
    ShouldPromptForUserPassword = true,
    ShouldFallbackToLocalSystemWithoutPromptedPassword = true,
    PreInstallHook = _ => Console.WriteLine("Installing"),
    ServiceCopyTargetPath = @"c:\temp\webservice.exe"
});

var app = builder.Build();

var didInstallationProcess = await app.RunSelfInstall(args);
if (didInstallationProcess)
{
    return;
}

app.MapControllers();
app.Run("http://localhost:1991");
