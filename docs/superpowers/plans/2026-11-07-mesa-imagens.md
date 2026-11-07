# Mesa Virtual Rodada 5 (imagens em camadas) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The GM and the players place extra images over a scene's map, managed in an "Imagens" tab like Photoshop layers. Every image has a lock; images always stay below tokens.

**Architecture:**
- **Data:** `SceneState.Images` (JSON in the scene).
- **Services:** `SceneService` gets the GM and player mutations, with the existing `MutateAsync`/`PlayerMutateAsync` and per-seat views (`SceneViews`).
- **Board:** a new Pixi layer right after the background, with its own gestures (move, resize from a corner, rotate).
- **Live drag:** a hub `imageDrag` preview, like `tokenDrag`.

**Tech Stack:** .NET 10 / EF Core (no schema change: the state is JSON), React + Pixi 8, Vitest, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-07-mesa-imagens-design.md`

## Global Constraints
- **Strings:** bilingual: `t('pt','en')` and `L.T("pt","en")`.
- **Builds and tests:** only after all tasks ([[build-test-at-end]]). dotnet comes from `~/.dotnet`.
- **Limits:**
  - 200 images per scene; 10 per seat per scene;
  - size 0.25–200 cells; opacity 0.1–1; rotation 0–360;
  - name up to 60 characters;
  - player upload up to 2 MB (PNG, JPEG, WebP or GIF, sniffed).
- **Errors:** 403 when the viewer may not edit; 404 for missing or hidden-to-player; 400 for limits.
- **Commits:** one per task, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus
1. **A hidden image** must not reach a player in any form: view, URL, the hub `imageDrag`, or an order index revealing it. Test: `Hidden_image_not_in_player_view`.
2. **A player** must never edit another seat's image or a GM-locked image, including via `order` and `delete`. Tests: `Player_cannot_touch_others_image`, `Gm_lock_blocks_owner`.
3. **Order renumbering** after delete and moves stays 0..n−1 with no duplicates. Test: `Order_renumbers_after_moves_and_delete`.
4. **Scenes saved before this round** (no `images`) load. Test: `Old_scene_json_loads`.
5. **A resize drag** on a rotated image keeps the opposite corner fixed. Vitest: `resizeFromCorner keeps the opposite corner on a rotated image`.

---

### Task 1: Backend — data, views, mutations, routes, player upload
**Files:** `Application/Table/SceneState.cs` (`SceneImage`), `SceneContracts.cs` (`SceneImageView`, `ImageInput`, `ImageOrderRequest`, `SceneView.Images`), `SceneViews.cs`, `SceneService.cs`, `MediaService.cs` (`UploadAsync(..., maxBytes)`), `Api/Controllers/TableController.cs`, `TablePlayController.cs`. Test: `Integration/TableImagesApiTests.cs`.

**Interfaces:**
- `SceneImage { Id, MediaId, Name, X, Y, Width, Height, Rotation, Opacity, Order, Locked, LockedByGm, Hidden, OwnerSeatId }`.
- `ImageInput(Guid? MediaId, string? Name, double X, double Y, double? Width, double? Height, double Rotation = 0, double Opacity = 1, bool Locked = false, bool Hidden = false)`.
- `ImageOrderRequest(string? Above)`.
- `SceneImageView(Id, Url, Name, X, Y, Width, Height, Rotation, Opacity, Order, Locked, LockedByGm, Hidden, OwnerSeatId, Editable)`.
- **GM:** `AddImageAsync(id, input)`, `UpdateImageAsync(id, imageId, input)`, `OrderImageAsync(id, imageId, req)`, `RemoveImageAsync(id, imageId)`.
- **Player:** `Player…(seat, …)` versions of the same four.
- **Routes:**
  - GM: `/api/table/scenes/{id}/images[/{imageId}[/order]]`.
  - Player: `/api/table/play/images[/{imageId}[/order]]`, plus `POST /api/table/play/media`.

**Steps:**
- [ ] Tests:
  - `Gm_adds_moves_resizes_and_orders_images`
  - `Player_adds_and_edits_own_image`
  - `Player_cannot_touch_others_image` (403)
  - `Gm_lock_blocks_owner` (403)
  - `Hidden_image_not_in_player_view`
  - `Order_renumbers_after_moves_and_delete`
  - `Seat_limit_is_10`
  - `Player_upload_over_2mb_is_400`
  - `Old_scene_json_loads`
- [ ] Implement:
  - Order is renumbered by `Order` on every change.
  - With no size given, the image fits 4×4 (square when the image's proportion is unknown to the server; the client sends the size from the texture).
  - Locks: the GM sets `LockedByGm`; a player's lock leaves it false.
  - A player can't unlock when `LockedByGm`.
- [ ] Commit.

### Task 2: Pure helpers (`frontend/src/features/table/images.ts`)
**Interfaces:**
- `fitInitial(texW, texH, maxCells = 4): { width, height }`
- `resizeFromCorner(r: ImgRect, corner: Corner, point: Point, keepRatio: boolean): ImgRect`
- `rotationFor(center, point, free): number` (handle above; 15° steps)
- `snapHalf(n)`
- `orderedImages(images)`: top first
- `aboveForMove(images, id, dir: 'up' | 'down'): string | null | undefined` (undefined = can't move)

`ImgRect = { x, y, width, height, rotation }`; `Corner = 'nw' | 'ne' | 'se' | 'sw'`.

- [ ] Vitest: fit, resize (unrotated, rotated 90° keeps the opposite corner, keepRatio, clamp 0.25), rotation snap, snapHalf, ordering and aboveForMove. Then implement. Commit.

### Task 3: Board — image layer, selection, handles, live preview
**Files:** `board/BoardController.ts`, `board/TableBoard.tsx`, `useTableHub.ts` (`imageDrag` send and receive), `Api/Hubs/TableHub.cs` (`DragImage`).

**Interfaces:**
- `BoardData.images: SceneImageView[]` (from `scene.images`), `selectedImage: string | null`.
- `BoardCallbacks.onImageSelect(id | null)`, `onImageDrag(id, rect)`, `onImageChange(id, rect)`.
- `BoardHandle.showImageDrag(id, rect)`.
- Hub: `DragImage(sceneId, imageId, x, y, w, h, rot)`. The server forwards to the others only when the image is visible to them; hidden ones go to the GM only.

**Steps:**
- [ ] `imageLayer` sits after `backgroundLayer`. Sprites with anchor 0.5; rotation and alpha; the GM sees hidden ones at 0.5.
- [ ] Clicking an image (select tool) selects it and clears the token selection. A token click clears the image selection on the page.
- [ ] Editable and selected: 4 corner squares and a rotate knob, drawn in `overlay`. New gestures `image-move | image-resize | image-rotate` use the helpers from Task 2. Each `onMove` re-places the sprite and calls `onImageDrag` (the page throttles it through the hub). `onUp` calls `onImageChange`.
- [ ] Commit.

### Task 4: "Imagens" tab and page wiring
**Files:** `frontend/src/api/table.ts`, `tableTypes.ts`, new `features/table/ImagesTab.tsx`, `GmTablePage.tsx`, `PlayerTablePage.tsx`, `Table.module.css`.

- [ ] API client:
  - GM: `addImage`, `updateImage`, `orderImage`, `removeImage`.
  - Player: the same, plus `uploadMedia` (2 MB, through `preparePortrait` for players and `prepareImage(MAP_LIMITS)` for the GM).
- [ ] `ImagesTab`:
  - add button and the "n/10" counter;
  - list, top first: thumbnail, name (rename on double click), eye (GM), lock (the GM's lock styled apart), opacity slider (saved on release), ↑↓, delete with confirmation, owner tag;
  - clicking a row selects the image;
  - non-editable rows are disabled with the hint "Imagem de {dono}".
- [ ] Pages:
  - new tab "Imagens";
  - selection state `selectedImage`;
  - callbacks to the board;
  - a dropped image is placed at the center of the view (`board.centerCell()` exposed by the handle);
  - the size comes from the texture via `fitInitial`.
- [ ] Commit.

### Task 5: Final verification
- [ ] Build, both test suites, typecheck and lint.
- [ ] Edge script: player A adds an image; B sees it and can't edit; the GM resizes, locks and hides it; it disappears for the players.
- [ ] Final review (fresh reviewer) and fixes.
- [ ] Exe rebuilt.
- [ ] Merge only when asked.
