# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

## [0.1.0] - 2025-10-03 (feat/vortice-dcomp)
- Wizard Offline/429 fallback: logs + visual banner; self-test automation with screenshot capture.
- DPI validation: status strip renders at 120/144/200 DPI; Per-Monitor-V2 confirmed.
- Storage: primary root selection + validation; long-path runtime proof (len=401).
- Wizard UX: Rename… and Delete… profile actions with confirmations and live refresh.
- CLI enhancements:
  - calib:* flows for capture/list/export/import/rename/delete
  - wgc:* validations (fps/window/validate)
  - strip:render for DPI proofs
  - evidence:bundle and evidence:report for packaging evidence
- Tests: Core (4) and OCR (3) passing on .NET 8.

