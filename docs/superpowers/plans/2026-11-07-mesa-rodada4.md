# Mesa Virtual Rodada 4 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the 17 requests from the players, plus weapon quality, the equipment split and YouTube music, in packages B → A → C → F → D → E → G on `feature/mesa-rodada4`.

**Architecture:**
- **Backend:** ASP.NET Core with Application and Domain layers.
  - Character sheets and encounters are stored as JSON.
  - Table changes reach clients through SignalR (`TableHub`, `ITableNotifier`, `IEncounterObserver`).
- **Frontend:** React, React Query and a PixiJS board.
- **Approach:** new behavior extends the existing services. The pending attack reuses `AttackResolver` with "fixed dice": each decision fixes one more die in the stored `AttackRequest` until the attack can be applied by the existing path.

**Tech Stack:** .NET 10 (xUnit, WebApplicationFactory), EF Core (Postgres + Sqlite migrations), React 19 + TypeScript + Vitest, PixiJS 8, three.js (dice), SignalR, YouTube IFrame Player API.

**Spec:** `docs/superpowers/specs/2026-11-07-mesa-rodada4-design.md` (read it with this plan; section ids like B1/A3/D1 refer to it).

## Global Constraints

- **Language:** every new UI string is bilingual. The frontend uses `t('pt', 'en')` (data strings: `tv()`); the backend uses `L.T("pt", "en")`.
- **Rule citations:** every rule effect cites its book page in code comments or messages ("CP:R p.146").
- **NPC HP:** NPC remaining HP (`Through`, `Hp`) is never sent to players. Damage dealt and the dice are sent.
- **dotnet:** `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet ...` (Git Bash). Ask the user before running `dotnet build`, `dotnet test` or `dotnet ef`.
- **Backend tests:** `dotnet test backend/tests/CyberpunkDb.Tests --filter <Name>`. **Frontend tests:** `npm --prefix frontend test -- <file>`; typecheck with `npm --prefix frontend run typecheck`.
- **Migrations:** every schema change gets **both** migrations (`docs/DESENVOLVIMENTO.md:155-157`):
  - Postgres: `--project src/CyberpunkDb.Infrastructure --output-dir Persistence/Migrations`
  - Sqlite: `--project src/CyberpunkDb.Migrations.Sqlite --output-dir Migrations -- --provider Sqlite`
- **Old data:** new JSON fields on the sheet and encounter state have defaults, so old sheets and fights keep loading.
- **Player surface:** the players' port only reaches what `PlayerSurfaceGuard` allows. Any new player route goes under `/api/table/play` and carries the `X-Seat` header (`?seat=` on images).
- **Commits:** one commit per task on `feature/mesa-rodada4`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Never merge, push or release without the user asking.

## Review Focus

1. **An abandoned pending attack** (the target's player closed the tab) must not lock the fight forever. The GM can answer any pending step or cancel it. Test in Task 14: `Gm_can_answer_defense_for_pc_target`.
2. **Sheet changes made by the encounter itself** (ammo, damage) must not broadcast twice or loop. Test in Task 1: `Encounter_attack_broadcasts_once`.
3. **Seat-scoped character API path tricks** must never reach another sheet: another id, `..` segments, uppercase or encoded paths, a missing header. Tests in Task 19: `Guard_refuses_other_character_paths`.
4. **Sheets and fights saved before this round** (no `Split`, `Quality`, `PortraitFocus`, `Pending`, `RoundFlags`, `Thrown`) must load and behave as Standard / no pending. Tests in Tasks 6, 7, 14: `Old_sheet_json_loads`, `Old_encounter_json_loads`.
5. **Attacking with a jammed or thrown weapon, or undoing after a pending attack**, must give a clear 4xx message and never corrupt state. Tests in Tasks 13, 14, 18.

---

## Package B — sync and bugs

### Task 1: Character changes reach the table (B1)

**Files:**
- Create: `backend/src/CyberpunkDb.Application/Characters/ICharacterObserver.cs`
- Create: `backend/src/CyberpunkDb.Application/Table/CharacterBroadcast.cs`
- Modify: `backend/src/CyberpunkDb.Application/Characters/CharacterService.cs` (`MutateAsync` ~l.575, `RestoreAsync` l.135, `SetPortraitAsync`/`DeletePortraitAsync`)
- Modify: `backend/src/CyberpunkDb.Application/Encounters/EncounterService.cs` (wrap its own mutation in the quiet scope)
- Modify: `backend/src/CyberpunkDb.Application/Table/ITableNotifier.cs`, `backend/src/CyberpunkDb.Api/Table/HubTableNotifier.cs`, `backend/src/CyberpunkDb.Application/DependencyInjection.cs`
- Modify: `frontend/src/features/table/useTableHub.ts`, `GmTablePage.tsx`, `PlayerTablePage.tsx`
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TableLiveSyncApiTests.cs`

**Interfaces:**
- Produces:
  - `interface ICharacterObserver { Task ChangedAsync(int characterId, CancellationToken ct); }`
  - `CharacterBroadcast : ICharacterObserver` with `static IDisposable Quiet()`. `Quiet` sets an `AsyncLocal<bool>` that makes `ChangedAsync` a no-op inside the scope.
  - `ITableNotifier.PartyAsync(int? characterId, CancellationToken ct)`: hub event `party` with payload `{ characterId }` sent to the GM group and the `players` group.
  - Client: `useTableHub` handler `party?: (p: { characterId: number | null }) => void`.

- [ ] **Step 1: Write the failing tests** in `TableLiveSyncApiTests`:
  - `Equipping_armor_pushes_encounter_to_seats`: a PC is in the active fight on the live scene and a seat hub client is connected (reuse the hub-client helper from `TableCombatApiTests`). `POST /api/characters/{id}/equip` with armor on the Body. Assert the seat receives an `encounter` event whose own fighter's `spBody` equals the armor's SP.
  - `Encounter_attack_broadcasts_once`: the GM attacks a PC through `/api/table/scenes/{id}/attack`. Assert the seat gets exactly **1** `encounter` event within 1 s.
  - `Sheet_change_without_fight_sends_party_only`: with no fight, `POST .../luck`. Assert one `party` event and no `encounter` event.
- [ ] **Step 2: Run them, expect FAIL** (no events). `dotnet test backend/tests/CyberpunkDb.Tests --filter TableLiveSyncApiTests`
- [ ] **Step 3: Implement.**
  - `CharacterService` takes `ICharacterObserver` and calls `observer.ChangedAsync(id, ct)` after a successful save in `MutateAsync`, `RestoreAsync` and the portrait methods.
  - `CharacterBroadcast` resolves `SceneService`, `EncounterService` and `IEncounterObserver` lazily through `IServiceProvider`, to break the DI cycle `CharacterService → observer → EncounterService → CharacterService`. It does:
    - `scenes.ActiveAsync` → if the encounter is active and its queue has the PC → `observer.ChangedAsync(detail)`;
    - always `notifier.PartyAsync(characterId)`.
  - `EncounterService` wraps the body of its `MutateAsync` and of `UndoAttackAsync` in `using var _ = CharacterBroadcast.Quiet();`, because it already broadcasts.
  - Remove the hand-made refresh in `TablePartyService.AdjustOwnArmorAsync` (now redundant).
  - Register it in `DependencyInjection.cs`.
- [ ] **Step 4: Frontend.**
  - `party` handler: GM invalidates `['table','party']`. Player invalidates `playerParty`, plus `playerCharacter` and `playerSheet` when `characterId === seat.characterId`.
- [ ] **Step 5: Run the tests, expect PASS;** run the whole backend suite (no regressions).
- [ ] **Step 6: Commit** `Mesa: mudanças na ficha chegam à mesa ao vivo (combate e grupo)`

### Task 2: HP bars for the other players (B2)

**Files:** found by the investigation. Likely `frontend/src/features/table/board/BoardController.ts:426`, `tokens.ts`, `PlayerTablePage.tsx`, `strip.ts`. Test: `frontend/src/features/table/tokens.test.ts` or the backend test of the root cause.

**Interfaces:**
- Produces, if the cause is the token lookup: `fighterOfToken(token: TokenView, fighters: Map<string, TokenFighter>, queue: readonly { id: string; characterId: number | null }[]): TokenFighter | undefined` in `tokens.ts`, used by `drawToken` instead of `data.fighters.get(token.combatantId)`.

- [ ] **Step 1: Reproduce** (superpowers:systematic-debugging). Start the dev backend and frontend. Write `scratchpad/pw/hp-sync.mjs` (playwright-core + Edge, as in previous rounds):
  - one GM page and two seat pages;
  - seat A attacks seat B's PC on its turn;
  - read the target's HP bar width and the strip's `hp/maxHp` text on seat **B's** page and on a third observer seat.
  - Record which surfaces stay stale.
- [ ] **Step 2: Trace one layer at a time** (hub event received? cache updated? `BoardData.fighters` changed? `drawToken` found the fighter?) with console logging in the script. Write the root cause down in the commit message.
- [ ] **Step 3: Write the failing test** for the root cause. For the token-lookup suspicion: `fighterOfToken finds a PC fighter by characterId when the token has no combatantId` → expect `hp` to equal the fighter's.
- [ ] **Step 4: Fix the cause; run the test (PASS) and the script again** (all pages update within 1 s).
- [ ] **Step 5: Commit** `Mesa: barras de HP atualizam para todos os jogadores (<causa>)`

### Task 3: Party cards for every seat (B3)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Table/TablePartyService.cs`
- Modify: `backend/src/CyberpunkDb.Api/Table/TablePresence.cs` (read online seat ids), `backend/src/CyberpunkDb.Api/Hubs/TableHub.cs` (connect/disconnect → `notifier.PartyAsync(null)`)
- Modify: `frontend/src/api/tableTypes.ts` (`PartyMember`), `frontend/src/features/table/strip.ts`, `SceneStrip.tsx`, `Table.module.css`
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TablePartyApiTests.cs`, `frontend/src/features/table/strip.test.ts`

**Interfaces:**
- Produces:
  - `PartyMemberDto(int CharacterId, string Name, int Hp, int MaxHp, WoundState WoundState, int SpHead, int? MaxSpHead, int SpBody, int? MaxSpBody, int SeatId, bool Online, bool InScene, string? ImageUrl)`.
  - `TablePartyService` needs online seat ids. Inject `Func<IReadOnlySet<int>>` or a new Application interface `ITablePresence { IReadOnlySet<int> OnlineSeats(); }` implemented by `TablePresence`.
  - TS `StripEntry` gains `online: boolean | null` and `offScene: boolean`. Off-scene entries use `tokenId: \`seat:${seatId}\``.
  - `buildStrip(tokens, party, fighters, order?: readonly string[])`. The `order` parameter is used by Task 9.

- [ ] **Step 1: Failing backend tests:**
  - `Party_lists_seat_without_token_as_not_in_scene` (`inScene == false`);
  - `Party_skips_revoked_seats`;
  - `Party_marks_connected_seat_online` (connect a hub client with the seat → `online == true`);
  - `Player_party_includes_other_seats_off_scene`.
- [ ] **Step 2: Failing vitest tests:**
  - `buildStrip appends off-scene members after token cards` (`offScene: true`, last);
  - `off-scene member has no combatantId`.
- [ ] **Step 3: Implement.**
  - **Members:** all non-revoked seats with a `CharacterId` (deduped by character), with `InScene` = a token of that character is visible to the requester. `ImageUrl` is the portrait served by the table media, the same URL format tokens use for PC portraits (see `TokenImages.cs`).
  - **Off-scene cards:** a faded class `stripCardOff` and a "fora da cena / off scene" tag. `onPick` and the aim ignore them (no `combatantId`).
  - **Connection dot** on every PC card.
- [ ] **Step 4: Run both suites, expect PASS.**
- [ ] **Step 5: Commit** `Mesa: cartões de todos os jogadores na faixa, com conectado e fora da cena`

---

## Package A — sheet

### Task 4: Portrait framing (A1)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Characters/CharacterSheet.cs` (`CharacterIdentity` + `Normalize`)
- Modify: `backend/src/CyberpunkDb.Application/Table/SceneViews.cs` (or wherever `TokenView` is built), `TokenImages.cs`
- Create: `frontend/src/lib/portraitFocus.ts`, `frontend/src/lib/portraitFocus.test.ts`
- Modify: `frontend/src/api/characterTypes.ts`, `tableTypes.ts`, `frontend/src/features/characters/CharacterUi.tsx` (`Portrait`), `CharactersPage.tsx:101`, `Characters.module.css`, `frontend/src/features/table/board/BoardController.ts` (sprite in `drawToken`)
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Characters/CharacterValidatorTests.cs` (or a new `PortraitFocusTests.cs`)

**Interfaces:**
- Produces:
  - `public sealed record PortraitFocus(double X = 50, double Y = 50, double Zoom = 1);` with `CharacterIdentity.PortraitFocus { get; init; }` (nullable). `Normalize()` clamps X and Y to 0..100 and Zoom to 1..3.
  - `TokenView.ImageFocus: PortraitFocus?`, set when the token image is the PC portrait.
  - TS:
    - `focusStyle(f: PortraitFocus | null): CSSProperties`, which returns `objectPosition` and `transform: scale(z)` with `transformOrigin` at the focus;
    - `spriteFit(texW, texH, boxW, boxH, f): { scale: number; x: number; y: number }`.

- [ ] **Step 1: Failing tests:**
  - backend `Portrait_focus_is_clamped`: X=150, Y=-5, Zoom=9 become 100, 0 and 3;
  - vitest `spriteFit covers the box and moves toward the focus`: a 200×100 texture in a 50×50 box with focus X=0 → scale 0.5, and the left edge aligned with the box's left edge;
  - vitest `focusStyle defaults to center`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement** the record, the clamp, the TokenView field and the two TS functions.
  - In `Portrait` add the button "Enquadrar / Frame". While framing: pointer drag updates X and Y (delta in px / box size × 100 / zoom), a range input sets the zoom, and "Salvar" saves through the sheet editor (`editor.update` on `identity.portraitFocus`, then flush). "Cancelar" restores.
  - `BoardController` uses `spriteFit` for the token sprite.
- [ ] **Step 4: Run, expect PASS; typecheck.**
- [ ] **Step 5: Commit** `Ficha: enquadrar o retrato (arrastar e zoom), também no token`

### Task 5: Wallet block (A2)

**Files:** Modify `frontend/src/features/characters/SheetView.tsx` (`IdentityColumn` l.61-101) and `Characters.module.css`.

**Interfaces:** reads `detail.sheet.wallet.balance` and the IP from the report the `ProgressionPanel` already uses. Grep `ProgressionPanel` for the exact field names (available/earned) and reuse them.

- [ ] **Step 1: Implement** the `WalletBlock` component under `<Portrait>`, with two big numbers side by side: "Saldo / Balance" `formatEb(...)` and "IP" available, plus the total earned in small type. Remove the `Saldo` row from the `kv` list.
- [ ] **Step 2: Typecheck and run vitest** (no regressions).
- [ ] **Step 3: Check in the browser** that the block shows on /characters/{id}/ficha at phone and desktop width.
- [ ] **Step 4: Commit** `Ficha: saldo e IP juntos no topo da aba Personagem`

### Task 6: Role split and skill bonuses (A3)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Characters/CharacterSheet.cs` (`CharacterRole.Split`)
- Modify: `backend/src/CyberpunkDb.Application/Characters/Rules/character-rules.json` (Nomad, Tech and Solo `ability`), `CharacterRules.cs` (model)
- Modify: `backend/src/CyberpunkDb.Application/Characters/Calculation/CharacterCalculator.cs` (`ComputeSkills` l.386)
- Modify: `backend/src/CyberpunkDb.Application/Characters/Validation/CharacterValidator.cs`, `CharacterService.cs` (new command), `backend/src/CyberpunkDb.Api/Controllers/CharactersController.cs`
- Create: `frontend/src/features/characters/RoleSplitDialog.tsx`
- Modify: `frontend/src/features/characters/SheetView.tsx` ("Distribuir" next to `RoleAbilityButton`), `characterTypes.ts`
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Characters/CharacterCalculatorTests.cs`, `CharacterValidatorTests.cs`, `Integration/CharactersApiTests.cs`

**Interfaces:**
- Produces:
  - `CharacterRole.Split: Dictionary<string,int>` (case-insensitive, default empty).
  - JSON: `ability.split: { keys: [{ id, label, labelPt }], max: "rank" | "2xrank", perKeyMax: "rank" | null, allowed?: { [key]: number[] } }`. Example: Solo `fumbleRecovery: [0,4]`.
  - JSON: `ability.skillBonuses: [{ skills: string[], from: "rank" | "split:<key>", label }]`.
  - Command `POST /api/characters/{id}/role-split { version, role, split }` → `CharacterService.SetRoleSplitAsync(int id, RoleSplitRequest r, ct)`, with `record RoleSplitRequest(int Version, string Role, Dictionary<string,int> Split)`.
  - Rule values:
    - **Nomad Moto:** `from: "rank"` on `drive-land-vehicle`, `pilot-air-vehicle`, `pilot-sea-vehicle`, `land-vehicle-tech`, `air-vehicle-tech`, `sea-vehicle-tech`. Use the exact skill ids from `character-rules.json`; grep `"id":` of those skills.
    - **Tech:** `split` keys `field`, `upgrade`, `fabrication`, `invention`, with `max: "2xrank"` and `perKeyMax: "rank"`. Bonus `from: "split:field"` on `basic-tech`, `cybertech`, `electronics-security-tech`, `weaponstech` and the three vehicle techs.
    - **Solo:** `split` keys `damageDeflection`, `fumbleRecovery`, `initiativeReaction`, `precisionAttack`, `spotWeakness`, `threatDetection`, with `max: "rank"` and `allowed.fumbleRecovery = [0,4]`. Bonus `from: "split:threatDetection"` on `perception`.
  - Label format of the `ValuePart`: `"Moto (Rank 4)"`, `"Field Expertise 2"`, `"Threat Detection 1"`.

- [ ] **Step 1: Failing tests:**
  - `Nomad_rank_4_adds_4_to_drive_land_vehicle` (the skill total rises by 4 and a part labeled "Moto (Rank 4)" exists);
  - `Tech_field_2_adds_2_to_weaponstech`;
  - `Solo_threat_detection_adds_to_perception`;
  - `Tech_split_over_twice_rank_is_refused` (Rank 2, split 3+2 → 400 on `role-split`);
  - `Solo_fumble_recovery_must_be_0_or_4`;
  - `Old_sheet_json_loads` (sheet JSON without `split` → no bonus, validator OK).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement** the model, the JSON, the bonus in `ComputeSkills` (after the modifiers, as `ValuePart`s), the validation and the command.
  - `RoleSplitDialog`: one `NumField` per key, a used/available counter and, for Solo, the note "em combate, redistribuir custa uma Ação (CP:R p.146)".
- [ ] **Step 4: Run, expect PASS; typecheck.**
- [ ] **Step 5: Commit** `Ficha: bônus de Moto (Nomad) e Field Expertise (Tech) nas perícias, distribuição de Rank do Tech e do Solo`

### Task 7: Weapon quality field (A4)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Characters/CharacterSheet.cs` (`WeaponState` l.221)
- Modify: `Characters/Operations/InventoryOperations.cs` (purchase/add default, new `SetQuality`), `CharacterService.cs`, `CharactersController.cs`
- Modify: `Characters/Calculation/CharacterReport.cs` (`WeaponLine.Quality`)
- Modify: `frontend/src/api/characterTypes.ts`, `EquipmentView.tsx`, `SheetView.tsx` (weapons panel tag), `frontend/src/features/table/PlayerPanels.tsx` (tag)
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Characters/LifepathAndEconomyTests.cs` (or a new `WeaponQualityTests.cs`)

**Interfaces:**
- Produces:
  - `enum WeaponQuality { Standard, Poor, Excellent }` (string enum JSON);
  - `WeaponState.Quality: WeaponQuality = Standard`;
  - `WeaponLine.Quality`;
  - `POST /api/characters/{id}/weapon-quality { version, instanceId, quality }`;
  - `static WeaponQuality InventoryOperations.DefaultQuality(CatalogItem item)`: Poor if the name or description contains "Poor Quality" (case-insensitive), Excellent if "Excellent Quality", else Standard.
  - The +1 attack effect is wired in Task 17, not here.

- [ ] **Step 1: Failing tests:**
  - `Quality_is_saved_per_instance` (two instances of the same item, one set to Excellent);
  - `Default_quality_from_catalog_text` (a Toggle's Temple Poor Quality weapon → Poor; a plain pistol → Standard);
  - `Old_sheet_json_loads` (no `quality` → Standard).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** Set the default when the item is added (purchase, starting package, manual add). The "Qualidade" `Select` appears only on weapon rows. The tag shows when the quality isn't Standard.
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Ficha: qualidade da arma (Poor/Standard/Excellent) por exemplar`

### Task 8: Equipment grouped by type (A5)

**Files:**
- Create: `frontend/src/features/characters/inventoryGroups.ts`, `inventoryGroups.test.ts`
- Modify: `frontend/src/features/characters/EquipmentView.tsx` (`InventoryPanels` l.132)

**Interfaces:**
- Produces:
  - `type InventoryGroup = 'weapons' | 'armor' | 'misc' | 'clothing'`;
  - `groupInventory(items: InventoryLine[]): Record<InventoryGroup, InventoryLine[]>`.
- Rules, in order:
  1. `fashion` → clothing.
  2. `Weapon | WeaponAttachment | Ammunition` → weapons.
  3. `Armor`, or `sp != null`, or `shieldHp != null` → armor.
  4. Everything else → misc.

- [ ] **Step 1: Failing tests** `groupInventory` puts:
  - a weapon, an ammunition and an attachment in weapons;
  - armor and a shield in armor;
  - clothing and Fashionware (`fashion: true`, even with category Armor) in clothing;
  - Gear and Drug in misc.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** Four `SheetPanel`s titled "Armas / Weapons (n)", "Armaduras / Armor (n)", "Itens diversos / Misc items (n)", "Roupas / Clothing (n)", each with an empty message and its own `InventoryTable`. Weapon rows show the quality `Select` from Task 7.
- [ ] **Step 4: Run, expect PASS; typecheck.**
- [ ] **Step 5: Commit** `Ficha: equipamento separado em armas, armaduras, itens diversos e roupas`

---

## Package C — combat on the table

### Task 9: Strip in Initiative order + GM initiative editing (C1, C2)

**Files:**
- Modify: `frontend/src/features/table/strip.ts` (the `order` parameter from Task 3), `GmTablePage.tsx`, `PlayerTablePage.tsx`
- Modify: `frontend/src/features/table/GmPanels.tsx` (`CombatTab` queue rows ~l.428-450)
- Test: `frontend/src/features/table/strip.test.ts`

**Interfaces:**
- Consumes: `buildStrip(..., order)` from Task 3; the existing routes `POST /api/encounters/{id}/combatants/{cid}/initiative` (`SetInitiativeRequest`) and the move-in-queue route. Grep `MoveInQueueRequest` in `EncountersController` for the path.

- [ ] **Step 1: Failing tests:**
  - `buildStrip orders fighters by queue order, non-fighters after, off-scene last`;
  - `buildStrip without order keeps token order`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement the sort.**
  - The pages pass `order = active fight ? queue.map(f => f.id) : undefined`.
  - In `CombatTab`: each row gets a `NumField` for the initiative (saved on blur through `command.mutate({ path: \`combatants/${id}/initiative\`, body: { initiative } })`) and ↑/↓ `IconButton`s that move the row through the move-in-queue route (↑ = before the previous row; ↓ = before the row after the next, or null at the bottom).
- [ ] **Step 4: Run, expect PASS; check in the browser.**
- [ ] **Step 5: Commit** `Mesa: faixa na ordem da iniciativa e iniciativa editável pelo mestre`

### Task 10: Player passes the turn (C3)

**Files:**
- Modify: `backend/src/CyberpunkDb.Api/Controllers/TablePlayController.cs`, `backend/src/CyberpunkDb.Application/Table/TableAttackService.cs` (or a new `TableTurnService.cs`)
- Modify: `frontend/src/api/table.ts` (`playerApi.endTurn`), `PlayerPanels.tsx` (`InitiativePanel`), `PlayerTablePage.tsx`
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TableTurnApiTests.cs`

**Interfaces:**
- Produces:
  - `POST /api/table/play/end-turn` → `Task<PlayerEncounterDto>` through `TableTurnService.EndTurnAsync(TableSeat seat, CancellationToken ct)`, which calls the existing next-turn method of `EncounterService` (l.238) with the current version.
  - 409 when there is no fight, it's not the seat's turn or (after Task 14) a pending attack exists.

- [ ] **Step 1: Failing tests:**
  - `Player_ends_own_turn` (the active id moves to the next fighter);
  - `Player_cannot_end_someone_elses_turn` (409);
  - `No_fight_is_409`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement** the route and the "Passar o turno / End turn" button in `InitiativePanel` and in the "Sua vez!" badge row, enabled only when `active?.own`.
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Mesa: jogador passa o próprio turno`

### Task 11: Character-tab attack uses the aim flow + attacks public to all (C4, C5)

**Files:**
- Modify: `frontend/src/features/table/PlayerPanels.tsx` (weapons block l.104-136), `PlayerTablePage.tsx` (pass `onAim` and `turnBlocked`)
- Modify: `backend/src/CyberpunkDb.Application/Table/TableAttacks.cs` (`ForSeat`)
- Modify: `frontend/src/features/table/dice3d/dice.ts` (`throwOf` for attacks, if it filters NPC dice)
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TableQuickAttackApiTests.cs`, `frontend/src/features/table/dice3d/dice.test.ts`

**Interfaces:**
- Produces:
  - `CharacterPanel` prop `onAim?: (weapon: WeaponView, mode: AttackMode) => void` and `turnBlocked: string | null`.
  - `ForSeat` keeps `Damage`, `Dv`, `DefenseRoll` and `DamageDice` for every target and nulls only `Through` for non-PC targets.

- [ ] **Step 1: Failing tests:**
  - backend `Seat_sees_damage_and_dice_on_npc_target_but_not_through` (damage ≠ null, damage dice non-empty, through == null);
  - vitest `throwOf includes damage dice of an NPC target`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.**
  - In a fight: "Ataque" calls `onAim(w, 'Single')` and "Autofire" calls `onAim(w, 'Autofire')`. The page runs `quick.start({ ...startAim(own.id, own.name, w), mode })`. Both are disabled with the `turnBlocked` hint outside the player's turn.
  - Outside a fight: the chat roll stays as today.
  - Hide the "resolvidos pelo mestre" note in a fight.
- [ ] **Step 4: Run, expect PASS; check in the browser** that the button opens the `AimBar` exactly like the chip.
- [ ] **Step 5: Commit** `Mesa: botão Ataque da aba Personagem mira como os cartões; acerto e dano visíveis a todos`

### Task 11b: Reload button (C6)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Encounters/EncounterService.cs` (new `ReloadAsync`), `EncounterContracts.cs` (`ReloadCombatantRequest`)
- Modify: `backend/src/CyberpunkDb.Api/Controllers/EncountersController.cs`, `TablePlayController.cs`, `backend/src/CyberpunkDb.Application/Table/TableTurnService.cs` (from Task 10: `ReloadAsync(seat, weapon)`)
- Modify: `frontend/src/api/table.ts` (`playerApi.reload`), `frontend/src/api/encounters.ts`, `frontend/src/features/table/PlayerPanels.tsx`, `SceneStrip.tsx` (a reload icon next to each magazine chip), `PlayerTablePage.tsx`, `GmTablePage.tsx`, `frontend/src/features/combat/EncounterView.tsx` (queue weapon chips)
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TableReloadApiTests.cs`

**Interfaces:**
- Produces:
  - `record ReloadCombatantRequest(int Version, string Weapon)`;
  - `EncounterService.ReloadAsync(int id, string combatantId, ReloadCombatantRequest r, CancellationToken ct)`:
    - **PC:** calls `characters.ReloadAsync(charId, new ReloadRequest(sheetVersion, weapon))`;
    - **NPC and generic:** `Ammo[weapon] = Magazine`;
    - logs `"{name} recarrega {weapon} (Ação)"`;
    - refuses a weapon without a magazine or with a full one (400).
  - `POST /api/encounters/{id}/combatants/{cid}/reload` and `POST /api/table/play/reload { weapon }`.
    - In a fight (the PC is in the active scene's fight): only on the PC's turn, otherwise 409.
    - Outside a fight: `characters.ReloadAsync` directly.
  - TS `canReload(w: WeaponView): string | null`: null when `magazine != null && (rounds ?? 0) < magazine`, otherwise the reason ("Pente cheio / Full magazine", "Sem pente / No magazine").

- [ ] **Step 1: Failing tests:**
  - `Pc_reloads_from_inventory_in_fight` (fire 1 round, reload → rounds = magazine, the inventory ammo drops);
  - `Npc_reload_refills_magazine`;
  - `Player_reload_out_of_turn_is_409`;
  - `Full_magazine_is_400`;
  - `Player_reload_outside_fight_works`;
  - vitest `canReload`.
- [ ] **Step 2: Implement** the routes and the "Recarregar / Reload" buttons (player: own weapons, disabled out of turn in a fight; GM: any fighter's chips and /combat queue chips).
- [ ] **Step 3: Commit** `Mesa e combate: botão de recarregar (PC pelo inventário, NPC enche o pente)`

---

## Package F — injury icons

### Task 12: Critical Injury icons around the token (F)

**Files:**
- Create: `frontend/src/features/table/board/injuryIcons.ts`, `injuryIcons.test.ts`
- Modify: `frontend/src/features/table/tokens.ts` (`TokenStatus`: split `conditions` and `injuries`), `board/BoardController.ts` (`drawToken` l.480-493)

**Interfaces:**
- Produces:
  - `type InjuryIcon = 'leg' | 'arm' | 'hand' | 'eye' | 'head' | 'torso' | 'foreign' | 'lung' | 'spine' | 'burn' | 'ear' | 'generic'`;
  - `injuryIcon(name: string): InjuryIcon`;
  - `drawInjuryIcon(g: Graphics, icon: InjuryIcon, r: number): void`;
  - `arcPositions(count: number, radius: number): { x: number; y: number }[]`: max 8 positions, from the top along both sides.
  - `TokenStatus` becomes `{ ratio, exact, color, conditions: string[], injuries: string[] }`.

- [ ] **Step 1: Failing tests:**
  - every Critical Injury name of the body and head tables maps to a non-`generic` icon. Get the names from `backend/src/CyberpunkDb.Application/Encounters/Rules/combat-rules.json` or the critical-injury table file; grep `"Broken Leg"`. Copy the list into the test.
  - an unknown name → `generic`;
  - `arcPositions(3, 10)` returns 3 points at distance 10 from the center, the first at the top (x≈0, y≈-10);
  - `arcPositions(12, 10).length === 8`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.**
  - Each injury is a dark circle with a red stroke and the icon in white `Graphics`. When there are more than 8, the 8th slot shows `+N`.
  - `pointerover` on an icon shows a `Text` label with the name; `pointerout` removes it.
  - Conditions stay as text badges.
- [ ] **Step 4: Run, expect PASS; check in the browser** with a fighter with 3 injuries.
- [ ] **Step 5: Commit** `Mesa: ícones de lesão crítica em volta do token`

---

## Package D — attack rules

### Task 13: Natural 10/1, Poor jam and unjam (D2, A4 effect)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Encounters/Combat/AttackResolver.cs`, `EncounterContracts.cs` (`AttackRequest.AttackExtra`, `AttackResult.AttackExtra`, `PlannedAttack.AttackExtra`, `Jammed`)
- Modify: `Encounters/EncounterState.cs` (`Combatant.Jammed: List<string>`), `Characters/CharacterSheet.cs` (`WeaponState.Jammed: bool`)
- Modify: `Encounters/Combat/FighterViews.cs` (`WeaponView.Quality`, `WeaponView.Jammed`), `EncounterService.cs` (persist the jam; new `UnjamAsync`), `backend/src/CyberpunkDb.Api/Controllers/EncountersController.cs`, `TablePlayController.cs`
- Modify: `frontend/src/features/table/aim.ts` (`cannotFire`: jammed), `SceneStrip.tsx`, `PlayerPanels.tsx` ("Destravar" button), `frontend/src/features/combat/AttackPanel.tsx`
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Encounters/CombatTests.cs`, `Integration/TableQuickAttackApiTests.cs`

**Interfaces:**
- Produces:
  - `AttackRequest.AttackExtra: int?` (a typed extra d10);
  - `AttackResult.AttackExtra: int?`, and `TableAttack.AttackExtra: int?` (the dice tray shows it);
  - `WeaponView.Quality: WeaponQuality` and `WeaponView.Jammed: bool`;
  - `POST /api/encounters/{id}/combatants/{cid}/unjam { version, weapon }` and `POST /api/table/play/unjam { weapon }` (own turn only);
  - log line "{name} destrava {weapon} (Ação)".
- Rule (CP:R p.130):
  - d10 = 10 → total += extra;
  - d10 = 1 → total -= extra, unless the attacker has `Split["fumbleRecovery"] >= 4` (read by Task 16; here, read the split through `FighterView.CombatAwareness`, added in this task as `IReadOnlyDictionary<string,int>`, empty for NPCs).
- Poor (CP:R p.344, confirm the page): a natural 1 on a Poor weapon jams it, after the attack resolves (the shot still resolves with the reduced total), even with Fumble Recovery.
- Attacking with a jammed weapon → 400 "Travada: gaste uma Ação para destravar / Jammed: spend an Action to unjam".

- [ ] **Step 1: Failing tests:**
  - `Natural_10_adds_extra_d10` (typed AttackRoll 10, AttackExtra 4 → total base+14);
  - `Natural_1_subtracts_extra_d10`;
  - `Fumble_recovery_ignores_natural_1`;
  - `Poor_weapon_jams_on_natural_1` (a PC's sheet `WeaponState.Jammed` becomes true; an NPC's combatant `Jammed` contains the key);
  - `Jammed_weapon_cannot_attack` (400);
  - `Unjam_clears_and_logs`;
  - `Player_unjam_only_on_own_turn` (409).
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.** The `CheckD10` helper returns `(roll, extra)`. The jam is saved through `characters` (PC) or `Replace` (NPC) inside the attack mutation.
- [ ] **Step 4: Run, expect PASS** (whole encounter suite).
- [ ] **Step 5: Commit** `Combate: 10/1 natural no ataque (CP:R p.130), arma Poor trava no 1 e destravar`

### Task 14: Pending attack core (D1 backend)

**Files:**
- Create: `backend/src/CyberpunkDb.Application/Encounters/PendingAttack.cs`
- Modify: `Encounters/EncounterState.cs` (`Pending`), `EncounterContracts.cs` (`AttackRequest.DodgeTargets`, `CritDice`; the detail DTO's `Pending`), `Combat/AttackResolver.cs` (per-target dodge), `EncounterService.cs`
- Modify: `backend/src/CyberpunkDb.Application/Table/TableAttackService.cs`, `PlayerEncounters.cs`
- Modify: `backend/src/CyberpunkDb.Api/Controllers/EncountersController.cs`, `TablePlayController.cs`
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Encounters/PendingAttackTests.cs`, `Integration/TablePendingAttackApiTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public enum PendingStep { Defense, CriticalInjury }
  public sealed record PendingAttack(int Seq, AttackRequest Request, string TargetId, PendingStep Step);
  // EncounterState.Pending: PendingAttack?   (JSON default null)
  // AttackRequest additions: List<string>? DodgeTargets = null, List<int>? CritDice = null   (CritDice: the 2d6; CritRoll = sum)
  public sealed record PendingView(int Seq, string AttackerId, string AttackerName, string Weapon, string TargetId, string TargetName,
      PendingStep Step, int? RangeDv, int? Evasion);
  // EncounterDetailDto.Pending: PendingView?;  PlayerEncounterDto.Pending: PendingView? plus bool PendingMine
  ```
  - `EncounterService.AttackAsync(int id, AttackRequest r, CancellationToken ct, bool interactive = false)`.
  - `EncounterService.AnswerDefenseAsync(int id, int version, string targetId, bool dodge, ct)`.
  - `EncounterService.RollPendingCriticalAsync(int id, int version, string targetId, ct)`.
  - `EncounterService.CancelPendingAsync(int id, EncounterVersionRequest r, ct)`.
  - GM routes:
    - `POST /api/encounters/{id}/pending/defense { version, targetId, dodge }`;
    - `POST /api/encounters/{id}/pending/critical { version, targetId }`;
    - `POST /api/encounters/{id}/pending/cancel { version }`.
  - Player routes: `POST /api/table/play/defense { dodge }` and `POST /api/table/play/critical`.
  - A player may answer only when they own the step:
    - **Defense:** the target's `CharacterId == seat.CharacterId`;
    - **CriticalInjury:** the attacker's `CharacterId == seat.CharacterId`.
- Algorithm ("fixed dice"):
  1. **Interactive attack.** `TableAttackService` passes `interactive: true`. `AttackPanel` passes `true` only when no defense/crit dice are typed.
     - Fix `AttackRoll` and `AttackExtra` first: roll them if they are null.
     - For each target, in order: if it is a ranged non-area, non-melee target that `CanDodge(DodgeRef)` and is not yet in `DodgeTargets` or the decided set → save `Pending(seq, request, target, Defense)` and return. **Nothing is applied and no ammo is spent.**
  2. **AnswerDefense.** Record the decision (add the target to `DodgeTargets` if dodging; set `DefenseRolls[target]` = d10 if dodging), then continue step 1 for the next target.
  3. **When every defense is decided.**
     - Fix `DamageDice` (roll if null).
     - Call `resolver.Resolve` with the fixed request to preview the hits.
     - If any hit is `Critical` and `CritDice` is null → `Pending(..., CriticalInjury)` on that target and return.
  4. **RollPendingCritical.** `CritDice` = 2d6 from the dice roller (`CritRoll` = sum), then apply.
  5. **Apply** means the existing body of `AttackAsync` (ammo, hits, undo snapshot, `LastAttack`), run with the fixed request, and clears `Pending`.
  - **Guard:** `MutateAsync` gains `bool allowPending = false`. When `state.Pending != null` and `!allowPending` → `ConflictException("Há um ataque esperando decisão… / An attack is waiting for a decision…")`.
  - **Pending methods:** they pass `allowPending: true` and require the matching `Pending.Step` / `TargetId`.
  - **Next turn:** refused while a pending attack exists (covers Task 10).
  - **Seq:** `Pending.Seq` is reserved from `AttackSeq + 1`. Apply uses the same seq.
  - **Resolver:** dodge per target = `r.Dodge || (r.DodgeTargets?.Contains(t.Id) ?? false)`.

- [ ] **Step 1: Failing unit tests (`PendingAttackTests`)**, using typed dice through a fake `IDiceRoller`:
  - `Ref7_target_resolves_immediately`;
  - `Ref8_target_pauses_on_defense` (`Pending.Step == Defense`; the target's HP and the attacker's ammo are unchanged);
  - `Dodge_uses_evasion_roll_as_dv` (Evasion 6 + d10 7 = 13 vs attack 14 → hit);
  - `No_dodge_uses_range_dv`;
  - `Two_sixes_pause_on_critical_after_defense`;
  - `Critical_step_rolls_2d6_and_applies_injury`;
  - `Cancel_spends_nothing_and_clears`;
  - `Other_actions_refused_while_pending` (next turn, damage, a new attack → 409);
  - `Undo_after_pending_attack_restores` (dodge → hit → undo → HP and ammo back);
  - `Old_encounter_json_loads` (state JSON without `pending`).
- [ ] **Step 2: Failing integration tests (`TablePendingAttackApiTests`):**
  - `Player_target_answers_own_defense`;
  - `Player_cannot_answer_for_another_pc` (409/403);
  - `Gm_can_answer_defense_for_pc_target`;
  - `Attacker_player_rolls_critical`;
  - `Seat_view_shows_pending_mine_for_target_only`;
  - `Pending_view_hides_attack_total` (`PendingView` has no total field: assert the JSON lacks `attackTotal`).
- [ ] **Step 3: Run, expect FAIL. Step 4: Implement.**
- [ ] **Step 5: Run the whole backend suite, expect PASS.**
- [ ] **Step 6: Commit** `Combate: ataque pendente — esquiva do alvo (REF 8+) e Critical Injury rolada pelo jogador`

### Task 15: Pending attack UI (D1 frontend)

**Files:**
- Create: `frontend/src/features/table/PendingCard.tsx`
- Modify: `frontend/src/api/encounterTypes.ts`, `tableTypes.ts`, `table.ts` (`playerApi.defense`, `playerApi.critical`), `frontend/src/api/encounters.ts` (GM pending commands)
- Modify: `frontend/src/features/table/PlayerTablePage.tsx`, `GmTablePage.tsx`, `frontend/src/features/combat/EncounterView.tsx`
- Modify: `frontend/src/features/table/dice3d/dice.ts` (`throwOf` animates `critDice` when present)
- Test: `frontend/src/features/table/fight.test.ts` (or a new `pending.test.ts`)

**Interfaces:**
- Consumes: `PendingView` and the routes from Task 14.
- Produces:
  - `pendingPrompt(p: PendingView, mine: boolean, gm: boolean): { title: string; actions: ('dodge' | 'noDodge' | 'critical' | 'cancel')[] } | null`.
  - `PendingCard` props: `{ pending: PendingView; mine: boolean; gm: boolean; busy: boolean; onDodge(dodge: boolean): void; onCritical(): void; onCancel?(): void }`.

- [ ] **Step 1: Failing tests (`pendingPrompt`):**
  - the defending player gets `['dodge','noDodge']`;
  - a non-owner player gets `null`;
  - the GM gets the step's actions plus `'cancel'`;
  - the critical step for the attacker → `['critical']`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.**
  - **Defense card:** "{atacante} atira em {alvo} com {arma}" with the buttons "Esquivar (Evasion +{n})" and "Não esquivar (DV {rangeDv})".
  - **Critical card:** "Dois 6 no dano! Rolar Critical Injury".
  - **Where:** an overlay above the `AimBar` on the table pages and above the queue on /combat.
  - **Sound:** a short ping through `sound.ts` when it's "mine".
  - **Locking:** disable the aim, end turn and the "Próximo turno" button while pending.
- [ ] **Step 4: Run, expect PASS. Run the Edge script** `scratchpad/pw/pending.mjs`: a GM and two seats. A shoots B (REF 8), B gets the card and dodges, the result shows for all. Force two 6s with a dev-only typed-dice path through the GM's `/combat` form: A's player gets the critical button.
- [ ] **Step 5: Commit** `Mesa e combate: cartões de decisão do ataque pendente (esquiva e crítico)`

### Task 16: Solo Combat Awareness in combat (D3)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Encounters/Combat/FighterViews.cs` (Precision Attack in `WeaponView.AttackTotal`), `EncounterState.cs` (`Combatant.RoundFlags: List<string>`), `EncounterService.cs` (initiative, damage, round change), `AttackResolver.cs` (Spot Weakness)
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Encounters/CombatAwarenessTests.cs`

**Interfaces:**
- Consumes: `FighterView.CombatAwareness` (Task 13) from `CharacterRole.Split` of the Solo role (Task 6).
- Values (CP:R p.146):
  - **Precision Attack:** 3/6/9 points → +1/+2/+3 (= points/3, rounded down, max 3).
  - **Spot Weakness:** +points damage (before armor) on the first hit of the round. Flag `"spotWeakness"`.
  - **Damage Deflection:** 2/4/6/8/10 points → −1..−5 (= points/2, max 5) on the first damage taken in the round. Flag `"damageDeflection"`.
  - **Initiative Reaction:** +points on the initiative roll.
  - The flags clear when `Round` increments.

- [ ] **Step 1: Failing tests:**
  - `Precision_attack_6_points_adds_2`;
  - `Spot_weakness_adds_once_per_round` (two hits in the same round: only the first gets +N; the next round gets it again);
  - `Damage_deflection_reduces_first_damage_only`;
  - `Initiative_reaction_adds_points`;
  - `Non_solo_gets_nothing`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.** Each bonus adds a note citing CP:R p.146 to the attack/damage notes.
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Combate: Combat Awareness do Solo (Precision, Spot Weakness, Deflection, Initiative Reaction)`

### Task 17: Attack breakdown + Excellent / Smartgun / Targeting Scope (D4)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Encounters/Combat/FighterViews.cs` (`WeaponView.AttackParts`), `Characters/Calculation/CharacterCalculator.cs` (`WeaponLines`: Excellent and Smartgun flags), `AttackResolver.cs` (Targeting Scope on aimed shots), `Encounters/Rules/combat-rules.json` (rule entries with sources)
- Modify: `frontend/src/features/table/SceneStrip.tsx` (`AimBar` shows the breakdown), `frontend/src/features/combat/AttackPanel.tsx`
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Encounters/AttackModifierTests.cs`

**Interfaces:**
- Produces:
  - `WeaponView.AttackParts: IReadOnlyList<ValuePart>` (label + value; their sum plus the skill total = `AttackTotal`);
  - TS `attackBreakdown(w: WeaponView): string`, for example `"Handgun 12 + Precision Attack 1 + Excellent 1"`.
- Rules:
  - **Excellent Quality:** +1 (CP:R p.344, confirm).
  - **Smartgun Link:** +1 on ranged attacks when the weapon has an attached `smartgun-link` **and** the PC has `neural-link` plus `interface-plugs` or `subdermal-grip` (item description of `smartgun-link`).
  - **Targeting Scope:** +1 on aimed shots when the PC has `targeting-scope` (item description); it applies in `AttackResolver` when the mode is Aimed*.
  - If a page or rule can't be confirmed in the repo data, leave that one out and report it to the user.

- [ ] **Step 1: Failing tests:**
  - `Excellent_weapon_adds_1`;
  - `Smartgun_needs_link_and_plugs` (link without neural link → +0; with it → +1);
  - `Targeting_scope_adds_1_to_aimed_only`;
  - `Attack_parts_sum_to_total`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.** Add a vitest test `attackBreakdown joins parts`.
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Combate: conta do ataque visível; Excellent, Smartgun Link e Targeting Scope`

### Task 18: Ranger Combat Boomerang (D5)

**Files:**
- Modify: `backend/src/CyberpunkDb.Application/Encounters/Rules/combat-rules.json` + `CombatRules.cs` (`SpecialWeapons`), `Combat/FighterViews.cs`, `Combat/AttackResolver.cs`, `EncounterState.cs` (`Combatant.Thrown: List<string>`), `EncounterService.cs` (mark on attack, return at the start of the next turn, new `PickUpAsync`)
- Modify: `backend/src/CyberpunkDb.Api/Controllers/EncountersController.cs`, `TablePlayController.cs`
- Modify: `frontend/src/features/table/aim.ts` (`cannotFire`: thrown), `SceneStrip.tsx`, `PlayerPanels.tsx` ("Pegar do chão / Pick up")
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Encounters/BoomerangTests.cs`

**Interfaces:**
- Produces:
  - `combat-rules.json`: `"specialWeapons": { "value": { "ranger-combat-boomerang": { "thrownOnly": true, "halvesArmor": true, "rof": 2, "returnsWith": "targeting-scope" } }, "source": "Black Chrome (ranger-combat-boomerang)" }`.
  - `WeaponView.ThrownOnly: bool`, `WeaponView.Thrown: bool` (currently out of hand) and `FighterView.HasCyberware(string slug)`, or a precomputed `CyberwareSlugs` set.
  - `POST /api/encounters/{id}/combatants/{cid}/pick-up { version, weapon }` and `POST /api/table/play/pick-up { weapon }`.
- Behavior:
  - **A `thrownOnly` weapon:** skill `athletics` (the `Athletics` total), `Melee = false`, DV from `combat.Row(doc.Thrown.Value.Row)` with the range from the map, its own damage (3d6) and `HalvesArmor = true`.
  - After an attack, its key goes into `Combatant.Thrown`. Attacking while thrown → 400 "O boomerang ainda não voltou / The boomerang hasn't come back".
  - When the turn passes **to** this combatant: if it has `targeting-scope` → remove the key and log "o boomerang volta / the boomerang returns".
  - `PickUp` removes the key (GM any time; player on own turn).

- [ ] **Step 1: Failing tests:**
  - `Boomerang_uses_athletics_and_thrown_dv`;
  - `Boomerang_halves_armor`;
  - `Boomerang_is_not_melee`;
  - `Thrown_boomerang_cannot_attack_again`;
  - `Returns_at_next_turn_with_targeting_scope`;
  - `Stays_without_scope_until_picked_up`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement. Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Combate: Ranger Combat Boomerang arremessado (Athletics, metade do SP, volta com Targeting Scope)`

---

## Package E — sheet popup, sessions, dice color

### Task 19: Seat-scoped character API + "Minha ficha" popup (E1)

**Files:**
- Modify: `backend/src/CyberpunkDb.Api/Table/PlayerSurfaceGuard.cs` (`PlayerMayReach`)
- Create: `backend/src/CyberpunkDb.Api/Table/SeatCharacterAccess.cs` (path matcher and seat check)
- Modify: `frontend/src/api/client.ts` (`setSeatContext`), `frontend/src/api/characters.ts` (`portraitUrl` adds `seat`)
- Create: `frontend/src/features/table/MySheetDialog.tsx`
- Modify: `frontend/src/features/table/PlayerTablePage.tsx` (header button)
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TableSeatCharacterApiTests.cs`

**Interfaces:**
- Produces:
  - `static class SeatCharacterAccess { static bool IsAllowedShape(HttpRequest req, out int? characterId); }`.
    - **Allowed on the player surface:**
      - `GET /api/characters/rules`, `GET /api/characters/store`;
      - `GET|PUT|POST /api/characters/{id}` and `/api/characters/{id}/{**rest}`;
      - the other read routes the character workspace calls. Grep `request<` and `characterApi.` under `frontend/src/features/characters` and list each path explicitly.
    - **Excluded:** `DELETE /api/characters/{id}`, `POST /{id}/duplicate`, `GET /{id}/export`, `POST /api/characters/import`, `POST /api/characters`.
    - **Path normalization:** segments are compared case-insensitively. Any `..`, `.`, `%2e` or `%2f` segment → refuse.
  - The guard authenticates `X-Seat` (or `?seat=` for the portrait GET) through `SeatService.AuthenticateAsync` and requires `seat.CharacterId == id` for the id routes. Anything else → 404 (same as today).
  - TS: `setSeatContext(seat: string | null)`. `request()` adds `X-Seat` when set, and `characterApi.portraitUrl` appends `&seat=`.
  - `MySheetDialog({ characterId, open, onClose })` renders `EmbeddedCharacter` with local tab state inside a near-fullscreen `Modal`.

- [ ] **Step 1: Failing tests:**
  - `Own_character_get_and_equip_pass_on_player_port`;
  - `Guard_refuses_other_character_paths` (theory over `/api/characters/{other}`, `/api/characters/{own}/../{other}`, `/api/characters/%2E%2E/{other}`, `/API/CHARACTERS/{other}` → 404);
  - `Delete_duplicate_export_import_refused_on_player_port`;
  - `Missing_or_revoked_seat_is_404`;
  - `Store_read_passes_with_seat`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.**
  - The player page calls `setSeatContext(token)` on mount and `null` on unmount.
  - The "Minha ficha / My sheet" button shows only when `seat.characterId != null`.
  - Equipping inside the popup updates the table through Task 1.
- [ ] **Step 4: Run, expect PASS. Edge check** through the player port (dev 5081): open the popup, equip armor, see the SP change on the strip of another seat.
- [ ] **Step 5: Commit** `Mesa: popup Minha ficha editável pelo túnel, só do personagem do convite`

### Task 20: Sessions (E2)

**Files:**
- Create: `backend/src/CyberpunkDb.Domain/Entities/TableSession.cs`
- Modify: `backend/src/CyberpunkDb.Domain/Entities/TableMessage.cs` (`SessionId`), `Encounter.cs` (`SessionId?`), `IAppDbContext.cs`, the Infrastructure `AppDbContext` + configuration
- Create: migrations `TableSessions` in both projects. The migration SQL inserts "Sessão 1" (StartedAt = the oldest message or now) and points every existing message to it.
- Create: `backend/src/CyberpunkDb.Application/Table/TableSessionService.cs`
- Modify: `Table/ChatService.cs` (post into the open session; `ClearAsync` removed), `TableFightService.cs` (stamp `SessionId` on create/link), `TableController.cs`
- Modify: `frontend/src/api/table.ts`, `tableTypes.ts`, `GmTablePage.tsx` (replace "Limpar histórico"), create `frontend/src/features/table/SessionsDialog.tsx`
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TableSessionsApiTests.cs`

**Interfaces:**
- Produces:
  - `TableSession { int Id; string Name; DateTime StartedAt; DateTime? EndedAt; }`. Exactly one session has `EndedAt == null`.
  - `TableSessionService`:
    - `CurrentAsync(ct)`: creates "Sessão 1" if none exists;
    - `StartNewAsync(string? name, ct)`: ends the current one with `name ?? "Sessão {date}"` and opens a new one;
    - `ListAsync(ct)` → `TableSessionDto(int Id, string Name, DateTime StartedAt, DateTime? EndedAt, int Messages, IReadOnlyList<EncounterSummaryDto> Fights)`;
    - `MessagesAsync(int id, ct)`, `RenameAsync(int id, string name, ct)`, `DeleteAsync(int id, ct)` (refuses the open one).
  - GM routes: `POST /api/table/sessions { name? }`, `GET /api/table/sessions`, `GET /api/table/sessions/{id}/messages`, `PUT /api/table/sessions/{id} { name }`, `DELETE /api/table/sessions/{id}`.
  - `DELETE /api/table/messages` (the old clear) is removed, together with its client call.
  - Player and GM `messages` return only the open session.

- [ ] **Step 1: Failing tests:**
  - `New_session_starts_empty_chat_and_archives_old`;
  - `Messages_go_to_open_session`;
  - `List_shows_counts_and_fights` (a fight created on the table during the session is listed);
  - `Archived_messages_readable`;
  - `Cannot_delete_open_session`;
  - `Player_sees_only_open_session`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement**, including both migrations (ask before `dotnet ef`). `SessionsDialog` lists the sessions and opens a read-only `ChatPanel` (no input) with the fights' links to `/combat/{id}`.
- [ ] **Step 4: Run, expect PASS** (with both providers if the suite runs Sqlite).
- [ ] **Step 5: Commit** `Mesa: sessões — nova sessão arquiva o chat e os combates, sessões anteriores para ler`

### Task 21: Dice color per person (E3)

**Files:**
- Modify: `backend/src/CyberpunkDb.Domain/Entities/TableSeat.cs` (`DiceColor`)
- Create: `backend/src/CyberpunkDb.Domain/Entities/TableSetting.cs` (Key/Value), with both migrations (`DiceColorAndSettings`)
- Create: `backend/src/CyberpunkDb.Application/Table/TableSettingsService.cs`
- Modify: `Table/ChatService.cs` (stamp `diceColor` on roll and attack payloads), `Table/Dice/*` (`RollResult.DiceColor`), `TableAttacks.cs` (`TableAttack.DiceColor`), `TablePlayController.cs`, `TableController.cs`
- Modify: `frontend/src/features/table/dice3d/DiceTray.tsx`, `diceScene.ts`, `dice.ts` (`throwOf` carries the color), create `frontend/src/features/table/DiceColorPicker.tsx`, `PlayerTablePage.tsx`, `GmTablePage.tsx`
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TableChatApiTests.cs`, `frontend/src/features/table/dice3d/dice.test.ts`

**Interfaces:**
- Produces:
  - `TableSettingsService.GetAsync(string key, ct)` / `SetAsync(string key, string value, ct)`, keys `gmDiceColor` and (Task 22) `music`, `musicRecent`.
  - Routes: `PUT /api/table/play/dice-color { color }` and `PUT /api/table/dice-color { color }`, with `^#[0-9a-fA-F]{6}$` validated (400 otherwise).
  - TS `inkFor(hex: string): '#0b0d10' | '#f4f6f8'`: relative luminance > 0.5 → dark numbers.

- [ ] **Step 1: Failing tests:**
  - `Roll_message_carries_seat_dice_color` (falls back to the seat color when null);
  - `Gm_roll_carries_gm_dice_color`;
  - `Invalid_color_is_400`;
  - vitest `inkFor('#ffffff')` is dark and `inkFor('#101010')` is light;
  - vitest `throwOf passes diceColor`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.** The `diceScene` material color comes from the throw, and the face texture ink comes from `inkFor`. The picker is a native `<input type="color">` next to the dice toggle.
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Mesa: cada um escolhe a cor dos próprios dados`

---

## Package G — YouTube music

### Task 22: Music state and routes (G backend)

**Files:**
- Create: `backend/src/CyberpunkDb.Application/Table/TableMusicService.cs`, `TableMusic.cs`
- Modify: `Table/ITableNotifier.cs` + `HubTableNotifier.cs` (`MusicAsync`), `TableController.cs`, `TablePlayController.cs` (`state` includes music), `Table/TableContracts.cs` (`PlayerState.Music`)
- Test: `backend/tests/CyberpunkDb.Tests/Unit/Table/YouTubeLinkTests.cs`, `Integration/TableMusicApiTests.cs`

**Interfaces:**
- Produces:
  - `record TableMusic(string? VideoId, string? PlaylistId, bool Playing, double PositionSeconds, DateTime UpdatedAt, int Volume)`;
  - `static (string? VideoId, string? PlaylistId) YouTubeLink.Parse(string url)`. It accepts `youtube.com/watch?v=`, `youtu.be/`, `youtube.com/playlist?list=`, `music.youtube.com/watch` and `&list=`. The id must match `^[A-Za-z0-9_-]{11}$` and the list `^[A-Za-z0-9_-]{10,64}$`. Anything else → `RequestValidationException("url", …)`.
  - Routes:
    - `PUT /api/table/music { url, title? }` loads paused at 0 and adds to the recent list (max 8, deduped);
    - `POST /api/table/music/play { positionSeconds? }`, `POST /api/table/music/pause`, `POST /api/table/music/seek { positionSeconds }`, `POST /api/table/music/next`;
    - `DELETE /api/table/music` stops;
    - `GET /api/table/music` returns `{ music, recent }`.
  - Hub event `music` with a `TableMusic?` payload to the GM and players. Persisted in `TableSettingsService` (Task 21).

- [ ] **Step 1: Failing tests:**
  - theory `Parse_accepts_known_forms` (5 URLs → expected ids);
  - `Parse_rejects_other_hosts_and_bad_ids` (`https://evil.com/watch?v=dQw4w9WgXcQ`, `javascript:` and an 8-char id);
  - `Play_sets_playing_and_timestamp`;
  - `Recent_keeps_8_deduped`;
  - `Player_state_includes_music`;
  - `Seat_receives_music_event`.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement. Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Mesa: estado da música (YouTube) no servidor, rotas do mestre e evento ao vivo`

### Task 23: Music player and GM panel (G frontend)

**Files:**
- Create: `frontend/src/features/table/music/youtube.ts` (lazy loader for `https://www.youtube.com/iframe_api`), `music/sync.ts`, `music/sync.test.ts`, `music/MusicPlayer.tsx`, `music/MusicPanel.tsx`
- Modify: `frontend/src/features/table/useTableHub.ts` (`music` handler), `PlayerTablePage.tsx`, `GmTablePage.tsx` (new tab or toolbar popover "Música"), `tablePrefs.ts` (`musicVolume`, `musicMuted`, `musicUnlocked`), `Table.module.css`

**Interfaces:**
- Consumes: the Task 22 routes and event.
- Produces:
  - `expectedPosition(m: TableMusic, nowMs: number): number`, which equals `m.positionSeconds + (m.playing ? (nowMs - Date.parse(m.updatedAt)) / 1000 : 0)`;
  - `needsSeek(actual: number, expected: number): boolean`, true when `Math.abs(actual - expected) > 2`.

- [ ] **Step 1: Failing tests:**
  - `expectedPosition` paused returns the stored position;
  - playing for 5 s → +5;
  - `needsSeek(10, 12.5)` is true and `needsSeek(10, 11.9)` is false.
- [ ] **Step 2: Run, expect FAIL. Step 3: Implement.**
  - **`MusicPlayer`:**
    - loads the API only when `music` is non-null;
    - is at least 200×200 in a corner of `boardArea`, collapsible to a pill that keeps playing;
    - shows "Ativar som / Enable sound" until the first click (`musicUnlocked`);
    - every 3 s and on each event, applies `needsSeek` and `seekTo`, plus `playVideo`/`pauseVideo`;
    - the local volume and mute come from `tablePrefs`;
    - `onError` 101/150 → a GM-only toast "Este vídeo não permite tocar fora do YouTube / This video can't play outside YouTube".
  - **`MusicPanel` (GM):** URL input + title, play/pause, seek slider, next, recent chips.
- [ ] **Step 4: Run, expect PASS. Edge check** (GM + 2 seats): load a link, play, pause and seek, and the seats follow within 2 s. The player who joins in the middle starts at the right position.
- [ ] **Step 5: Commit** `Mesa: música do YouTube sincronizada para todos, com painel do mestre`

---

### Task 24: Final verification

- [ ] **Step 1:** Run the whole backend suite and `npm --prefix frontend test`, `typecheck` and `lint`. Everything passes; record the counts.
- [ ] **Step 2:** Run the whole-branch review (superpowers:requesting-code-review) and fix the Critical and Important findings.
- [ ] **Step 3:** Rebuild the single-file exe as in previous rounds (see the `release.yml` publish step) and smoke-test it: the GM table, one seat through the player port, a pending dodge, the popup and music.
- [ ] **Step 4:** Update the memory file `mesa-rodada4-plan.md` with the commits and test counts. Do not merge, push or tag without the user asking.
