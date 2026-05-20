# Startup First Frame Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent the main window from exposing an unpainted shell during startup.

**Architecture:** Keep the AntdUI `MainWindow` and existing UI composition intact. Hide the window during the first-frame preparation phase, complete layout and bounds normalization, then restore redraw and reveal the window after the message queue reaches the shown state.

**Tech Stack:** .NET Framework 4.0, WinForms, AntdUI v2.3.0.

---

## File Structure

- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - Add startup visibility state.
  - Start the form fully transparent before the first show.
  - Restore redraw and opacity after the first shown message cycle.
- Test: `AiCleanVolume.sln`
  - Build the solution with `dotnet build`.
  - Manually launch the desktop app and verify the blank shell is no longer visible.

## Task 1: Startup Visibility Control

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Capture current startup code**

Read the current `OnLoad`, `OnHandleCreated`, `OnShown`, and redraw helper methods.

Expected: identify the existing `startupRedrawSuspended` and `startupRedrawCompleted` flow.

- [ ] **Step 2: Add startup opacity state**

Add a field to remember the normal opacity and mark startup reveal completion.

Expected: startup state does not interfere with minimized restore redraw.

- [ ] **Step 3: Hide first paint**

Set `Opacity = 0D` during initialization before the handle is shown.

Expected: system and DWM can create the window, but the user does not see the blank shell.

- [ ] **Step 4: Reveal after shown cycle**

In `OnShown`, defer the reveal with `BeginInvoke`, apply bounds if needed, resume redraw, update once, and restore opacity to the previous value.

Expected: the first visible frame already contains the completed AntdUI layout.

- [ ] **Step 5: Guard repeated calls**

Make reveal idempotent so it cannot run twice during normal startup or later window state changes.

Expected: opacity remains correct after startup.

## Task 2: Verification

**Files:**
- Test: `AiCleanVolume.sln`

- [ ] **Step 1: Build solution**

Run: `dotnet build AiCleanVolume.sln -c Debug`

Expected: build succeeds.

- [ ] **Step 2: Review working tree**

Run: `git status --short`

Expected: only the plan document and `MainWindow.cs` are changed by this task, plus any pre-existing unrelated user changes.

- [ ] **Step 3: Manual startup check**

Launch the desktop executable.

Expected: the app appears only after the first useful frame is ready; the blank white shell is not visible.

## Commit

No commit is performed in this plan. Commit only after the user explicitly requests it, using the repository commit format.
