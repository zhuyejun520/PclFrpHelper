# Changelog

All notable changes to this project are documented here.

## [0.2.0] - 2026-10-22

### Added

- 樱花FRP support: account info, tunnel list, auto-fill tunnel ID, launch official frpc.
- 公版 FRP support: custom frps address/port/token, local/remote ports, TCP/UDP.
- 「获取密钥」shortcut buttons for OpenFrp and 樱花FRP consoles.
- Liquid Glass UI: borderless rounded window, gradient + glow backdrop, frosted cards,
  pill buttons, segmented tab strip, owner-drawn list and log box.

### Fixed

- Tab switching flicker / ghosting (all controls now paint the same backdrop, composited window).
- 樱花FRP log box only rendered a thin strip (multiline layout was never recalculated).
- Selecting a tunnel did not fill the tunnel ID field.

## [0.1.0] - 2026-10-21

### Added

- OpenFrp support: token → tunnel list → one-click launch of the official frpc,
  public address display and copy.
- `PclFrpAddon` WPF addon that injects an "FRP 联机" entry into PCL's 联机 page.
- `tools/patch-pcl.ps1` Mono.Cecil patcher (show 联机 tab, bypass DisableReason2,
  embed addon, load it from memory).
- Single-file packaging: helper embedded inside the addon, addon embedded inside the PCL exe.
