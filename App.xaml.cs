using System;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace BotWhatsappCSharp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static System.Threading.Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Nome único para o Mutex (mesmo que será usado no instalador)
        const string appGuid = "BotZapAI-72d9cfa3-71af-414f-b78e-efeec3e31a47";
        _mutex = new System.Threading.Mutex(true, appGuid, out bool createdNew);

        if (!createdNew)
        {
            // App já está rodando
            MessageBox.Show("O BotZap AI já está em execução.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
            return;
        }

        base.OnStartup(e);

        // Global Exception Handling
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            LogException((Exception)args.ExceptionObject, "AppDomain.UnhandledException");

        DispatcherUnhandledException += (s, args) =>
        {
            LogException(args.Exception, "DispatcherUnhandledException");
            args.Handled = true; // Prevent crash
            MessageBox.Show($"Ocorreu um erro inesperado: {args.Exception.Message}\nVeja os logs para mais detalhes.", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogException(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    private void LogException(Exception ex, string source)
    {
        BotWhatsappCSharp.Services.Logger.Error($"[{source}] Unhandled Crash", ex);
    }
}
