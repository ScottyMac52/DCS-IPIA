using System.IO;
using System.Windows;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DcsIpia.Presentation;

/// <summary>Starts the WPF application with dependency-injected services.</summary>
public partial class App : System.Windows.Application {
 private ServiceProvider? _services;
 #region BEGIN Public
 /// <summary>Creates import and settings services before showing the main window.</summary>
 /// <param name="e">The WPF startup event.</param>
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  var userFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DCS-IPIA");
  _services=new ServiceCollection()
   .AddSingleton<IAppSettingsService>(_=>new FileAppSettingsService(
    Path.Combine(AppContext.BaseDirectory,"appsettings.json"),
    Path.Combine(userFolder,"user-settings.json"),
    Path.Combine(userFolder,"theme.txt")))
   .AddSingleton<IHueyWorkspaceLoader,HueyWorkspaceLoader>()
   .AddSingleton<IDcsFolderImporter,DcsFolderImporter>()
   .AddTransient<MainWindow>()
   .BuildServiceProvider();
  MainWindow=_services.GetRequiredService<MainWindow>();
  MainWindow.Show();
 }
 /// <summary>Releases the service provider on application exit.</summary>
 /// <param name="e">The WPF exit event.</param>
 protected override void OnExit(ExitEventArgs e) {
  _services?.Dispose();
  base.OnExit(e);
 }
 #endregion END Public
}
