# VieriLink

Private Discord live status, notifications, and allowlisted remote controls for VieriAutoDuty.

VieriLink reads live data through `AutoDuty.GetVieriStatus`, edits one persistent message in a dedicated status channel, and polls a separate private command channel for commands beginning with `!vieri`. Command responses and enabled notifications are sent to the command channel. The Discord bot token is protected with Windows DPAPI and is not stored as plaintext in the Dalamud configuration.

The bot should receive only View Channel, Send Messages, Embed Links, and Read Message History in the two dedicated channels. Enable Discord's Message Content intent for the bot. Never grant Administrator.

One VieriLink installation controls one character and two Discord channels: one clean, permanent status channel and one commands/notifications channel. You and a friend can use the same private bot with separate channel pairs, then configure each PC with its own two channel IDs and allowlisted Discord user IDs.

## Live status and notifications

The persistent status message can show character, current location, job, permanent job level and level-progress percentage, item level, duty, current state/action, completed/configured runs, inventory, durability, and runtime. Location is read from the character's live in-game territory, so the card changes when travel actually reaches a new area. Every status field can be shown or hidden independently. Notifications can be enabled independently for queue readiness, duty completion, deaths, duty changes, automation stopping, every genuine level gained, a chosen target level, low durability, and high inventory use. Duty level sync, zoning, login, job changes, and loading-screen durability gaps do not generate false alerts.

## Remote commands

- `!vieri status`
- `!vieri start`, `stop`, `pause`, or `resume`
- `!vieri leave` to stop automation, wait for combat to end, and safely leave the duty
- `!vieri loops 10`, `!vieri loops +5`, or `!vieri loops -2`
- `!vieri sell`, `repair`, or `inn`
- `!vieri job 3` or `!vieri job Gearset Name`
- `!vieri get SettingName`
- `!vieri set SettingName value` when advanced remote configuration is explicitly enabled

Every command is restricted to the configured Discord user allowlist, acknowledged in Discord, and recorded in the in-game audit line. Old channel messages are never replayed as commands when VieriLink is first connected.
