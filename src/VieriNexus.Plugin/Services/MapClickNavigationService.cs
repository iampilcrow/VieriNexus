using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal enum MapClickNavigationState
{
    Idle,
    Armed,
    Navigating,
    Arrived,
    Blocked,
    Failed,
}

internal sealed record MapClickDestination(
    uint TerritoryId,
    uint MapId,
    float X,
    float Z,
    string TerritoryName);

internal sealed record MapClickNavigationStatus(
    MapClickNavigationState State,
    string Message,
    MapClickDestination? Destination)
{
    internal bool IsActive => State is MapClickNavigationState.Armed or MapClickNavigationState.Navigating;
}

/// <summary>
/// Nexus-owned one-shot map navigation. The destination and user-facing lifecycle belong to Nexus;
/// the complete trip is delegated to the same stock Lifestream and vnavmesh provider chain used by
/// authored routes.
/// </summary>
internal sealed class MapClickNavigationService
{
    private readonly Configuration configuration;
    private readonly NavigationRouteRuntimeService navigation;
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly IKeyState keyState;
    private readonly HashSet<VirtualKey> captureInitiallyDown = [];
    private MapClickDestination? flagWhenArmed;
    private Guid? activeRouteId;
    private bool hotkeyWasDown;
    private MapClickNavigationState state = MapClickNavigationState.Idle;
    private string message = "Ready for a map destination.";
    private MapClickDestination? destination;

    internal MapClickNavigationService(
        Configuration configuration,
        NavigationRouteRuntimeService navigation,
        IClientState clientState,
        IDataManager dataManager,
        IKeyState keyState)
    {
        this.configuration = configuration;
        this.navigation = navigation;
        this.clientState = clientState;
        this.dataManager = dataManager;
        this.keyState = keyState;
    }

    internal MapClickNavigationStatus Status => new(state, message, destination);

    internal bool IsCapturingHotkey { get; private set; }

    internal string HotkeyName
    {
        get
        {
            VirtualKey key = (VirtualKey)configuration.MapClickNavigationHotkey;
            if (configuration.MapClickNavigationHotkey == 0 || !Enum.IsDefined(key))
                return "Not assigned";

            List<string> parts = [];
            if (configuration.MapClickNavigationHotkeyControl) parts.Add("Ctrl");
            if (configuration.MapClickNavigationHotkeyShift) parts.Add("Shift");
            if (configuration.MapClickNavigationHotkeyAlt) parts.Add("Alt");
            parts.Add(key.GetFancyName());
            return string.Join(" + ", parts);
        }
    }

    internal void Update()
    {
        if (!clientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer is null)
        {
            hotkeyWasDown = false;
            if (state == MapClickNavigationState.Armed)
                CancelArming();
            return;
        }

        if (IsCapturingHotkey)
            CaptureHotkey();
        else
            UpdateHotkey();

        if (state == MapClickNavigationState.Armed)
        {
            MapClickDestination? current = ReadCurrentFlag();
            if (current is not null && current != flagWhenArmed)
                Start(current);
        }

        if (state != MapClickNavigationState.Navigating || activeRouteId is null)
            return;

        NavigationRouteExecutionStatus status = navigation.Status;
        if (status.RouteId != activeRouteId || status.IsActive)
            return;

        activeRouteId = null;
        if (status.State == NavigationRouteExecutionState.Completed &&
            status.Code is "nexus-route-completed" or "suite-route-completed")
        {
            state = MapClickNavigationState.Arrived;
            message = "Destination reached. Map navigation is finished.";
        }
        else
        {
            state = MapClickNavigationState.Failed;
            message = string.IsNullOrWhiteSpace(status.Message)
                ? "Navigation stopped before reaching the selected map point."
                : status.Message;
        }
    }

    internal bool ArmNextMapClick(out string result)
    {
        if (navigation.Status.IsActive)
        {
            state = MapClickNavigationState.Blocked;
            result = message = "Stop the current Nexus trip before selecting another map destination.";
            return false;
        }

        flagWhenArmed = ReadCurrentFlag();
        destination = null;
        activeRouteId = null;
        state = MapClickNavigationState.Armed;
        result = message = "Armed. Place a new flag anywhere on the FFXIV map with Ctrl + right-click.";
        return true;
    }

    internal void CancelArming()
    {
        flagWhenArmed = null;
        state = MapClickNavigationState.Idle;
        message = "Map-click navigation was cancelled.";
    }

    internal bool NavigateToCurrentFlag(out string result)
    {
        MapClickDestination? selected = ReadCurrentFlag();
        if (selected is null)
        {
            state = MapClickNavigationState.Blocked;
            result = message = "No map flag is set. Place a flag on the FFXIV map first.";
            return false;
        }

        return Start(selected, out result);
    }

    internal bool Stop(out string result)
    {
        if (state == MapClickNavigationState.Armed)
        {
            CancelArming();
            result = message;
            return true;
        }

        if (state != MapClickNavigationState.Navigating || activeRouteId is null)
        {
            result = "No map-navigation trip is active.";
            return false;
        }

        navigation.Stop();
        activeRouteId = null;
        state = MapClickNavigationState.Idle;
        result = message = "Map navigation stopped.";
        return true;
    }

    internal void StartHotkeyCapture()
    {
        captureInitiallyDown.Clear();
        foreach (VirtualKey key in keyState.GetValidVirtualKeys())
            if (keyState[key])
                captureInitiallyDown.Add(key);
        IsCapturingHotkey = true;
        hotkeyWasDown = false;
    }

    internal void CancelHotkeyCapture()
    {
        IsCapturingHotkey = false;
        captureInitiallyDown.Clear();
    }

    internal void ClearHotkey()
    {
        CancelHotkeyCapture();
        configuration.MapClickNavigationHotkey = 0;
        configuration.MapClickNavigationHotkeyControl = false;
        configuration.MapClickNavigationHotkeyShift = false;
        configuration.MapClickNavigationHotkeyAlt = false;
        configuration.Save();
    }

    internal void Save() => configuration.Save();

    private bool Start(MapClickDestination selected) => Start(selected, out _);

    private bool Start(MapClickDestination selected, out string result)
    {
        if (navigation.Status.IsActive)
        {
            state = MapClickNavigationState.Blocked;
            result = message = "Stop the current Nexus trip before selecting another map destination.";
            return false;
        }

        var route = new NavigationRouteSnapshot(
            Guid.NewGuid(), $"Map destination — {selected.TerritoryName}", selected.TerritoryId,
            [new NavigationRoutePoint(selected.X, 1024f, selected.Z)],
            "Temporary Nexus map destination.", "built-in, map-navigation",
            true, false, 2.5f, 2.5f, 0, 0, string.Empty, false, DateTime.UtcNow);
        NavigationRouteExecutionStatus started = navigation.Start(
            route, NavigationRoutePlanKind.Playback, resolveDestinationFloor: true);
        destination = selected;
        flagWhenArmed = null;
        if (!started.IsActive)
        {
            state = MapClickNavigationState.Blocked;
            result = message = started.Message;
            return false;
        }

        activeRouteId = route.Id;
        state = MapClickNavigationState.Navigating;
        result = message = $"Travelling to the selected point in {selected.TerritoryName}.";
        return true;
    }

    private unsafe MapClickDestination? ReadCurrentFlag()
    {
        AgentMap* map = AgentMap.Instance();
        if (map is null || map->FlagMarkerCount == 0)
            return null;

        FlagMapMarker marker = map->FlagMapMarkers[0];
        string territoryName = dataManager.GetExcelSheet<TerritoryType>()
            .GetRowOrDefault(marker.TerritoryId)?
            .PlaceName.ValueNullable?.Name.ToString() ?? $"Territory {marker.TerritoryId}";
        return new MapClickDestination(
            marker.TerritoryId, marker.MapId, marker.XFloat, marker.YFloat, territoryName);
    }

    private void UpdateHotkey()
    {
        VirtualKey key = (VirtualKey)configuration.MapClickNavigationHotkey;
        if (!configuration.MapClickNavigationHotkeyEnabled ||
            configuration.MapClickNavigationHotkey == 0 || !Enum.IsDefined(key))
        {
            hotkeyWasDown = false;
            return;
        }

        bool control = ModifierDown(VirtualKey.CONTROL, VirtualKey.LCONTROL, VirtualKey.RCONTROL);
        bool shift = ModifierDown(VirtualKey.SHIFT, VirtualKey.LSHIFT, VirtualKey.RSHIFT);
        bool alt = ModifierDown(VirtualKey.MENU, VirtualKey.LMENU, VirtualKey.RMENU);
        bool modifiersMatch = configuration.MapClickNavigationHotkeyExactModifiers
            ? control == configuration.MapClickNavigationHotkeyControl &&
              shift == configuration.MapClickNavigationHotkeyShift &&
              alt == configuration.MapClickNavigationHotkeyAlt
            : (!configuration.MapClickNavigationHotkeyControl || control) &&
              (!configuration.MapClickNavigationHotkeyShift || shift) &&
              (!configuration.MapClickNavigationHotkeyAlt || alt);
        bool down = keyState.IsVirtualKeyValid(key) && keyState[key] && modifiersMatch;
        if (down && !hotkeyWasDown)
        {
            if (state == MapClickNavigationState.Armed)
                CancelArming();
            else
                ArmNextMapClick(out _);
        }
        hotkeyWasDown = down;
    }

    private void CaptureHotkey()
    {
        foreach (VirtualKey key in captureInitiallyDown.ToArray())
            if (!keyState[key])
                captureInitiallyDown.Remove(key);

        if (keyState[VirtualKey.ESCAPE] && !captureInitiallyDown.Contains(VirtualKey.ESCAPE))
        {
            CancelHotkeyCapture();
            return;
        }

        foreach (VirtualKey key in keyState.GetValidVirtualKeys())
        {
            if (!keyState[key] || captureInitiallyDown.Contains(key) || IsModifier(key) ||
                key is VirtualKey.LBUTTON or VirtualKey.RBUTTON)
                continue;

            configuration.MapClickNavigationHotkey = (ushort)key;
            configuration.MapClickNavigationHotkeyEnabled = true;
            configuration.MapClickNavigationHotkeyControl = ModifierDown(
                VirtualKey.CONTROL, VirtualKey.LCONTROL, VirtualKey.RCONTROL);
            configuration.MapClickNavigationHotkeyShift = ModifierDown(
                VirtualKey.SHIFT, VirtualKey.LSHIFT, VirtualKey.RSHIFT);
            configuration.MapClickNavigationHotkeyAlt = ModifierDown(
                VirtualKey.MENU, VirtualKey.LMENU, VirtualKey.RMENU);
            hotkeyWasDown = true;
            CancelHotkeyCapture();
            configuration.Save();
            return;
        }
    }

    private static bool IsModifier(VirtualKey key) => key is
        VirtualKey.SHIFT or VirtualKey.LSHIFT or VirtualKey.RSHIFT or
        VirtualKey.CONTROL or VirtualKey.LCONTROL or VirtualKey.RCONTROL or
        VirtualKey.MENU or VirtualKey.LMENU or VirtualKey.RMENU or
        VirtualKey.LWIN or VirtualKey.RWIN;

    private bool ModifierDown(VirtualKey generic, VirtualKey left, VirtualKey right) =>
        keyState.IsVirtualKeyValid(generic) && keyState[generic] ||
        keyState.IsVirtualKeyValid(left) && keyState[left] ||
        keyState.IsVirtualKeyValid(right) && keyState[right];
}
