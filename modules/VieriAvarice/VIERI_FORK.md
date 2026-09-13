# VieriAvarice

VieriAvarice is Valentina Vieri's upstream-compatible customization of
[PunishXIV Avarice](https://github.com/PunishXIV/Avarice).

The fork keeps Avarice's positional and range overlays while enabling native
FFXIV UI masking by default. Overlay graphics render behind maps, inventory,
hotbars, job gauges, and comparable native windows. Existing Avarice settings
are copied on VieriAvarice's first launch and the original configuration is
left untouched.

## Repository layout

- `origin` points to the private VieriAvarice backup repository.
- `upstream` points to `PunishXIV/Avarice`.
- `vieri` is the maintained release branch.

Run `scripts/sync-upstream.ps1` to fetch upstream changes and review them on a
temporary sync branch before merging them into `vieri`.

## License

The upstream Avarice and PunishLib BSD 3-Clause licenses remain included. The
Vieri changes are distributed under those same terms.
