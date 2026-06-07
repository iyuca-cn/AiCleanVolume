# Scan Progress Performance Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep scan progress visually smooth while reducing repeated UI writes and AntdUI redraw pressure.

**Architecture:** Leave scanning services unchanged. Throttle and de-duplicate only the scan page presentation updates in `MainWindow.Scan.cs`, with timing constants in `ScanPageText.cs` and runtime state in `ScanPageState.cs`. Elapsed time and progress value refresh at 100ms cadence, progress animation completes in 60ms, and repeated expensive state writes are skipped.

**Tech Stack:** C# WinForms, AntdUI, .NET Framework desktop project.

---

## File Structure

- Modify `src/AiCleanVolume.Desktop/Presentation/Features/Scan/ScanPageText.cs`: expose 100ms progress and elapsed refresh intervals.
- Modify `src/AiCleanVolume.Desktop/Presentation/Features/Scan/ScanPageState.cs`: store last rendered scan progress state for de-duplication.
- Modify `src/AiCleanVolume.Desktop/Presentation/MainWindow/MainWindow.Scan.cs`: throttle active progress refresh, split immediate state transitions from active animation updates, and avoid repeated `Loading` writes.

## Chunk 1: Progress Update Throttling

### Task 1: Add Timing Constants And Render State

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Presentation/Features/Scan/ScanPageText.cs`
- Modify: `src/AiCleanVolume.Desktop/Presentation/Features/Scan/ScanPageState.cs`

- [x] Add constants for active progress interval and elapsed text interval.
- [x] Add state fields for last progress text, value, loading flag, state, elapsed text, and last elapsed timestamp.

### Task 2: De-Duplicate Progress Writes

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Presentation/MainWindow/MainWindow.Scan.cs`

- [x] Add a reset method for rendered progress state.
- [x] Update scan start to reset state and start the timer with the new interval.
- [x] Change active refresh so it updates value smoothly, updates elapsed text every 100ms, and avoids repeated state/text/loading writes.
- [x] Keep completion and failure updates immediate.

### Task 3: Verify

**Files:**
- Build: `AiCleanVolume.sln`

- [x] Run Debug build with `dotnet build AiCleanVolume.sln -c Debug`.
- [x] Inspect `git diff --check`.
