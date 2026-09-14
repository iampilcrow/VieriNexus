namespace VieriNexus.Application.Tests;

public sealed class WorldAutomationPolicyTests
{
    [Fact]
    public void SelectsUnlockingOrderBeforeExplorationAndAchievements()
    {
        WorldAutomationSelection selection = new(true, true, true, true);
        WorldAutomationAvailability availability = new(2, 3, 4, 5, 6);

        Assert.Equal(WorldAutomationActivity.Aetheryte,
            WorldAutomationPolicy.SelectNext(selection, availability));
        Assert.Equal(WorldAutomationActivity.FieldAetherCurrent,
            WorldAutomationPolicy.SelectNext(selection, availability with { ReachableAetherytes = 0 }));
        Assert.Equal(WorldAutomationActivity.AetherCurrentQuest,
            WorldAutomationPolicy.SelectNext(selection, availability with
            {
                ReachableAetherytes = 0,
                ReachableFieldAetherCurrents = 0,
            }));
        Assert.Equal(WorldAutomationActivity.Exploration,
            WorldAutomationPolicy.SelectNext(selection, availability with
            {
                ReachableAetherytes = 0,
                ReachableFieldAetherCurrents = 0,
                ReadyAetherCurrentQuests = 0,
            }));
        Assert.Equal(WorldAutomationActivity.Achievement,
            WorldAutomationPolicy.SelectNext(selection, availability with
            {
                ReachableAetherytes = 0,
                ReachableFieldAetherCurrents = 0,
                ReadyAetherCurrentQuests = 0,
                ReachableExplorationRegions = 0,
            }));
    }

    [Fact]
    public void DisabledOrUnavailableActivitiesAreNotSelected()
    {
        WorldAutomationSelection selection = new(false, true, false, false);
        WorldAutomationAvailability availability = new(9, 0, 0, 9, 9);

        Assert.Null(WorldAutomationPolicy.SelectNext(selection, availability));
    }
}
