using Microsoft.UI.Xaml;
using EffortLess.Core.Library;

namespace EffortLess.App;

public partial class App : Application
{
    internal EffortLess.Core.Diagnostics.DiagnosticJournal Diagnostics { get; }
    internal string DataDirectory { get; }
    internal string CaptureDirectory { get; }
    internal string? MigrationWarning { get; private set; }
    private MainWindow? _window;
    public App()
    {
        // Complete copy-only migration before any preference or log writer starts.
        DataDirectory = PrepareDirectory(Environment.SpecialFolder.LocalApplicationData);
        CaptureDirectory = PrepareDirectory(Environment.SpecialFolder.MyPictures);
        Diagnostics = new(System.IO.Path.Combine(DataDirectory, "logs"));
        if (MigrationWarning is not null) Diagnostics.Write(MigrationWarning);
        UnhandledException += (_, e) => Diagnostics.Write(e.Exception.ToString());
        InitializeComponent();
    }

    private string PrepareDirectory(Environment.SpecialFolder folder)
    {
        string root = Environment.GetFolderPath(folder);
        string source = System.IO.Path.Combine(root, "LightPlayer"); // Legacy data compatibility.
        string destination = System.IO.Path.Combine(root, "EffortLess");
        try
        {
            DirectoryDataMigration.Migrate(source, destination);
            return destination;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            // Do not load defaults over a partially migrated profile. Keep using
            // the intact old profile and retry migration on the next launch.
            MigrationWarning = "EffortLess could not finish copying the previous profile. Existing data remains in use; migration will retry on the next launch. " + ex.Message;
            return System.IO.Directory.Exists(source) ? source : destination;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
