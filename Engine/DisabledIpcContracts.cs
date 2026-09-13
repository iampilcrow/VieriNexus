// Compile-time compatibility names for upstream's UNUSED IPC/UI code.
// This is not an IPC client or provider. No WrathCombo.API implementation is
// bundled or initialized. Numeric rotation modes retain configuration format
// compatibility; descriptions are intentionally just enum names.
namespace WrathCombo.API.Enum
{
    public enum ComboTargetTypeKeys { SingleTargetDPS, AoEDPS, SingleTargetHeals, AoEHeals, Other }
    public enum ComboSimplicityLevelKeys { Simple, Advanced, Other }
    public enum ComboStateKeys { Enabled, AutoMode }
    public enum DPSRotationMode { Manual, Highest_Max, Lowest_Max, Highest_Current, Lowest_Current, Tank_Target, Nearest, Furthest }
    public enum HealerRotationMode { Manual, Highest_Current, Lowest_Current }
    public enum BailMessage { InvalidLease, BlacklistedLease, LiveDisabled }
    public enum CancellationReason { JobChanged, LeaseeReleased, WrathPluginDisabled, WrathUserManuallyCancelled, AllServicesSuspended, LeaseePluginDisabled }
    public enum SetResult { Okay, OkayWorking, Duplicate, IGNORED, InvalidConfiguration, InvalidLease, InvalidValue, IPCDisabled, PlayerNotAvailable, BlacklistedLease }
    public enum AutoRotationConfigOption
    {
        InCombatOnly, DPSRotationMode, HealerRotationMode, FATEPriority, QuestPriority,
        SingleTargetHPP, AoETargetHPP, SingleTargetRegenHPP, ManageKardia, AutoRez,
        AutoRezDPSJobs, AutoCleanse, IncludeNPCs, OnlyAttackInCombat, OrbwalkerIntegration,
        AutoRezOutOfParty, DPSAoETargets, SingleTargetExcogHPP, AutoRezDPSJobsHealersOnly,
        DPSAlwaysHardTarget, HealerAlwaysHardTarget, BypassQuest, BypassFATE,
        IgnoreRangeInBoss, UnTargetAndDisableForPenalty
    }
}
namespace WrathCombo.API.Attribute
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class ConfigValueTypeAttribute : System.Attribute
    {
        public System.Type ValueType { get; }
        public ConfigValueTypeAttribute(System.Type type) { ValueType = type; }
    }
}
namespace WrathCombo.API.Extension
{
    public static class DisabledIpcExtensions
    {
        extension(Enum.BailMessage value) { public string Description => value.ToString(); }
        extension(Enum.CancellationReason value) { public string Description => value.ToString(); }
        extension(Enum.AutoRotationConfigOption option)
        {
            public System.Type ValueType => option switch
            {
                Enum.AutoRotationConfigOption.DPSRotationMode => typeof(Enum.DPSRotationMode),
                Enum.AutoRotationConfigOption.HealerRotationMode => typeof(Enum.HealerRotationMode),
                Enum.AutoRotationConfigOption.SingleTargetHPP or Enum.AutoRotationConfigOption.AoETargetHPP or
                Enum.AutoRotationConfigOption.SingleTargetRegenHPP or Enum.AutoRotationConfigOption.DPSAoETargets or
                Enum.AutoRotationConfigOption.SingleTargetExcogHPP => typeof(int),
                _ => typeof(bool)
            };
        }
    }
}
