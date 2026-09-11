using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class OperationsExecutionPolicyTests
{
    [Fact]
    public void ConfiguredBatchIncludesOnlyNativeSafeActionsInStableOrder()
    {
        AutoDutyMaintenancePolicy policy = Policy() with
        {
            AutoRepair = true,
            RepairWithCrafter = true,
            AutoExtract = true,
            RegisterTripleTriadCards = true,
            RegisterMinions = true,
            RegisterOrchestrionRolls = true,
            AutoOpenCoffers = true,
            AutoSell = true,
            AutoDesynth = true,
            AutoGrandCompanyTurnIn = true,
        };

        Assert.Equal([
            NexusMaintenanceOperation.Repair,
            NexusMaintenanceOperation.ExtractMateria,
            NexusMaintenanceOperation.RegisterTripleTriadCards,
            NexusMaintenanceOperation.RegisterMinions,
            NexusMaintenanceOperation.RegisterOrchestrionRolls,
            NexusMaintenanceOperation.OpenCoffers,
        ], OperationsExecutionPolicy.ConfiguredOperations(policy));
    }

    [Fact]
    public void VendorRepairIsNotSilentlyReplacedBySelfRepair()
    {
        AutoDutyMaintenancePolicy policy = Policy() with { AutoRepair = true, RepairWithCrafter = false };
        Assert.DoesNotContain(NexusMaintenanceOperation.Repair,
            OperationsExecutionPolicy.ConfiguredOperations(policy));
    }

    [Fact]
    public void StrikingDummyCatalogPreservesExpansionCoverageAndCoordinateConversion()
    {
        Assert.Contains(StrikingDummyCatalog.Destinations, item => item.Expansion == "A Realm Reborn");
        Assert.Contains(StrikingDummyCatalog.Destinations, item => item.Expansion == "Dawntrail");
        (float x, float z) = StrikingDummyCatalog.MapToWorld(24, 19.5f, 100, 0, 0);
        Assert.True(float.IsFinite(x));
        Assert.True(float.IsFinite(z));
    }

    private static AutoDutyMaintenancePolicy Policy() => new(
        false, 1_000_000, false, false, 50, true, null, false, false, false, null, false,
        new Dictionary<uint, string>(), false, false, 50, false, true, 1, false, false, 5, false,
        false, false, false, false, false, false, 1, 1, false, "UnneededEquipment", true, 120,
        true, 85, true, null, false, true, 20, true, false, false, false);
}
