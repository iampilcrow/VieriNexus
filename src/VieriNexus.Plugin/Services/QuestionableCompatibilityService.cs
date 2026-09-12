using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal enum QuestionableCompatibilityHealth
{
    Missing,
    Waiting,
    Checking,
    Active,
    Native,
    Conflict,
    Disabled,
    Failed,
}

internal sealed record QuestionableCompatibilityStatus(
    QuestionableCompatibilityHealth Health,
    string Message,
    string? BundleSha256 = null)
{
    internal bool IsReady => Health is QuestionableCompatibilityHealth.Active or QuestionableCompatibilityHealth.Native;
}

/// <summary>
/// Keeps five finite Vieri route corrections layered over stock Questionable's complete downloaded
/// route bundle. Work happens only while Questionable is disabled or confirms it is idle.
/// </summary>
internal sealed class QuestionableCompatibilityService
{
    private readonly DependencyService dependencies;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly Func<bool> enabled;
    private readonly QuestionableRouteCompatibilityPack pack = new();
    private readonly ICallGateSubscriber<bool> questionableIsRunning;
    private readonly string bundlePath;
    private readonly string backupRoot;
    private readonly string receiptPath;
    private readonly HashSet<string> reloadedHashes = new(StringComparer.OrdinalIgnoreCase);
    private Task<QuestionableCompatibilityInstallResult>? installTask;
    private QuestionableCompatibilityInstallResult? lastResult;
    private DateTimeOffset nextCheck;
    private DateTime observedWriteTimeUtc;
    private long observedLength = -1;
    private QuestionableCompatibilityStatus status = new(
        QuestionableCompatibilityHealth.Checking,
        "Checking the stock Questionable route bundle.");

    internal QuestionableCompatibilityService(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies,
        ICommandManager commandManager,
        IPluginLog log,
        Func<bool> enabled)
    {
        this.dependencies = dependencies;
        this.commandManager = commandManager;
        this.log = log;
        this.enabled = enabled;
        questionableIsRunning = pluginInterface.GetIpcSubscriber<bool>("Questionable.IsRunning");
        string pluginConfigDirectory = pluginInterface.GetPluginConfigDirectory();
        string configRoot = Directory.GetParent(pluginConfigDirectory)?.FullName ?? pluginConfigDirectory;
        bundlePath = Path.Combine(configRoot, "Questionable", "PathData", "bundle.zip");
        string dataRoot = Path.Combine(pluginConfigDirectory, "NexusData", "questionable-compatibility");
        backupRoot = Path.Combine(dataRoot, "backups");
        receiptPath = Path.Combine(dataRoot, "receipt.v1.json");
    }

    internal QuestionableCompatibilityStatus Status => status;

    internal bool CanRun(string questId)
    {
        if (!QuestionableRouteCompatibilityPack.IsManagedQuest(questId))
            return true;
        if (status.Health == QuestionableCompatibilityHealth.Native)
            return true;
        return status.Health == QuestionableCompatibilityHealth.Active &&
               lastResult?.BundleSha256 is { } hash &&
               reloadedHashes.Contains(hash);
    }

    internal void Update(DateTimeOffset now)
    {
        try
        {
            PluginPresence presence = dependencies.FindPlugin("Questionable");
            CompleteInstall(presence);

            if (!enabled())
            {
                status = new(QuestionableCompatibilityHealth.Disabled,
                    "Nexus compatibility management is disabled; stock Questionable routes are untouched.");
                return;
            }
            if (!presence.IsInstalled)
            {
                status = new(QuestionableCompatibilityHealth.Missing,
                    "Stock Questionable is not installed yet. Nexus will prepare the five corrections when its route bundle appears.");
                return;
            }
            if (!File.Exists(bundlePath))
            {
                status = new(QuestionableCompatibilityHealth.Waiting,
                    "Waiting for Questionable to download its complete route bundle.");
                return;
            }

            FileInfo bundle = new(bundlePath);
            bool bundleChanged = lastResult is not null &&
                                 (observedWriteTimeUtc != bundle.LastWriteTimeUtc || observedLength != bundle.Length);
            if (bundleChanged)
            {
                lastResult = null;
                reloadedHashes.Clear();
                nextCheck = default;
                status = new(QuestionableCompatibilityHealth.Checking,
                    "Questionable installed a new route bundle; Nexus is rechecking the five protected routes.");
            }

            if (lastResult is { IsSatisfied: true, BundleSha256: not null } satisfied &&
                satisfied.State is QuestionableCompatibilityInstallState.Installed or
                    QuestionableCompatibilityInstallState.ManagedActive)
            {
                if (!presence.IsLoaded)
                {
                    status = new(QuestionableCompatibilityHealth.Active,
                        "Five Nexus route corrections are installed and will load with Questionable.",
                        satisfied.BundleSha256);
                }
                else if (!reloadedHashes.Contains(satisfied.BundleSha256))
                {
                    if (QuestionableRunning(presence, out bool running) && !running)
                    {
                        if (TryReload())
                        {
                            reloadedHashes.Add(satisfied.BundleSha256);
                            status = new(QuestionableCompatibilityHealth.Active,
                                "Five Nexus route corrections are active through stock Questionable.",
                                satisfied.BundleSha256);
                        }
                        else
                        {
                            status = new(QuestionableCompatibilityHealth.Waiting,
                                "The corrected bundle is installed; waiting for Questionable to accept a safe idle reload.",
                                satisfied.BundleSha256);
                        }
                    }
                    else
                    {
                        status = new(QuestionableCompatibilityHealth.Waiting,
                            "The corrected bundle is installed; Nexus will reload it after Questionable becomes idle.",
                            satisfied.BundleSha256);
                    }
                }
                else
                {
                    status = new(QuestionableCompatibilityHealth.Active,
                        "Five Nexus route corrections are active through stock Questionable.",
                        satisfied.BundleSha256);
                }
            }
            else if (lastResult is { State: QuestionableCompatibilityInstallState.AlreadyCorrect })
            {
                status = new(QuestionableCompatibilityHealth.Native,
                    "All five corrections are already present in Questionable; Nexus is not overriding them.",
                    lastResult.BundleSha256);
            }

            if (installTask is not null || now < nextCheck)
                return;
            nextCheck = now.AddSeconds(10);

            if (lastResult is not null && observedWriteTimeUtc == bundle.LastWriteTimeUtc && observedLength == bundle.Length)
                return;

            if (presence.IsLoaded && (!QuestionableRunning(presence, out bool providerRunning) || providerRunning))
            {
                status = new(QuestionableCompatibilityHealth.Waiting,
                    "Waiting for stock Questionable to become idle before checking its route bundle.");
                return;
            }

            status = new(QuestionableCompatibilityHealth.Checking,
                "Checking five exact routes against Questionable's current bundle.");
            installTask = Task.Run(() => pack.Install(bundlePath, backupRoot, receiptPath));
        }
        catch (Exception exception)
        {
            log.Error(exception, "Questionable compatibility update failed safely.");
            status = new(QuestionableCompatibilityHealth.Failed,
                "The Questionable compatibility check failed safely; no affected quest will start.");
        }
    }

    internal void RequestRecheck()
    {
        lastResult = null;
        reloadedHashes.Clear();
        observedLength = -1;
        observedWriteTimeUtc = default;
        nextCheck = default;
    }

    internal QuestionableCompatibilityInstallResult RestoreOfficialBundle()
    {
        PluginPresence presence = dependencies.FindPlugin("Questionable");
        if (presence.IsLoaded && (!QuestionableRunning(presence, out bool running) || running))
            return new(QuestionableCompatibilityInstallState.Failed,
                "Stop or disable Questionable before restoring its official route bundle.");
        QuestionableCompatibilityInstallResult result = pack.RestoreOfficialBundle(bundlePath, receiptPath);
        if (result.IsSatisfied && presence.IsLoaded && !TryReload())
            result = result with
            {
                Message = result.Message + " Restart Questionable once so it loads the restored official routes."
            };
        lastResult = null;
        reloadedHashes.Clear();
        RequestRecheck();
        return result;
    }

    private void CompleteInstall(PluginPresence presence)
    {
        if (installTask is not { IsCompleted: true } completed)
            return;
        installTask = null;
        try
        {
            lastResult = completed.GetAwaiter().GetResult();
            if (File.Exists(bundlePath))
            {
                FileInfo bundle = new(bundlePath);
                observedWriteTimeUtc = bundle.LastWriteTimeUtc;
                observedLength = bundle.Length;
            }
            status = lastResult.State switch
            {
                QuestionableCompatibilityInstallState.Installed or QuestionableCompatibilityInstallState.ManagedActive =>
                    new(presence.IsLoaded
                            ? QuestionableCompatibilityHealth.Waiting
                            : QuestionableCompatibilityHealth.Active,
                        presence.IsLoaded
                            ? "Five Nexus route corrections are installed; an idle Questionable reload is pending."
                            : "Five Nexus route corrections are installed and will load with Questionable.",
                        lastResult.BundleSha256),
                QuestionableCompatibilityInstallState.AlreadyCorrect =>
                    new(QuestionableCompatibilityHealth.Native,
                        "All five corrections are already present in Questionable; Nexus is not overriding them.",
                        lastResult.BundleSha256),
                QuestionableCompatibilityInstallState.Conflict =>
                    new(QuestionableCompatibilityHealth.Conflict, lastResult.Message, lastResult.BundleSha256),
                _ => new(QuestionableCompatibilityHealth.Failed, lastResult.Message, lastResult.BundleSha256),
            };
        }
        catch (Exception exception)
        {
            log.Error(exception, "Questionable compatibility pack background check failed.");
            status = new(QuestionableCompatibilityHealth.Failed,
                "The Questionable compatibility check failed safely; no quest route was changed.");
        }
    }

    private bool QuestionableRunning(PluginPresence presence, out bool running)
    {
        running = false;
        if (!presence.IsLoaded)
            return true;
        if (!questionableIsRunning.HasFunction)
            return false;
        try
        {
            running = questionableIsRunning.InvokeFunc();
            return true;
        }
        catch (Exception exception)
        {
            log.Verbose(exception, "Could not read stock Questionable activity while preparing route corrections.");
            return false;
        }
    }

    private bool TryReload()
    {
        try
        {
            return commandManager.ProcessCommand("/qst reload");
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Stock Questionable rejected its safe idle route reload.");
            return false;
        }
    }
}
