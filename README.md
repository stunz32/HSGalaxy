# HSGalaxy Arena Draft Assistant

Early repository bootstrap per Checklist Galaxy plan. See `Checklist Galaxy.md` for the authoritative roadmap and validation gates.

- Root: `D:\\cursor bots\\HSGalaxy\\`
- Primary goals: WPF app with overlay + OCR pipeline for Hearthstone Arena drafting.

Status: repo initialized (git), base config files added.
# HSGalaxy

Windows overlay + OCR helper for Hearthstone calibration workflows. WPF UI with Vortice D3D11/DirectComposition renderer and a CLI for validations.

## Quick Start

- Build: `dotnet build`
- Run App (Wizard only):
  - `setx HSGALAXY_DISABLE_OVERLAY 1`
  - `setx HSGALAXY_SHOW_WIZARD 1`
  - `dotnet run --project src/HSGalaxy.App`

## OCR (Azure/offline) Env Vars

- `HSGALAXY_AZURE_VISION_ENDPOINT` and `HSGALAXY_AZURE_VISION_KEY`
- Test flags: `HSGALAXY_OCR_FORCE_OFFLINE=1`, `HSGALAXY_OCR_FORCE_429=1`

## CLI Commands

`dotnet run --project src/HSGalaxy.CLI --`

Key commands:
- `ocr:test` — auto-select OCR client and print results
- `calib:mkprofile-window <query> <name>` — make profile from a window
- `calib:capture-profile <name>` — capture composite PNG for a saved profile
- `calib:list|export|export-all|import|rename|delete`
- `storage:validate|primary-probe`
- `fs:longpath`
- `strip:render [dpi] [theme]`
- `wgc:fps|window|validate`
- `evidence:bundle` — package logs/screens/exports into a ZIP
- `evidence:report` — HTML report with embedded images and overlay.log tail

## Wizard Self-Test

Set the following for automation:
- `HSGALAXY_SHOW_WIZARD=1`, `HSGALAXY_WIZARD_SELFTEST=1`, optional `HSGALAXY_WIZARD_SELFTEST_CAPTURE=1`, and `HSGALAXY_EXIT_AFTER_TEST=1`.

## Logs

- Primary: `D:\cursor_bots\HSGalaxy\logs\overlay.log`
- Fallback: `%LOCALAPPDATA%\HSGalaxy\logs\overlay.log`

