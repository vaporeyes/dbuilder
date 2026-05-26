// ABOUTME: Persisted external command settings used by UDB map options.
// ABOUTME: Stores command text, working directory and error handling flags without executing commands.

namespace DBuilder.IO;

public sealed class ExternalCommandSettings
{
    public string WorkingDirectory { get; set; } = "";
    public string Commands { get; set; } = "";
    public bool AutoCloseOnSuccess { get; set; } = true;
    public bool ExitCodeIsError { get; set; } = true;
    public bool StdErrIsError { get; set; } = true;

    public ExternalCommandSettings() { }

    public ExternalCommandSettings(Configuration configuration, string section)
    {
        LoadSettings(configuration, section);
    }

    public void LoadSettings(Configuration configuration, string section)
    {
        WorkingDirectory = configuration.ReadSetting(section + ".workingdirectory", "") ?? "";
        Commands = configuration.ReadSetting(section + ".commands", "") ?? "";
        AutoCloseOnSuccess = configuration.ReadSetting(section + ".autocloseonsuccess", true);
        ExitCodeIsError = configuration.ReadSetting(section + ".exitcodeiserror", true);
        StdErrIsError = configuration.ReadSetting(section + ".stderriserror", true);
    }

    public void WriteSettings(Configuration configuration, string section)
    {
        if (!string.IsNullOrWhiteSpace(Commands)) configuration.WriteSetting(section + ".commands", Commands);
        else configuration.DeleteSetting(section + ".commands");

        if (!string.IsNullOrWhiteSpace(WorkingDirectory)) configuration.WriteSetting(section + ".workingdirectory", WorkingDirectory);
        else configuration.DeleteSetting(section + ".workingdirectory");

        configuration.WriteSetting(section + ".autocloseonsuccess", AutoCloseOnSuccess);
        configuration.WriteSetting(section + ".exitcodeiserror", ExitCodeIsError);
        configuration.WriteSetting(section + ".stderriserror", StdErrIsError);
    }
}
