using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using CharacterStruct = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace VieriRotationHelper;

internal sealed class TargetAnalysis(IObjectTable objectTable, ITargetManager targetManager, IDataManager dataManager)
{
    internal TargetSnapshot Snapshot(uint actionId)
    {
        var player = objectTable.LocalPlayer;
        var target = targetManager.Target;
        if (player == null)
            return new TargetSnapshot(0, null);

        var action = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(actionId);
        var radius = action.HasValue ? Math.Max(0f, action.Value.EffectRange) : 5f;
        var centeredOnPlayer = action?.Range == 0;

        var validTarget = target != null && IsHostile(target) && target.IsTargetable && !target.IsDead;
        var origin = !centeredOnPlayer && validTarget ? target! : player;
        var count = !centeredOnPlayer && validTarget ? 1 : 0;

        foreach (var candidate in objectTable)
        {
            if (!candidate.IsTargetable || candidate.IsDead || candidate.GameObjectId == origin.GameObjectId || !IsHostile(candidate))
                continue;
            var distance = Vector3.Distance(candidate.Position, origin.Position) - candidate.HitboxRadius;
            if (distance <= radius)
                count++;
        }

        float? targetRange = validTarget ? target!.CurrentDistance : null;
        return new TargetSnapshot(count, targetRange);
    }

    private static unsafe bool IsHostile(IGameObject gameObject)
    {
        if (gameObject is not ICharacter || gameObject.Address == nint.Zero)
            return false;
        if (gameObject.SubKind is not (1 or 5))
            return false;
        return ((CharacterStruct*)gameObject.Address)->CharacterData.Battalion > 0;
    }
}
