# AGENTS.md - Project Rules & Guidelines for Antigravity

This document outlines the mandatory operating rules, language consistency standards, documentation workflow, and version control policies for AI assistants and agents working on the **UNICUT** codebase.

---

## 1. Language Consistency Rules

### 1.1. User Interface (UI) - Always English
* **All UI strings MUST be written in English**:
  - Button texts, labels, and icons.
  - Tooltips, placeholders, and status badges.
  - Modal titles, dialog prompts, and messagebox alerts.
  - Error messages and logging outputs.
* Never mix Indonesian into application UI components or code strings.

### 1.2. Chat Responses - Always Bahasa Indonesia
* **All chat communications and explanations to the user MUST be delivered in Bahasa Indonesia**.
* Keep responses polite, structured, concise, and easy to understand.
* Technical terms (e.g., *clipboard*, *snapshot*, *pill capsule*, *drop shadow*, *DPI scaling*, *free-roam*) may remain in English where standard.

---

## 2. Planning & Documentation Workflow

### 2.1. Implementation Plan (`implementation_plan.md`)
* When handling non-trivial features, refactors, or architectural upgrades:
  - Document the proposed architecture, component breakdown, and todo checklist in `implementation_plan.md`.
  - Wait for explicit user confirmation before executing when required by Planning Mode.

### 2.2. Walkthrough (`walkthrough.md`)
* Upon completing features, bug fixes, or optimizations:
  - Update `walkthrough.md` with:
    1. **Summary of Changes**: What was added, fixed, or modified.
    2. **Verification Results**: Compilation logs (e.g., `build.bat` output).
    3. **How to Test**: Clear, numbered step-by-step instructions for the user to manually test the changes.

### 2.3. Antigravity Build & Verification Discipline
* Always compile and verify the build using `cmd /c build.bat` before concluding tasks.
* Stop any running `UNICUT` instances before compilation if locked (`Stop-Process -Name UNICUT -Force`).
* Ensure zero compilation warnings/errors before reporting completion.

---

## 3. Git & Version Control Rules

### 3.1. Local Commits
* **Always commit changes to the local Git repository** after completing and verifying tasks.
* Use clear, conventional commit messages (e.g., `feat: ...`, `fix: ...`, `refactor: ...`, `docs: ...`).

### 3.2. Remote Push Policy - User Authorization Required
* **NEVER push (`git push`) to the remote repository automatically**.
* Only execute `git push` when the user **explicitly issues a command or grants direct approval** to push to remote.

---

## 4. Git Release Rules & Guidelines

### 4.1. Release Format & Content
* **Clean & Professional Formatting**:
  - Keep release notes concise, clear, and structured.
  - **Do NOT use emojis or decorative icons** in release titles or release descriptions.
  - Structure release notes with standard markdown sections:
    - Summary
    - Key Features & Changes
    - Bug Fixes & Improvements
    - Download & Installation
* Keep release notes in English.

### 4.2. Mandatory Asset Upload
* **Always attach the compiled standalone executable (`UNICUT.exe`)** as a binary release asset.
* Ensure the binary is freshly compiled and verified via `cmd /c build.bat` before creating the release.
* Follow semantic versioning tags (e.g., `v1.0.0`, `v1.1.0`).
