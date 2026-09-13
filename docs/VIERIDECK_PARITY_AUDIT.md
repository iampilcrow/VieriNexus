# VieriDeck parity and retirement audit

Status: complete in VieriNexus 0.1.0.56, pending one ordinary in-game acceptance pass.

## Directly preserved

- Live installed-plugin catalog, including disabled-plugin visibility and loaded/version status.
- Search across plugin names, descriptions, commands, and command help.
- Manual catalog/command refresh.
- Favorites, favorites-only filtering, hidden plugins, restore-hidden, and plugins-without-actions filtering.
- Yellow filled favorite star and gray unfilled non-favorite star.
- Favorites before every remaining plugin, matching VieriDeck ordering.
- Safe plugin main/configuration window opening and non-overlay window toggling.
- Registered command discovery, documented subcommand enrichment, Lifestream shortcuts, and saved custom commands.
- Command left-click execution and right-click copy.
- Custom command add/remove, preferred quick actions, and command-only plugin actions.
- Dedicated configuration opening, hiding, and internal-name copy through the plugin action menu.
- Close-after-opening behavior.
- Imported hotkey, enable/disable, exact modifiers, capture/cancel/clear, and the unmodified-key warning.
- Dalamud Plugins and Dalamud Settings quick-access buttons.

## Nexus equivalents

- VieriDeck's **Toggle AD Overlay** is **Show/Hide Nexus Overlay**. Nexus owns the custom operations overlay and no longer depends on the VieriAutoDuty overlay toggle.
- **VieriDeck Settings** is **Nexus Settings**. Interface scale and overlay presentation are global Nexus settings; plugin-list filters and hotkey controls remain on Plugins.
- `/nexus plugins` replaces `/vd`; `/nexus commands`, `/nexus commandcenter`, and `/nexus deck` remain compatible aliases. Nexus does not claim `/vd` while VieriDeck may still be installed because duplicate command ownership would be unsafe.

## Intentional integrated-layout changes

- The detached VieriDeck command window is replaced by commands expanding directly beneath the selected plugin. This satisfies the user requirement for one page scrollbar and removes a second movable/resizable pane.
- VieriDeck's standalone window position/width/height reset has no separate Nexus equivalent because Plugins is a page inside the single resizable Nexus window. The imported values remain in migration evidence for rollback, while Nexus uses its global window and interface-scale behavior.
- VieriDeck's own settings-window and deck-window open buttons are unnecessary inside the Nexus page; the equivalent Nexus Settings and Plugins navigation are always present.

## Retirement gate

VieriDeck can be disabled after updating to 0.1.0.56 and confirming once that:

1. Existing favorites appear first with yellow filled stars.
2. Dalamud Plugins, Dalamud Settings, Show/Hide Nexus Overlay, and Nexus Settings work.
3. Show Commands expands directly beneath a plugin; left-click runs and right-click copies a command.
4. Disabling and re-enabling Nexus retains favorites, filters, custom commands, and hotkey settings.

VieriDeck does not need to remain enabled after those checks. Its original configuration and the Nexus migration backup/receipt remain available for rollback; Nexus never deletes the predecessor configuration automatically.
