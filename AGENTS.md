# Project Working Rules

- Treat this directory as the only project root.
- Keep source code and scripts under `src/` and `scripts/`.
- Keep temporary build and verification files under `work/`.
- Keep release packages and user-facing deliverables under `outputs/`.
- Source code and releases are maintained together at `deepfry666/SijiPVToolbox-Releases`; generated packages stay ignored locally and are uploaded as Release assets, not committed to Git.
- Never publish engineering DWG/DXF files, private screenshots, local settings, or the former private Git history. Public snapshot boundaries and developer-machine migration are documented in `OPEN_SOURCE.md`.
- Do not place project artifacts under `Documents/Codex` or other temporary roots.
- Never overwrite the original DWG template in the project root. Test with a copy under `work/` or `outputs/`.
- Develop every new feature for the Four Seasons PV Toolbox as an independent module first, with its own testable output. Integrate it into the main toolbox only after the standalone feature has been fully tested and is stable.
- After each completed project change, create a dedicated Git commit and push it to the configured remote repository. Keep unrelated changes out of the commit, and report any commit or push failure explicitly.

## Issue Tracking Rules

- Treat `ISSUES.md` in the repository root as the single source of truth for project problems across tasks and conversations.
- Read `ISSUES.md` at the start of every project task before changing project files.
- Record every distinct user-reported problem, every problem the user asks an agent to organize, and every actionable problem an agent confirms within the current task scope as an unchecked Markdown item (`- [ ]`) before attempting the fix.
- Give each item a stable sequential ID. Use one checkbox per independently verifiable problem, and update an existing item instead of creating a duplicate.
- Keep an item unchecked while its fix is incomplete, unverified, blocked, or failing verification, and note the current blocker or failed check in the item.
- Only after the fix is implemented and the relevant verification passes, change that same item to checked (`- [x]`) and add the completion date plus concise verification evidence. Never delete completed items.
- In each final task summary, report which issue IDs were added, updated, or checked off and link to `ISSUES.md`.

## Interaction Design Rules

- Whenever users must enter custom engineering geometry such as position, direction, edge distance, spacing, offset, or component relationships, provide a corresponding diagram in the same UI context.
- The diagram labels must use the same names or symbols as the input fields, clearly identify the reference object and local direction, and distinguish centerline dimensions from outline clearances.
- Show the diagram when the related custom mode or inputs are active. Keep it compact, readable at supported DPI scales, and covered by UI layout tests.

## Cursor Cloud specific instructions

Cloud Agents for this repository run on Linux. The product still targets AutoCAD 2020 Win64 R23.1 on Windows. Do not install or fake an AutoCAD SDK, and do not overwrite the DWG templates in the repository root. Test drawings belong under `work/`.

The Cloud Agent install provides PowerShell 7, Mono, Xvfb on display `:99`, and a `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` shim. `powershell` and `pwsh` on `PATH` are that shim. Use them for every regression script so child processes keep the compiler drive, `DISPLAY`, and the legacy `cmd.exe` stand-in.

Daily pure-code check from the repository root:

```bash
bash scripts/cloud-agent-start.sh
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
```

That command compiles and runs the array-recognition, ballast, rail, update, support-detail, and WinForms layout tests. It then stops in `tests/PublishAtomicityTests.ps1` because publish and archive scripts compare Windows backslash path prefixes. `scripts/build.ps1` and `scripts/Build-ReleaseCandidate.ps1` also stop until a real AutoCAD 2020 R23.1 install is available at `D:\cad\AutoCAD 2020`. Those checks stay on a Windows machine. Candidate output still belongs under `work/`, and accepted packages under `outputs/`.
