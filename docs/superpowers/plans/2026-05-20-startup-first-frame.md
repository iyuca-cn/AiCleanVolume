# Startup First Frame Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent the main window from exposing an unpainted shell during startup.

**Architecture:** Keep the AntdUI `MainWindow` and existing UI composition intact. Keep construction light so the form and taskbar button can appear quickly, then restore the first frame and bind heavier settings/drive data after `OnShown`. The design is recorded in `docs/plans/2026-05-20-startup-first-frame-design.md`.

**Tech Stack:** .NET Framework 4.0, WinForms, AntdUI v2.3.0.

---

## File Structure

- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - Add startup reveal state.
  - Defer settings, AI profile cards, drive enumeration, and drive summary binding until after the first show.
  - Avoid form opacity/layered-window startup hiding so minimized restore repaint remains stable.
  - Paint erase-background requests with the app page background.
- Test: `AiCleanVolume.sln`
  - Build the solution with `dotnet build`.
  - Manually launch the desktop app and verify the blank shell is no longer visible.

## Task 1: Startup Visibility Control

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [x] **Step 1: Capture current startup code**

Read the current `OnLoad`, `OnHandleCreated`, `OnShown`, and redraw helper methods.

Expected: identify the existing `startupRedrawSuspended` and `startupRedrawCompleted` flow.

- [x] **Step 2: Add startup reveal state**

Add a field to guard the startup reveal callback.

Expected: startup state does not interfere with minimized restore redraw.

- [x] **Step 3: Keep constructor light**

Move settings binding, AI profile card population, drive enumeration, and drive summary loading out of the constructor.

Expected: system and DWM can create the window and taskbar button sooner.

- [x] **Step 4: Bind startup data after shown**

In `OnShown`, queue first-frame redraw restoration and then queue startup UI binding.

Expected: the first visible frame is stable, and heavier UI data appears after the taskbar button is already available.

- [x] **Step 6: Preserve minimized restore repaint**

Avoid form opacity changes and paint `WM_ERASEBKGND` with the app background color.

Expected: restoring from minimized state does not flash a black client area.

- [x] **Step 5: Guard repeated calls**

Make reveal idempotent so it cannot run twice during normal startup or later window state changes.

Expected: startup reveal and startup binding each run once.

- [x] **Step 7: Suppress startup binding event storms**

Use `loadingStartupUi` to prevent placeholder and real startup binding assignments from triggering repeated drive summary, prompt, and preset recalculation.

Expected: startup data binding updates the UI once and does not reintroduce constructor-time blocking.

## Task 2: Verification

**Files:**
- Test: `AiCleanVolume.sln`

- [x] **Step 1: Build solution**

Run: `dotnet build AiCleanVolume.sln -c Debug`

Expected: build succeeds.

- [x] **Step 2: Review working tree**

Run: `git status --short`

Expected: only the plan document and `MainWindow.cs` are changed by this task, plus any pre-existing unrelated user changes.

- [x] **Step 3: Manual startup check**

Launch the desktop executable.

Expected: the taskbar button appears quickly, the app starts with stable placeholder values, and startup data fills in after the first show.

## Commit

No commit is performed in this plan. Commit only after the user explicitly requests it, using the repository commit format.
