using System.Collections.Generic;

namespace DelvUI.Interface.Highlighting
{
    internal enum HighlightCombatRole { None, Tank, Healer, Damage }

    // Membership is by live entity ID, never NPC name. Trust and Duty Support
    // companions can be in the native party list without the PartyMember flag.
    internal sealed class HighlightingPartyRoster
    {
        private readonly Dictionary<uint, int> _members = new();

        public void Clear() => _members.Clear();

        public void Add(uint entityId, int classIconId)
        {
            if (entityId is 0 or 0xE0000000 or uint.MaxValue) return;
            _members[entityId] = classIconId;
        }

        public bool IsPartyMember(uint entityId, bool partyFlag) => partyFlag || _members.ContainsKey(entityId);

        public HighlightCombatRole ResolveRole(uint entityId, byte actorRole,
            IReadOnlyDictionary<int, HighlightCombatRole> iconRoles)
        {
            // The displayed party role also handles flexible-role NPC companions.
            if (_members.TryGetValue(entityId, out int icon) &&
                iconRoles.TryGetValue(icon, out HighlightCombatRole role) && role != HighlightCombatRole.None)
                return role;

            return RoleFromClassJob(actorRole);
        }

        public static HighlightCombatRole RoleFromClassJob(byte role) => role switch
        {
            1 => HighlightCombatRole.Tank,
            4 => HighlightCombatRole.Healer,
            2 or 3 => HighlightCombatRole.Damage,
            _ => HighlightCombatRole.None
        };
    }
}
