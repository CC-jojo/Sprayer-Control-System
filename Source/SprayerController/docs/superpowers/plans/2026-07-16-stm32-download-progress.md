# STM32 Download Progress Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace STM32CubeProgrammer's garbled block-character progress output in CLion with fixed-width ASCII progress bars while preserving download, verification, reset, and exit-code behavior.

**Architecture:** A focused PowerShell wrapper invokes the existing STM32CubeProgrammer CLI, captures each output line, suppresses only progress-rendering artifacts, and emits deterministic ASCII completion bars. The existing CLion `STM32 Download` configuration invokes PowerShell with this wrapper after building the firmware target.

**Tech Stack:** Windows PowerShell 5.1, STM32CubeProgrammer CLI 2.19.0, CLion CMake run configuration XML.

## Global Constraints

- Keep the CLion one-click `STM32 Download` workflow.
- Do not modify firmware source code, CMake compilation, ELF content, or programmer connection/write/verify/reset arguments.
- Preserve the Programmer process exit code.
- Preserve device information, success output, and error output.

---

### Task 1: ASCII progress output wrapper and CLion integration

**Files:**
- Create: `tools/stm32-download.ps1`
- Create: `tools/tests/test-stm32-download-output.ps1`
- Modify: `.idea/runConfigurations/STM32_Download.xml`
- Modify: `.idea/workspace.xml`

**Interfaces:**
- Consumes: `stm32-download.ps1 -Programmer <exe> -Firmware <elf>`.
- Produces: Programmer output with progress sections rendered as `Write progress: [####################] 100%` or `Read progress:  [####################] 100%`, and the Programmer exit code.

- [ ] **Step 1: Write the failing output-filter test**

Create a test that invokes the wrapper's `Format-ProgrammerOutput` function with simulated `Download in Progress:` and `Read progress:` sections containing `北` and `圹`, then asserts that neither character remains, both ASCII bars exist, and `Download verified successfully` remains.

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-stm32-download-output.ps1
```

Expected: nonzero exit because `tools/stm32-download.ps1` does not exist.

- [ ] **Step 3: Implement the minimal wrapper**

Create `tools/stm32-download.ps1` with mandatory `Programmer` and `Firmware` parameters. Add `Format-ProgrammerOutput` to recognize `Download in Progress:` and `Read progress:`, suppress their following rendering lines through the percentage line, emit one 20-character `#` bar at 100%, preserve all other lines, invoke the Programmer with `-c port=SWD -w $Firmware -v -rst`, and exit with `$LASTEXITCODE`.

- [ ] **Step 4: Run the filter test and verify GREEN**

Run the command from Step 2.

Expected: `PASS: STM32 download output uses ASCII progress bars` and exit code 0.

- [ ] **Step 5: Point CLion at the wrapper**

Set `RUN_PATH` to `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` and set `PROGRAM_PARAMS` to:

```text
-NoProfile -ExecutionPolicy Bypass -File "$PROJECT_DIR$/tools/stm32-download.ps1" -Programmer "E:\APP\STM32CubeCLT\STM32CubeCLT_1.18.0\STM32CubeProgrammer\bin\STM32_Programmer_CLI.exe" -Firmware "$PROJECT_DIR$/build/Debug/SprayerController.elf"
```

Apply this to the project run-configuration file and the active workspace copy.

- [ ] **Step 6: Verify CLion one-click download**

Restart CLion, select `STM32 Download`, and click the green Run button.

Expected: build succeeds; output contains ASCII `Write progress` and `Read progress` bars, `Download verified successfully`, `MCU Reset`, `Software reset is performed`, and process exit code 0; output contains no `北` or `圹`.

- [ ] **Step 7: Commit if version control is available**

The current project is not a Git repository, so record `NO_GIT_REPOSITORY` instead of creating a commit.
