# 1) Overview & Objectives

**Goal**
Build a Windows 11 overlay companion that identifies the three Arena draft cards per pick using a composite-image OCR pipeline and recommends the best pick with clear rationale, fast and reliably.

**Scope**
- MVP features (non‑negotiable): Win11 overlay (click‑through), WGC window capture, composite image per pick with 16‑px gutters + 1‑px separator, Azure Image Analysis 4.0 (v4) Read (sync) primary, Vision Read v3.2 (async) fallback, SymSpell resolver with class/arena filters, tier+synergy/curve recommendation, calibration wizard, status strip + three lanes + recommendation chip + banners, ETW + WPR, Data Root on D:\.
- Extensibility (designed in): pluggable OCR backends (local OCR, future engines), locale pipelines (Latin→EU→CJK), portrait tie‑break module, ONNX/DirectML acceleration, dynamic concurrency, optional local microservice, MSIX packaging, richer diagnostics, additional themes.

**Success criteria**
- Latency: p50 ≤ 300 ms, p95 ≤ 600 ms per pick (sunny path).
- Quality: false‑ID ≤ 0.5% (Latin MVP), ambiguity ≤ 1.0%, unrecognized ≤ 0.5% on ≥ 500‑pick corpus.
- Reliability: capture only when overlay exclusion passes; graceful rate‑limit/offline handling.
- UX: WCAG 2.1 AA legibility; Safe theme guarantees AA; present‑on‑change only.

---

# 2) Assumptions & Constraints

**Platform & Rendering**
- Windows 11 minimum; D3D11 Feature Level ≥ 11_0.
- Per‑Monitor‑V2 DPI (manifest); UI laid out in DIPs; snap text to whole DIPs.
- Overlay HWND: `WS_EX_LAYERED | WS_EX_TRANSPARENT`, top‑most, click‑through by default; hit‑test only if hotspots added later.
- DComp renderer: `CreateSwapChainForComposition`, BGRA8 UNORM, Premultiplied, Flip‑Discard, BufferCount=2 (auto‑escalate to 3 on ETW signal); present‑on‑change only.
- Text AA: grayscale on translucent UI; ClearType only on fully opaque status strip.

**Capture & Hygiene**
- Windows.Graphics.Capture (window capture). Minimized windows deliver no frames; occlusion handled via state machine and banners.
- Apply `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` after HWND creation. Mirror View gate must PASS (0 overlay pixels across 3 consecutive frames) before enabling capture.
- GPU→CPU: staging textures per ROI, triple‑buffer ring; Map ≥2 frames later to avoid stalls.
- HDR/WCG: detect via AdvancedColor/DXGI and tone‑map to sRGB for OCR; overlay swapchain remains sRGB.

**OCR & HTTP**
- Primary: Azure Image Analysis 4.0 Read (sync). Region: West US (configurable).
- Fallback: Vision Read v3.2 async with polling cadence (≤ 2 s total). Local OCR (PaddleOCR) OFF by default; auto‑engage on 429/offline/p95 overrun.
- Singleton HttpClient/HTTP/2; connect timeout 250 ms; overall per‑call 600–1200 ms; backoff on 429.

**Composite & Encoding**
- Three nameplate ROIs tiled into one composite; 16‑px gutters; 1‑px separator; offset table `[roiIndex,x0,y0,w,h]`.
- PNG default; if >150 KB use JPEG 4:4:4 @ q≈90; hard cap ~300 KB; PNG palettization when ≤256 colors.

**Resolver & Data**
- HearthstoneJSON `/v1/latest/` pinned per session. Dictionaries per locale (MVP: enUS) with frequencies.
- SymSpell thresholds: require OCR confidence ≥ 0.70 before fuzzying; edit distance ≤1 for names ≤10 chars, else ≤2; filter by class + Arena eligibility + rotation.

**Recommendation & Ledger**
- Deterministic tier scores + synergy/curve modifiers. Ledger (recognized picks) is SoT; deck‑panel parse (optional later) wins only at high confidence (≥0.9), else flag discrepancy and prefer ledger.

**Storage, Keys & Privacy**
- Data Root: `D:\cursor bots\HSGalaxy\{config,calibration,dict,tiers,logs,dumps}`. If missing, fallback to `%LOCALAPPDATA%` for the session (red banner) and resume to D:\ when back.
- Azure key: Windows Credential Manager (CredWrite/CredRead), target name `HearthArenaDraft:AzureVision`; rotate in‑app.
- Logs: JSONL, daily roll + ZIP, 14‑day TTL, 200 MB LRU cap. Dumps: WER LocalDumps to D:\dumps, DumpCount=5; app‑level 50 MB cap.

**Non‑Goals (MVP)**
- No input automation (no simulated clicks/keys). No client memory inspection. Cloud usage is OCR‑only; no full screen uploads.

---

# 3) Stakeholders & RACI

| Role | Person/Team | Responsibilities | Decision Rights |
|---|---|---|---|
| Product Owner | You | Feature priorities, success criteria | Final scope/gates |
| Tech Lead / Dev | You | Architecture, implementation, performance | Technical decisions |
| UX | You | UI layout, tokens, accessibility | Visual decisions |
| QA | You | Corpus, harness, validation | Release Go/No‑Go per locale |
| Ops | You | Keys, WER config, backups | Operational policies |

---

# 4) Milestones & Timeline (ASSUMED)

- **M1 (Week 1)**: Overlay skeleton (HWND, DComp swapchain, present‑on‑change), status strip scaffold, Data Root paths.
- **M2 (Week 2)**: WGC capture + readback; Mirror View gate; calibration wizard skeleton.
- **M3 (Week 3)**: Composite builder + Azure v4 client + v3.2 fallback + resolver pipeline.
- **M4 (Week 4)**: Overlay UI (lanes, chip, banners), hotkeys, Settings tabs.
- **M5 (Week 5)**: Diagnostics (ETW, WPR profiles), WER configurator, logs/dumps caps, backup.
- **M6 (Week 6)**: Validation corpus + bench runner; tuning; RC build.

Critical path: Capture/Composite → OCR → Resolver → Recommendation → Overlay UI → Validation.

---

# 5) Work Breakdown Structure (WBS) — AI Task List

| ID | Task Name | Owner/Role | Inputs | Step‑by‑Step Instructions | Tools/Systems | Dependencies (IDs) | Duration (est) | Acceptance Criteria (testable) | Risks & Mitigations | Outputs/Deliverables |
|---|---|---|---|---|---|---|---|---|---|---|
| A1 | Repo & solution bootstrap | Dev | N/A | Init git; add .editorconfig/.gitignore; create projects App/Core/OCR.Azure/OCR.Local/UI/Diagnostics/CLI; CI build | Git/VS | — | 0.5d | Debug/Release build clean; CI green | Toolchain | Repo + CI |
| A2 | App manifest (PMv2, longPathAware) | Dev | Windows SDK | Add manifest flags; verify in Process Explorer | VS | A1 | 0.25d | Flags visible; DPI moves OK | Mis‑config | Manifest |
| A3 | Data Root & folders | Dev | Plan | Settings ▸ Storage; default LocalAppData; override D:\; create subfolders | Win32, Known Folders | A1 | 0.5d | Writes go to D:\; fallback works with banner | Permissions | Folder tree |
| A4 | Overlay HWND (layered, transparent) | Dev | Spec | Create top‑most HWND with WS_EX_LAYERED|WS_EX_TRANSPARENT; click‑through | Win32 | A1 | 0.5d | Click‑through verified | Hit‑test quirks | Overlay window |
| A5 | DComp swapchain & visual | Dev | A4 | Create BGRA8 Premul Flip‑Discard (2 buffers); bind visual; present‑on‑change | D3D11, DComp | A4 | 1d | No idle presents; escalate to 3 on ETW drops | Stutter | Renderer core |
| A6 | Status strip scaffold | Dev | Tokens | Opaque bar; text fields placeholders | DirectWrite | A5 | 0.5d | Crisp at 100/125/150% | Fonts | Status UI |
| B1 | Mirror View gate | Dev | A4–A6 | 3‑frame capture; assert 0 overlay pixels; block capture if fail | WGC/D3D11 | A6 | 1d | PASS required before capture enable | False positives | Mirror tool |
| B2 | WGC capture session | Dev | Window handle | Create capture item; frame pool; states Active/Occluded/Minimized | WGC API | B1 | 1d | State changes → banners | Minimize | Capture loop |
| B3 | Readback path | Dev | B2 | CopySubresourceRegion→staging; triple‑buffer; Map after ≥2 frames | D3D11 | B2 | 1d | No GPU stalls (ETW) | Stalls → increase defer | Readback module |
| C1 | Calibration wizard | Dev/UX | Spec | Step1 select window; Step2 draw 3 ROIs; Step3 validate w/ preview | Overlay UI | B2 | 1.5d | Profile saved/loaded; Show My Crops aligns | Drift | Wizard + JSON |
| C2 | Composite builder | Dev | C1 | 16‑px gutters; 1‑px sep; offset table; PNG→JPEG 4:4:4 q≈90 if >150 KB | WIC | C1 | 1d | Sizes ≤150 KB typical; ≤300 KB max | Size | Composite images |
| D1 | HttpClient | Dev | A1 | Singleton, HTTP/2, connect 250 ms, per‑call 600–1200 ms; retry/backoff | WinHTTP/.NET | A1 | 0.5d | Reuse; TLS OK | DNS | HTTP layer |
| D2 | Azure v4 client (sync) | Dev | D1 | Implement Read (sync); map boxes to ROI via offsets | REST/SDK | C2 | 1d | Valid OCR on test | Region issues | v4 client |
| D3 | v3.2 fallback (async) | Dev | D2 | Implement async Read with polling ≤2 s | REST/SDK | D2 | 0.75d | Fallback engages on anomalies | Latency | v3.2 client |
| D4 | Local OCR fallback | Dev | D3 | PaddleOCR bridge; OFF by default; trigger on 429/offline/p95 overrun | PaddleOCR | D3 | 1d | Only runs on failure; banner shows Offline | Footprint | Local OCR |
| E1 | HSJSON ingest | Dev | Net | Fetch `/v1/latest/`; cache by build id | HTTP/JSON | C1 | 0.5d | Pinned build id | Network | Data cache |
| E2 | Dictionary builder | Dev | E1 | Build SymSpell dict (enUS) | SymSpell | E1 | 0.5d | Dict files created | Bad freq | `.symspell` |
| E3 | Resolver | Dev | D2,E2 | Normalize; thresholds; class/arena filter; edit distance | App logic | E2 | 1d | Correct map on tests | Over‑match | Resolver mod |
| F1 | Tier engine | Dev | Spec | Base tier + synergy/curve; rationale strings | App logic | E3 | 1d | Deterministic output | Stale tiers | Engine |
| F2 | Ledger SoT | Dev | F1 | Track picks; reconcile with panel when ≥0.9 (later); SoT rules | App logic | F1 | 0.5d | Stable counts | Drift | Ledger |
| G1 | Lanes & chip | Dev/UX | A6,F1 | Three lanes (name+OCR%); rec chip (tier/score+reasons); ≤120 ms anims | DWrite/DComp | F1 | 1d | Layout matches spec; AA OK | Busy scene | UI panels |
| G2 | Banners & errors | Dev/UX | B2,D2 | Rate‑limited/Offline/Minimized/Recalibrate/Retry failed/Generic | UI | G1 | 0.5d | Correct per state | Noise | Banner row |
| H1 | Hotkeys & Help | Dev | Spec | Accelerator table; F1 Help lists hotkeys + links | Win32 | G2 | 0.5d | Foreground‑only; toast on no‑op | Conflicts | Help panel |
| H2 | Settings tabs | Dev | Spec | Capture/OCR/Resolver/Overlay/Calibration/Diagnostics | UI | G2 | 1d | Persist across restart | Validation | Settings UI |
| I1 | ETW provider | Dev | Spec | TraceLogging provider & events | ETW | G2 | 0.5d | Visible in WPA | Perf | ETW dll |
| I2 | WPR profiles & launcher | Dev | I1 | Bundle Light/Full .wprp; button to run/open WPA | WPR/WPA | I1 | 0.5d | Traces open | UAC | WPR |
| J1 | Logs & bundle export | Dev | Spec | JSONL/ZIP; export bundle | Zip lib | I1 | 0.5d | TTL+cap enforced | Disk | Log sys |
| J2 | WER configurator | Dev | Spec | Elevated set HKLM LocalDumps to D:\dumps; DumpCount=5; DumpType=1 | Reg APIs | J1 | 0.75d | Dumps produced; size cap via cleaner | HKLM | WER tool |
| K1 | Validation runner (/bench) | Dev/QA | Corpus | OCR folder; JSONL + HTML report | CLI+HTML | E3,F1 | 1d | ≥95% agree; p95 ≤ 600 ms | Corpus bias | Bench tool |
| K2 | Corpus prep | QA | Screens | ≥500 picks labeled (DPI/themes) | Scripts | — | 1.5d | Set ready | Time | Corpus |
| L1 | Packaging (portable signed) | Dev | Keys | OV code sign EXE; version info | Sign tool | All | 0.75d | SmartScreen OK | AV | Installer |
| L2 | Backups | Dev | J1 | Weekly auto backup (keep 3); on‑demand backup/restore | Zip | J1 | 0.5d | Backups exist | Space | Backup |
| M1 | Release hardening | Dev/QA | K1 | Tune thresholds; finalize tokens; docs | — | K1 | 1d | Gates met | Regressions | RC build |

---

# 6) Dependencies & Critical Path

**Narrative**
- Overlay & capture must be stable before OCR/resolution can be validated. Composite builder feeds OCR; resolver depends on dictionaries; recommendation requires resolver and tiers; overlay UI depends on recommendation.

**Ordered list (by ID)**
1. B1 → B2 → B3 (capture stack)  
2. C1 → C2 (calibration + composite)  
3. D2 → D3 → D4 (OCR pipeline)  
4. E1 → E2 → E3 (data + resolver)  
5. F1 → F2 (recommendation + ledger)  
6. G1 → G2 → H1 → H2 (UI + settings)  
7. I1 → I2 → J1 → J2 (diagnostics & ops)  
8. K1 → M1 (validation & release)

---

# 7) Risk Register

| Risk | Likelihood | Impact | Mitigation | Trigger | Owner |
|---|---|---|---|---|---|
| Azure p95 spikes | Med | High | West US region, strict timeouts, v3.2 fallback, local OCR on fail | Probe p95 > 800 ms; runtime spikes | Dev |
| OCR near‑name collisions | Med | Med | Tight edit distance, class/arena filters, min OCR conf; (portrait tie‑break later) | Ambiguity > 1% | Dev |
| Overlay captured | Low | High | WDA_EXCLUDEFROMCAPTURE + Mirror View (0 pixels × 3) gate | Mirror View FAIL | Dev |
| D:\ missing/low space | Med | Med | LocalAppData session fallback; 1 GB guard; 200 MB logs cap; 50 MB dumps cap | Banner appears | Ops |
| WER HKLM blocked | Low | Med | Elevated configurator; MiniDump fallback | Apply fails | Ops |
| DPI/scaling drift | Med | Med | PMv2; drift>3 px prompt recalibration | Drift detected | Dev |
| HDR contrast loss | Low | Med | Tone‑map HDR→sRGB | HDR detected | Dev |
| Free‑tier 429 | Med | Med | Backoff + retry + fallback; banner | 429 count rises | Dev |

---

# 8) QA & Validation Plan

**Unit/Integration**
- Composite tiling (gutters/separator/offset table)  
- OCR clients (timeouts, retries, fallback cadence)  
- Resolver thresholds & filters  
- Tier engine scoring & rationale  
- Ledger SoT rules

**UI/UX**
- Present‑on‑change; 0 presents when idle  
- Status strip fields; latency badge thresholds  
- DPI moves (100/125/150%) and legibility  
- Safe Mode contrast checker (AA pass)

**Mirror View Acceptance**
- PASS = 0 overlay pixels × 3 frames; capture blocked until PASS.

**Corpus Validation**
- ≥500 picks (enUS × 100/125/150% × theme mix)  
- Metrics: false‑ID, ambiguity, unrecognized, p50/p95  
- **Go/No‑Go:** ≥95% agreement; p95 ≤ 600 ms; ambiguity ≤ 1.0%; false‑ID ≤ 0.5%.

---

# 9) Metrics & Instrumentation

- Per‑pick spans: capture, compose, Azure, resolve, total.
- Status strip shows moving p50/p95 over last 20 picks + last pick RTT.
- Reliability: 429/timeouts; Mirror View pass rate; D:\ missing events.
- Storage: size on disk; low‑space throttles.
- Validation report (HTML) for accuracy/latency.

---

# 10) Rollout & Rollback

- Packaging: portable, code‑signed EXE (OV).  
- First‑run: v4 probe (3 tiny calls) → show region health; Mirror View gate; calibration wizard.  
- Auto‑backup: weekly ZIP of config/calibration/dict/tiers to `D:\…\backups` (keep last 3).  
- Rollback: keep previous binary; allow manual revert from Settings.

---

# 11) Operations & Maintenance

**Runbook**
- Rotate Azure key via Settings (CredWrite overwrite).  
- Change endpoint/region; restart app.  
- Apply/remove WER dumps (elevated).  
- Migrate Data Root; restart app; verify paths.  
- Run Mirror View; run WPR Light/Full; export logs bundle.

**SLOs**
- p95 ≤ 600 ms (sunny path); Mirror View pass 100% after setup; < 2 rate‑limit banners/day on average.

**Support**
- All local logs; export “log bundle” (ZIP) with last settings snapshot and optional Diagnostic ROI images.

---

# 12) Communications Plan

- Change log: `logs/release-notes.md`.  
- Semantic versioning: e.g., `0.1.0-mvp`.  
- In‑app: OCR page shows API path (v4/v3.2) + region + measured p50/p95; banners for fallback.

---

# 13) Open Questions & Decisions Log

**Decisions (locked)**
- Win11; D3D11 FL 11_0; PMv2 DPI; layered+transparent overlay; DComp flip‑model; present‑on‑change; grayscale AA on translucent, ClearType on opaque strip; Mirror View 0‑pixel gate; WGC capture; tone‑map HDR for OCR only; Data Root D:\ (fallback to LocalAppData session); WER LocalDumps to D:\dumps; Azure v4 sync primary with v3.2 async fallback; Local OCR off by default; HSJSON `/v1/latest/` pin; SymSpell thresholds; tier+synergy decisioning; status strip fields; hotkeys; Help panel; Safe/Light/Dark themes; contrast guard in Safe.

**Open (to finalize during build)**
- Local OCR packaging (bundle vs on‑demand).  
- Exact hex/α values for all theme tokens after UI contrast tests.  
- WPR `.wprp` contents (Light vs Full) and ETW GUID/event payloads.

---

## Appendix A — Swapchain Descriptor (final)
- DXGI_FORMAT: BGRA8 UNORM  
- AlphaMode: Premultiplied  
- SwapEffect: FlipDiscard  
- BufferCount: 2 (auto‑escalate to 3 on ETW drop event)  
- Scaling: Stretch  
- SampleDesc: 1×  
- Present: compositor vsync; idle heartbeat: none (optional 1 Hz in Diagnostics)

## Appendix B — Azure Request/Response SOP (summary)
- v4 sync: send composite PNG/JPEG; HTTP timeout 600 ms (overall 1.2 s); parse lines/words + boxes; map via offsets.  
- v3.2 async: poll every ~150 ms up to ≤ 2 s; same mapping; on failure: banner + optional local OCR fallback.

## Appendix C — Hotkeys (MVP)
- Toggle overlay; Scan current pick; Retry OCR; Show My Crops; Copy last OCR JSON; Toggle Safe Mode; F1 opens Help.

## Appendix D — Paths
- Data Root: `D:\cursor bots\HSGalaxy\{config,calibration,dict,tiers,logs,dumps}`  
- Backups: `D:\cursor bots\HSGalaxy\backups\` (keep last 3)

## Appendix E — State Machine
- {Idle, WindowFound, Active, Occluded, Minimized, RateLimited, Offline}; each transition raises ETW + shows single‑line banner.

## Appendix F — Theme Tokens (initial baseline; verify in UI QA)
- **Light**: Status bg `#F9FAFB`, fg `#111827`; Chip bg `#AD111827` (68% α) / fg `#FFFFFF`; Separators subtle (`#33000000`).  
- **Dark**: Status bg `#111827`, fg `#F9FAFB`; Chip bg `#D1F3F4F6` (82% α) / fg `#111827`.  
- **Safe** (opaque): Status bg `#0B0F17`, fg `#F9FAFB`; Chip bg `#E6111827` (90% α) or `#EBF3F4F6` (92% α) / fg matched to pass AA.

## Appendix G — Validation Report (HTML) schema (excerpt)
- Summary: p50/p95 latency, false‑ID, ambiguity, unrecognized.  
- Per‑card accuracy table.  
- Per‑pick row: OCR text, resolved card, decision, reasons, timings.

