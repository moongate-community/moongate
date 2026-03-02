# UI Item Templates Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add an Item Templates admin page in `ui/` with server pagination and authenticated image previews.

**Architecture:** Extend existing React Router admin shell with a new `ItemTemplatesPage`. Consume backend endpoints `/api/item-templates` and `/api/item-templates/by-item-id/{itemId}/image`. Since image endpoints are protected when JWT is enabled, load previews via authenticated fetch (blob URL) instead of plain `<img src>`.

**Tech Stack:** React 19, TypeScript, HeroUI, Vite.

---

### Task 1: Add typed API support for binary responses

**Files:**
- Modify: `ui/src/api/client.ts`

**Steps:**
1. Add a generic helper for raw fetch with existing auth headers.
2. Add `api.getBlob(path)` for image binary payloads.
3. Keep existing JSON/text behavior unchanged.

### Task 2: Implement Item Templates page

**Files:**
- Create: `ui/src/pages/ItemTemplatesPage.tsx`

**Steps:**
1. Define UI types for paged response and item rows.
2. Implement paged loading state (`page`, `pageSize`, `totalCount`).
3. Build table with columns: preview, id, name, category, itemId.
4. Add previous/next and page-size controls.
5. Add preview component that fetches image blob via `api.getBlob`.

### Task 3: Wire navigation and routes

**Files:**
- Modify: `ui/src/router.tsx`
- Modify: `ui/src/components/Sidebar.tsx`

**Steps:**
1. Register `/item-templates` route.
2. Add sidebar entry for Item Templates.

### Task 4: Verify frontend build

**Files:**
- None

**Steps:**
1. Run `npm --prefix ui run lint`.
2. Run `npm --prefix ui run build`.
3. Fix any type/lint issues and rerun until green.
