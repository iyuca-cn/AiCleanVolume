# Minimize Restore Repaint Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent the main window client area from flashing or staying black after restoring from minimized state.

**Architecture:** Keep the existing AntdUI window and normal-bounds correction. Remove the full-window redraw suspension from the minimized restore path, paint erase-background requests with the app page color, briefly hide the restore frame with opacity while the queued layout/repaint completes, then reveal the already-painted window.

**Tech Stack:** .NET Framework 4.0, WinForms, AntdUI v2.3.0.

---

## File Structure

- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - Stop using `WM_SETREDRAW` to freeze the whole main window during minimized restore.
  - Keep the queued bounds correction.
  - Paint `WM_ERASEBKGND` with the app background color.
  - Temporarily hide the minimized restore frame with opacity and reveal it after repaint.
  - Add a focused restore repaint helper that performs layout, invalidates children, and updates immediately.
- Test: `AiCleanVolume.sln`
  - Build the solution.
  - Launch the app, minimize it, restore it, and verify the client area paints normally.

## Task 1: Restore Repaint Flow

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Review current restore hooks**

Read `OnSizeChanged`, `WndProc`, `IsRestoreFromMinimizedSizeMessage`, `SuspendWindowRestoreRedraw`, `QueueWindowRestoreCompletion`, and `ResumeWindowRestoreRedraw`.

Expected: confirm minimized restore currently suspends redraw before `base.WndProc`.

- [ ] **Step 2: Remove restore redraw suspension**

Change `WndProc` so minimized restore only queues completion after `base.WndProc`.

Expected: the native restore frame can paint normally instead of exposing a frozen black client area.

- [ ] **Step 3: Paint erase-background requests**

Handle `WM_ERASEBKGND` in `WndProc` and fill the client area with `PageBackground`.

Expected: system background erasure never exposes a black client area.

- [ ] **Step 4: Hide restore frame until repaint**

When a restore from minimized `WM_SIZE` arrives, set `Opacity = 0D` before the base window processing and restore it after queued repaint.

Expected: any transient native restore frame is not visible to the user.

- [ ] **Step 5: Force full repaint after bounds correction**

After queued bounds correction, call a helper that runs layout and immediate repaint.

Expected: restored window content repaints in the same queued turn.

- [ ] **Step 6: Remove unused restore redraw state**

Delete fields and methods that only supported restore redraw suspension.

Expected: startup redraw suspension remains intact and separate.

## Task 2: Verification

**Files:**
- Test: `AiCleanVolume.sln`

- [ ] **Step 1: Build solution**

Run: `dotnet build AiCleanVolume.sln -c Debug`

Expected: build succeeds.

- [ ] **Step 2: Launch smoke test**

Start `src/AiCleanVolume.Desktop/bin/Debug/net40/AiCleanVolume.exe`.

Expected: process starts and main window title is `AI智能清盘`.

- [ ] **Step 3: Minimize and restore manually**

Use the window minimize button or taskbar restore action.

Expected: client area is the full AntdUI interface, not a black rectangle.

## Commit

No commit is performed in this plan. Commit only after the user explicitly requests it, using the repository commit format.
