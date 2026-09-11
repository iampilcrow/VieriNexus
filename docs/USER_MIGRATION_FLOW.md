# VieriNexus install-first migration

This flow applies separately on every computer. VieriNexus never copies one player's configuration, character IDs, routes, or secrets to another player.

1. Install VieriNexus from the Daily Pilcrow Dalamud repository while the existing Vieri products are still installed and their local configuration files still exist.
2. Log into the character whose settings should be selected by default.
3. Open **Migration** and use **Set Up This Computer > Back up and prepare detected settings**.
4. Confirm the page reports verified Nexus working copies. The import writes timestamped source backups and leaves every predecessor configuration unchanged.
5. Keep each predecessor enabled until its corresponding Nexus replacement is marked ready and the normal workflow has been checked on that computer.
6. Disable the replaced Vieri product. Do not delete its configuration or backup yet.
7. Keep stock dependencies such as Lifestream, vnavmesh, Boss Mod, Questionable, and eventually stock AutoDuty installed when the enabled Nexus modules require them.

For a second user, repeat the same process on their computer before disabling their Vieri products. Their Nexus installation imports their own routes, profile-to-character assignments, operation policies, and overlay preferences; it does not receive the first user's data.

Current automatic preparation supports VieriNavPlotter routes/settings and VieriAutoDuty profiles/operations. Other predecessor products remain authoritative until their dedicated validated importers and Nexus-owned replacements ship.
