using DelvUI.Interface.Highlighting;
using Xunit;

public class HighlightingPartyRosterTests
{
    private static readonly Dictionary<int, HighlightCombatRole> Icons = new()
    {
        [62121] = HighlightCombatRole.Tank,
        [62128] = HighlightCombatRole.Healer,
        [62135] = HighlightCombatRole.Damage,
        [62142] = HighlightCombatRole.Damage
    };

    [Fact]
    public void DutySupportRosterRecognizesCompanionsWithoutPlayerPartyFlags()
    {
        var roster = new HighlightingPartyRoster();
        roster.Add(100, 62128); // Player healer
        roster.Add(101, 62121); // Wuk Lamat tank
        roster.Add(102, 62135); // Alisaie DPS
        roster.Add(103, 62142); // Krile DPS
        foreach (uint id in new uint[] { 100, 101, 102, 103 }) Assert.True(roster.IsPartyMember(id, false));
        Assert.Equal(HighlightCombatRole.Healer, roster.ResolveRole(100, 4, Icons));
        Assert.Equal(HighlightCombatRole.Tank, roster.ResolveRole(101, 0, Icons));
        Assert.Equal(HighlightCombatRole.Damage, roster.ResolveRole(102, 0, Icons));
        Assert.Equal(HighlightCombatRole.Damage, roster.ResolveRole(103, 0, Icons));
        Assert.False(roster.IsPartyMember(104, false)); // Separate pet or another NPC
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0xE0000000u)]
    [InlineData(uint.MaxValue)]
    public void InvalidRowsDoNotCreatePartyMembership(uint id)
    {
        var roster = new HighlightingPartyRoster();
        roster.Add(id, 62121);
        Assert.False(roster.IsPartyMember(id, false));
    }

    [Fact]
    public void RegularPartyFlagsStillWorkWhenNativeRowsAreUnavailable()
    {
        var roster = new HighlightingPartyRoster();
        Assert.True(roster.IsPartyMember(123, true));
        Assert.Equal(HighlightCombatRole.Healer, roster.ResolveRole(123, 4, Icons));
    }

    [Fact]
    public void RefreshDropsOldPartyIdsAndReevaluatesFlexibleNpcRole()
    {
        var roster = new HighlightingPartyRoster();
        roster.Add(101, 62121);
        Assert.Equal(HighlightCombatRole.Tank, roster.ResolveRole(101, 4, Icons));
        roster.Clear();
        Assert.False(roster.IsPartyMember(101, false));
        roster.Add(102, 62128);
        Assert.Equal(HighlightCombatRole.Healer, roster.ResolveRole(102, 1, Icons));
    }

    [Theory]
    [InlineData((byte)0, HighlightCombatRole.None)]
    [InlineData((byte)1, HighlightCombatRole.Tank)]
    [InlineData((byte)2, HighlightCombatRole.Damage)]
    [InlineData((byte)3, HighlightCombatRole.Damage)]
    [InlineData((byte)4, HighlightCombatRole.Healer)]
    [InlineData((byte)255, HighlightCombatRole.None)]
    internal void UnknownPartyIconUsesActualActorJobRole(byte actorRole, HighlightCombatRole expected)
    {
        var roster = new HighlightingPartyRoster();
        roster.Add(101, 999999);
        Assert.Equal(expected, roster.ResolveRole(101, actorRole, Icons));
    }
}
