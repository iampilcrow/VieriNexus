# VieriDelvUI maintenance branch

This private repository keeps Valentina Vieri's DelvUI changes separate from the official project while retaining a clean path for upstream updates.

## Repository layout

- `origin` points to `iampilcrow/VieriDelvUI` (private).
- `upstream` points to `DelvUI/DelvUI` (official).
- `vieri` is the customization and default branch.
- Official changes arrive from `upstream/develop`.

## Compatibility decisions

- The plugin identity and output assembly are `VieriDelvUI`.
- Commands are `/vieridelvui` and `/vdui`, so they do not collide with the official plugin.
- On its first launch, VieriDelvUI copies an existing `pluginConfigs/DelvUI` directory into its own configuration directory only when the destination is empty.
- The original DelvUI settings directory is never moved, overwritten, or deleted.
- Existing DelvUI JSON and `.delvui` profile data remain compatible with the copied configuration.

## Updating from DelvUI

Run `scripts/sync-upstream.ps1` from PowerShell. The script requires a clean working tree, fetches the official `develop` branch, merges it into `vieri`, and builds the result. Resolve merge conflicts deliberately and test in game before pushing.

The scheduled GitHub workflow also opens an issue when the branch falls behind official DelvUI. It never merges upstream code automatically.

## Distribution

DelvUI is licensed under AGPL-3.0. If a VieriDelvUI binary is distributed to another person, the corresponding source and license must be made available to that recipient under the same license.

The first successful private package is preserved as the prerelease `v2.7.0.1-vieri.1`. It is intentionally marked as a prerelease until its configuration copy and HUD behavior have been tested in game.
