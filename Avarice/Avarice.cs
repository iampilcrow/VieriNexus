using Avarice.Data;
using Avarice.Positional;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Ipc;
using ECommons.EzSharedDataManager;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using ECommons.Schedulers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Pictomancy;
using PunishLib;

#pragma warning disable CS0649

namespace Avarice;

public unsafe class Avarice : IDalamudPlugin
{
    public string Name
    {
        get
        {
            return "VieriAvarice";
        }
    }

    internal Config config;
    internal Profile currentProfile;
    internal static Avarice P;
    internal WindowSystem windowSystem;
    internal ConfigWindow configWindow;
    private Canvas canvas;
    internal PositionalDebugWindow positionalDebugWindow;
    internal Memory memory;

    internal static uint[] PositionalJobs = new uint[] { 2, 4, 29, 30, 20, 34, 39, 22 };
    internal uint Job = 0;
    internal HashSet<uint> StaticAutoDetectRadiusData;
    internal PositionalManager PositionalManager;
    internal uint[] PositionalStatus;
    internal RotationSolverWatcher RotationSolverWatcher;
    internal RotationHelperWatcher RotationHelperWatcher;
    private readonly ICallGateSubscriber<bool> autoDutyIsStopped;
    private readonly ICallGateSubscriber<string, string, string, string, bool> bossModAddTransientStrategy;
    private readonly GameplayOverlayGate gameplayOverlayGate = new();
    private byte? lastAutoDutyPositional;

    public Avarice(IDalamudPluginInterface pi)
    {
        P = this;
        LegacyConfigurationMigration.CopyFromAvariceIfNeeded(pi);
        ECommonsMain.Init(pi, this, Module.DalamudReflector, Module.ObjectFunctions);
        autoDutyIsStopped = pi.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        bossModAddTransientStrategy =
            pi.GetIpcSubscriber<string, string, string, string, bool>("BossMod.Presets.AddTransientStrategy");
        PunishLibMain.Init(pi, Svc.PluginInterface.InternalName, PunishOption.DefaultKoFi);
        _ = new TickScheduler(delegate
        {
            PositionalStatus = EzSharedData.GetOrCreate<uint[]>("Avarice.PositionalStatus", [0, 0]);
            config = Svc.PluginInterface.GetPluginConfig() as Config ?? new();
            if (config.Profiles.Count == 0)
            {
                config.Profiles.Add(new() { Name = "Default", IsDefault = true });
            }
            foreach (var pr in config.Profiles)
                if (pr.IsDefault && pr.Name == "Default profile") pr.Name = "Default";
            currentProfile = config.Profiles.FirstOr0(x => x.IsDefault);
            //Svc.GameNetwork.NetworkMessage += OnNetworkMessage;
            RotationSolverWatcher = new();
            RotationHelperWatcher = new();
            memory = new();
            windowSystem = new();
            configWindow = new();
            windowSystem.AddWindow(configWindow);
            canvas = new();
            windowSystem.AddWindow(canvas);
            positionalDebugWindow = new();
            windowSystem.AddWindow(positionalDebugWindow);
            Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += delegate { configWindow.IsOpen = true; };
            Svc.Condition.ConditionChange += OnConditionChange;
            CommandInfo commandInfo = new((string cmd, string args) =>
            {
                if (args == "debug")
                {
                    P.currentProfile.Debug = !P.currentProfile.Debug;
                    positionalDebugWindow.IsOpen = P.currentProfile.Debug;
                    Svc.Chat.Print($"Debug mode {(P.currentProfile.Debug ? "enabled" : "disabled")}");
                }
                else if (args == "draw") // Added new command for toggling drawing
                {
                    P.currentProfile.DrawingEnabled = !P.currentProfile.DrawingEnabled;
                    Svc.Chat.Print($"Drawing {(P.currentProfile.DrawingEnabled ? "enabled" : "disabled")}");
                }
                else
                {
                    configWindow.IsOpen = !configWindow.IsOpen;
                }
            })
            { HelpMessage = "Toggle VieriAvarice settings. Use 'draw' to toggle drawing or 'debug' for debug mode." };
            _ = Svc.Commands.AddHandler("/vieriavarice", commandInfo);
            _ = Svc.Commands.AddHandler("/va", commandInfo);
            //LoadOpcode.Start();
            LuminaSheets.Init();
            Svc.PluginInterface.GetIpcProvider<IntPtr, CardinalDirection>("Avarice.CardinalDirection").RegisterFunc(GetCardinalDirectionForObject);
            Svc.Framework.Update += Tick;
            StaticAutoDetectRadiusData = Util.LoadStaticAutoDetectRadiusData();
            if (config.SplatoonUnsafePixel)
            {
                TabSplatoon.WriteRequest();
            }

            ActionWatching.Enable();
            ComboCache.ComboCacheInstance = new ComboCache();

            PositionalManager = new();
            PctService.Initialize(Svc.PluginInterface);
        });
    }

    private CardinalDirection GetCardinalDirectionForObject(IntPtr arg)
    {
        var obj = Svc.Objects.CreateObjectReference(arg);
        if (obj != null && Svc.Objects.LocalPlayer != null)
        {
            return MathHelper.GetCardinalDirection((MathHelper.GetRelativeAngle(Svc.Objects.LocalPlayer.Position, obj.Position) + obj.Rotation.RadToDeg()) % 360);
        }
        else
        {
            return (CardinalDirection)(-1);
        }
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {

        if (flag == ConditionFlag.InCombat)
        {
            Safe(delegate
            {
                if (value)
                {
                    PluginLog.Debug("Entered combat");
                }
                else
                {
                    PluginLog.Debug("Exited combat");
                    Svc.PluginInterface.SavePluginConfig(config);
                    if (currentProfile.Announce && !currentProfile.CurrentEncounterStats.Finished &&
              (currentProfile.CurrentEncounterStats.Hits > 0 || currentProfile.CurrentEncounterStats.Missed > 0))
                    {
                        var total = currentProfile.CurrentEncounterStats.Hits + currentProfile.CurrentEncounterStats.Missed;
                        var success = (int)(100f * currentProfile.CurrentEncounterStats.Hits / total);
                        Svc.Chat.Print(new SeStringBuilder()
                    .AddText($"Positionals summary for encounter: {currentProfile.CurrentEncounterStats.Hits}/{total} - ")
                    .AddUiForeground($"{success}%", Util.GetParsedSeStringColor(success))
                    .Build());
                    }
                    currentProfile.CurrentEncounterStats.Finished = true;
                }
            });
        }
    }

    internal static bool IsConditionMatching(DisplayCondition c)
    {
        if (c == DisplayCondition.Only_in_combat)
        {
            return Svc.Condition[ConditionFlag.InCombat];
        }
        else if (c == DisplayCondition.Only_in_duty)
        {
            return Svc.Condition[ConditionFlag.BoundByDuty56];
        }
        else if (c == DisplayCondition.In_duty_or_combat)
        {
            return Svc.Condition[ConditionFlag.InCombat] || Svc.Condition[ConditionFlag.BoundByDuty56];
        }
        else if (c == DisplayCondition.In_duty_and_combat)
        {
            return Svc.Condition[ConditionFlag.InCombat] && Svc.Condition[ConditionFlag.BoundByDuty56];
        }
        else
        {
            return true;
        }
    }

    internal void RecordStat(bool isMiss)
    {
        if (currentProfile.CurrentEncounterStats.Finished)
        {
            currentProfile.CurrentEncounterStats = new();
        }
        if (!currentProfile.Stats.ContainsKey((uint)Player.Job))
        {
            currentProfile.Stats[(uint)Player.Job] = new();
        }
        if (isMiss)
        {
            currentProfile.Stats[(uint)Player.Job].Missed++;
            currentProfile.CurrentEncounterStats.Missed++;
        }
        else
        {
            currentProfile.Stats[(uint)Player.Job].Hits++;
            currentProfile.CurrentEncounterStats.Hits++;
        }
    }

    internal Profile GetProfileForJob(uint job)
    {
        if (P.config.JobProfiles.TryGetValue(job, out var guid))
        {
            if (P.config.Profiles.TryGetFirst(x => x.GUID == guid, out var profile))
            {
                return profile;
            }
        }
        return null;
    }

    public void Dispose()
    {
        ResetAutoDutyPositionalBridge();
        Safe(() => Svc.PluginInterface.SavePluginConfig(config));
        //Svc.GameNetwork.NetworkMessage -= OnNetworkMessage;
        Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        _ = Svc.Commands.RemoveHandler("/vieriavarice");
        _ = Svc.Commands.RemoveHandler("/va");
        Svc.Condition.ConditionChange -= OnConditionChange;
        Svc.Framework.Update -= Tick;
        Safe(() =>
        {
            Svc.PluginInterface.GetIpcProvider<IntPtr, CardinalDirection>("Avarice.CardinalDirection").UnregisterFunc();
        });
        memory.Dispose();
        ActionWatching.Dispose();
        ComboCache.ComboCacheInstance.Dispose();
        VisualFeedbackManager.Dispose();
        PctService.Dispose();
        RotationSolverWatcher.Dispose();
        PunishLibMain.Dispose();
        ECommonsMain.Dispose();
        P = null;
    }

    private void Tick(object framework)
    {
        if (Framework.Instance()->FrameCounter - PositionalStatus[0] > 1)
        {
            PositionalStatus[1] = 0;
        }
        UpdateAutoDutyPositionalBridge();
        if (Svc.Objects.LocalPlayer != null)
        {
            var newJob = (uint)Player.Job;
            if (newJob != Job)
            {
                PluginLog.Debug($"Job changed from {Job} to {newJob}");
                var newJobProfile = GetProfileForJob(newJob);
                if (newJobProfile != null)
                {
                    currentProfile = newJobProfile;
                    PluginLog.Debug($"Switched profile to job profile {newJobProfile.Name}");
                }
                else
                {
                    if (GetProfileForJob(Job) != null)
                    {
                        currentProfile = P.config.Profiles.FirstOr0(x => x.IsDefault);
                        PluginLog.Debug($"Switched profile to default {currentProfile.Name}");
                    }
                }
            }
            Job = newJob;
        }
    }

    internal bool GameplayOverlaysReady => gameplayOverlayGate.Evaluate(
        Svc.ClientState.IsLoggedIn,
        Svc.Objects.LocalPlayer is { IsTargetable: true },
        Svc.ClientState.TerritoryType != 0,
        Svc.Condition[ConditionFlag.BetweenAreas] ||
        Svc.Condition[ConditionFlag.BetweenAreas51],
        Environment.TickCount64);

    private void UpdateAutoDutyPositionalBridge()
    {
        bool autoDutyRunning;
        try
        {
            autoDutyRunning = autoDutyIsStopped.HasFunction && !autoDutyIsStopped.InvokeFunc();
        }
        catch
        {
            autoDutyRunning = false;
        }

        if (!autoDutyRunning)
        {
            ResetAutoDutyPositionalBridge();
            return;
        }

        byte positional = PositionalStatus.Length >= 2
            ? (byte)Math.Clamp(PositionalStatus[1], 0, 2)
            : (byte)0;
        if (lastAutoDutyPositional == positional ||
            !EzThrottler.Throttle("VieriAvarice.AutoDutyPositionalBridge", 100))
        {
            return;
        }

        if (TrySetAutoDutyPositional(positional))
        {
            if (lastAutoDutyPositional is null)
                PluginLog.Information("Enabled VieriAvarice positional movement for AutoDuty");
            lastAutoDutyPositional = positional;
        }
    }

    private void ResetAutoDutyPositionalBridge()
    {
        if (lastAutoDutyPositional is not null)
            TrySetAutoDutyPositional(0);
        lastAutoDutyPositional = null;
    }

    private bool TrySetAutoDutyPositional(byte positional)
    {
        if (!bossModAddTransientStrategy.HasFunction)
            return false;

        string positionalName = positional switch
        {
            1 => "Rear",
            2 => "Flank",
            _ => "Any"
        };

        try
        {
            return bossModAddTransientStrategy.InvokeFunc(
                "AutoDuty Passive",
                "BossMod.Autorotation.MiscAI.GoToPositional",
                "Positional",
                positionalName);
        }
        catch (Exception exception)
        {
            if (EzThrottler.Throttle("VieriAvarice.AutoDutyPositionalBridge.Error", 5000))
                PluginLog.Error($"Could not update AutoDuty positional movement: {exception.Message}");
            return false;
        }
    }
}
