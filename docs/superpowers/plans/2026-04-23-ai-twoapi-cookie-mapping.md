# AI 2API Cookie Mapping Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a second AI access mode that sends model-specific `X-Provider-Cookie` headers for ai2api while preserving the existing standard API mode.

**Architecture:** Extend persisted AI settings with an access-mode string and a structured model-cookie list. Reuse the existing OpenAI-compatible request path, switching only authentication headers based on the selected access mode.

**Tech Stack:** .NET Framework 4.0, WinForms, AntdUI, Newtonsoft.Json, RestSharp 105.2.3.

---

## Chunk 1: Settings Model

### Task 1: Persist 2API configuration

**Files:**
- Modify: `src/AiCleanVolume.Core/Models/ApplicationSettings.cs`

- [ ] Add `AccessMode` to `AiSettings` with default `standard_api`.
- [ ] Add `ModelCookieMappings` to `AiSettings`.
- [ ] Add `AiModelCookieMapping` with `Model` and `Cookie`.
- [ ] Normalize missing or invalid values in `EnsureDefaults`.

## Chunk 2: Settings UI

### Task 2: Add access mode and cookie mapping inputs

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] Add `接入类型` select with `标准 API` and `2API`.
- [ ] Add `模型 Cookie` multiline input.
- [ ] Load `AccessMode` and mappings into controls.
- [ ] Save selected access mode and parsed mappings.
- [ ] Show `API Key` as disabled in `2API` mode without clearing it.

## Chunk 3: Request Authentication

### Task 3: Switch request headers by access mode

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.cs`

- [ ] Keep current `Authorization` behavior for `standard_api`.
- [ ] Match current model to a cookie mapping for `two_api`.
- [ ] Send `X-Provider-Cookie` only for `two_api`.
- [ ] Fall back to local advisor when required 2API cookie is missing.

## Chunk 4: Validation

### Task 4: Build and smoke-check

**Files:**
- Modify: `README.md`

- [ ] Document both AI access modes.
- [ ] Run `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`.
- [ ] Fix compile errors without changing unrelated behavior.
