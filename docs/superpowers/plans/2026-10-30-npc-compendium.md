# Compêndio de NPCs: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Listar em `/npcs` os NPCs nomeados dos livros de Cyberpunk RED, com ficha ou perfil, e permitir colocar quem tem ficha no Friday Night Firefight.

**Architecture:** Um JSON conferido à mão (`data/source/npc-compendium.json`) é embutido no `CyberpunkDb.Application` e carregado num singleton. Uma API só de leitura serve as telas, e `AddCombatantsRequest` ganha um campo que transforma a entrada num `CombatSnapshot`.

**Tech Stack:** .NET 10, xUnit, FluentAssertions, React 19, TypeScript, react-query, Vite, Vitest, Node + `pdftotext` para a extração.

**Spec:** `docs/superpowers/specs/2026-10-30-npc-compendium-design.md`

## Global Constraints

- Branch: `feature/npc-compendium`, criada a partir de `master`. Nada de push ou tag sem pedido do Yuri.
- Rodar .NET com `export PATH="$USERPROFILE/.dotnet:$PATH"`.
- Nenhum número inventado: todo valor de ficha vem do PDF. Campo `—` no livro vira `null`.
- Página citada é sempre a impressa, nunca a do PDF.
- `summary`, `specials[].text` e `sheetNote` são escritos por nós em PT e EN, sem copiar frases dos livros. Regras especiais preservam a mecânica em poucas frases.
- Nomes de NPC, armas, equipamentos e perícias ficam em inglês, como no livro.
- Textos da interface usam `t(pt, en)`. Mensagens do backend usam `L.T(pt, en)`.
- Fichas genéricas não entram. Critério de entrada: seção "Quem entra" da spec.
- Sem migration e sem tabela nova.
- Comentários e nomes de código em inglês; documentação em português, como no resto do repositório.

## Review Focus

1. **Campos `—` no livro** (Smasher não tem EMP nem Seriously Wounded): a ficha mostra `—`, nunca `0`, e o combate funciona. Teste na Task 3 e na Task 5.
2. **NPC com ficha incompleta** (sem Evasion, sem armadura ou sem arma): entra em combate com SP 0 e ataque desarmado, sem erro. Teste na Task 3.
3. **Dano fora do padrão `NdM`** (`2d6+1`, `—`): o teste de dados usa as mesmas regras do motor de combate, e dano sem dados vira nota, não arma. Teste na Task 1.
4. **Busca com acento e apelido** (`Léon`, `leon`, `Malarkey`): encontra o NPC. Teste na Task 4.
5. **Combate encerrado ou versão desatualizada no diálogo:** só combates abertos são oferecidos, a versão vem do detalhe buscado na hora, e um conflito mostra toast de erro sem fechar o diálogo. Teste na Task 6.

---

## File Structure

| Arquivo | Responsabilidade |
|---|---|
| `data/source/npc-compendium.json` | Os dados |
| `backend/src/CyberpunkDb.Application/Npcs/Compendium/CompendiumNpc.cs` | Records do JSON |
| `backend/src/CyberpunkDb.Application/Npcs/Compendium/NpcCompendium.cs` | Carrega, indexa e monta DTOs |
| `backend/src/CyberpunkDb.Application/Npcs/Compendium/CompendiumContracts.cs` | `CompendiumNpcSummary`, `CompendiumNpcDetail` |
| `backend/src/CyberpunkDb.Api/Controllers/NpcCompendiumController.cs` | API só de leitura |
| `backend/src/CyberpunkDb.Application/Encounters/Combat/FighterViews.cs` | `Compendium(...)` |
| `backend/tests/CyberpunkDb.Tests/NpcCompendium/NpcCompendiumDataTests.cs` | Validação dos dados |
| `backend/tests/CyberpunkDb.Tests/Integration/NpcCompendiumApiTests.cs` | API e combate |
| `frontend/src/api/npcCompendium.ts` | Tipos e hooks |
| `frontend/src/features/npc-compendium/filterNpcs.ts` (+ `.test.ts`) | Filtro puro |
| `frontend/src/features/npc-compendium/NpcCompendiumPage.tsx` | Lista |
| `frontend/src/features/npc-compendium/NpcDossierPage.tsx` | Ficha |
| `frontend/src/features/npc-compendium/AddToCombatDialog.tsx` | Diálogo de combate |
| `tools/npc-compendium/extract.mjs` | Rascunho de blocos e relatório |

---

### Task 1: Modelo, carregamento e dados de partida

**Files:**
- Create: `data/source/npc-compendium.json`, `Npcs/Compendium/CompendiumNpc.cs`, `Npcs/Compendium/NpcCompendium.cs`
- Modify: `backend/src/CyberpunkDb.Application/CyberpunkDb.Application.csproj`, `backend/src/CyberpunkDb.Application/DependencyInjection.cs`
- Test: `backend/tests/CyberpunkDb.Tests/NpcCompendium/NpcCompendiumDataTests.cs`

**Interfaces:**
- Produces:
  - `record CompendiumNpc { string Id; string Name; List<string> Aliases; CompendiumKind Kind; string? Role; int? RoleRank; int? Rep; string? Affiliation; List<CompendiumSource> Sources; LocalizedText Summary; LocalizedText? SheetNote; CompendiumSheet? Sheet }`
  - `enum CompendiumKind { Sheet, Profile }`, `record CompendiumSource(string Book, int Page)`, `record LocalizedText(string Pt, string En)` com `string Current => L.T(Pt, En)`
  - `record CompendiumSheet { int Hp; int? SeriouslyWounded; int? DeathSave; Dictionary<string,int?> Stats; Dictionary<string,int> ArmoredStats; List<CompendiumWeapon> Weapons; List<CompendiumArmor> Armor; Dictionary<string,int> Skills; List<string> Gear; List<string> Cyberware; List<CompendiumSpecial> Specials }`
  - `record CompendiumWeapon { string Name; string Skill; int AttackBase; string Damage; int Rof; int? Magazine; WeaponClass? Class; bool Melee; string? Notes }`
  - `record CompendiumArmor(string Location, string Name, int Sp)`, `record CompendiumSpecial(string Name, LocalizedText Text)`
  - `class NpcCompendium { IReadOnlyList<CompendiumNpc> All; CompendiumNpc? Find(string id); static NpcCompendium LoadEmbedded() }`

- [ ] **Step 1: Criar a branch e commitar a spec e o plano**

```bash
git checkout -b feature/npc-compendium
git add docs/superpowers
git commit -m "Compêndio de NPCs: spec e plano"
```

- [ ] **Step 2: Escrever os testes de dados**

`NpcCompendiumDataTests`, com `NpcCompendium.LoadEmbedded()`:
- `Ids_are_unique_slugs`: regex `^[a-z0-9]+(-[a-z0-9]+)*$`, sem repetição.
- `Names_are_unique`
- `Every_entry_cites_a_printed_page`: `Sources` não vazio, `Page > 0`, `Book` não vazio.
- `Every_entry_has_both_languages`: `Summary.Pt` e `Summary.En` preenchidos e diferentes.
- `Sheet_matches_kind`: `Kind == Sheet` exige `Sheet != null`; `Profile` exige `null`.
- `Sheets_are_usable`: `Hp > 0`; chaves de `Stats` e `ArmoredStats` dentro de `INT REF DEX TECH COOL WILL LUCK MOVE BODY EMP`.
- `Weapon_damage_rolls_in_the_combat_engine`: `Damage` casa com `^\d+d\d+([+-]\d+)?$`, `Rof >= 1`.

- [ ] **Step 3: Rodar e ver falhar**

Run: `cd backend && dotnet test --filter "FullyQualifiedName~NpcCompendiumDataTests"`
Expected: erro de compilação, `NpcCompendium` não existe.

- [ ] **Step 4: Implementar records e `NpcCompendium`**

Seguir `NpcRuleBook.LoadEmbedded()`: `JsonSerializerOptions` com `PropertyNameCaseInsensitive`, `JsonStringEnumConverter`. O csproj ganha:
`<EmbeddedResource Include="..\..\..\data\source\npc-compendium.json" LogicalName="CyberpunkDb.Application.Npcs.npc-compendium.json" />`
Registrar `services.AddSingleton(_ => NpcCompendium.LoadEmbedded());`.

- [ ] **Step 5: Escrever as entradas piloto**

Em `npc-compendium.json`: as fichas de **Adam Smasher**, **Falco**, **Maria Torres** e **Dalton Estevan-Luis** do Edgerunners Mission Kit, e o perfil de **Morgan Blackhand** do Core (se a confirmação não achar ficha). Valores conferidos no PDF.

- [ ] **Step 6: Rodar e ver passar**

Run: `cd backend && dotnet test --filter "FullyQualifiedName~NpcCompendiumDataTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add data/source/npc-compendium.json backend
git commit -m "Compêndio de NPCs: dados de partida e carregamento"
```

### Task 2: API

**Files:**
- Create: `Npcs/Compendium/CompendiumContracts.cs`, `backend/src/CyberpunkDb.Api/Controllers/NpcCompendiumController.cs`
- Modify: `Npcs/Compendium/NpcCompendium.cs`
- Test: `backend/tests/CyberpunkDb.Tests/Integration/NpcCompendiumApiTests.cs`

**Interfaces:**
- Consumes: `NpcCompendium`, `LocalizedText.Current`.
- Produces:
  - `GET /api/npc-compendium` → `CompendiumNpcSummary(string Id, string Name, IReadOnlyList<string> Aliases, CompendiumKind Kind, string? Role, int? RoleRank, int? Rep, string? Affiliation, IReadOnlyList<CompendiumSource> Sources, string Summary)`
  - `GET /api/npc-compendium/{id}` → `CompendiumNpcDetail(...os mesmos..., string? SheetNote, CompendiumSheetDto? Sheet)`. `CompendiumSheetDto` é a `CompendiumSheet` com `Specials` como `(string Name, string Text)`.
  - `NpcCompendium.Require(string id)`: lança `NotFoundException(L.T($"NPC '{id}' não está no compêndio.", $"NPC '{id}' isn't in the compendium."))`.

- [ ] **Step 1: Escrever os testes**

`[Collection(ApiCollection.Name)]`:
- `List_has_sheets_and_profiles`: contém `adam-smasher` com `kind == "Sheet"` e `morgan-blackhand` com `"Profile"`; lista ordenada por nome.
- `Detail_returns_the_printed_sheet`: `adam-smasher` tem `sheet.hp == 75`, `sheet.deathSave == 17`, `sheet.stats.BODY == 17`, `sheet.stats.EMP == null`.
- `Texts_follow_the_language`: o `summary` com `en-US` difere do `summary` com `pt-BR`.
- `Unknown_id_is_404`: `en-US` contém `isn't in the compendium`.

- [ ] **Step 2: Rodar e ver falhar**

Run: `cd backend && dotnet test --filter "FullyQualifiedName~NpcCompendiumApiTests"`
Expected: FAIL com 404 na rota.

- [ ] **Step 3: Implementar contratos, `Require` e o controller**

Controller no molde de `EncountersController`, com `[Route("api/npc-compendium")]`.

- [ ] **Step 4: Rodar e ver passar**

Run: mesmo comando. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "Compêndio de NPCs: API de leitura"
```

### Task 3: Entrar em combate

**Files:**
- Modify: `Encounters/EncounterContracts.cs`, `Encounters/EncounterService.cs:105-147`, `Encounters/Combat/FighterViews.cs`, `Npcs/Compendium/NpcCompendium.cs`
- Test: `Integration/NpcCompendiumApiTests.cs`, `NpcCompendium/NpcCompendiumDataTests.cs`

**Interfaces:**
- Consumes: `NpcCompendium.Require`, `NpcRuleBook.Skills`.
- Produces:
  - `AddCombatantsRequest(..., string? CompendiumNpcId = null)`, no fim dos parâmetros.
  - `CombatSnapshot FighterViews.Compendium(CompendiumNpc npc)`.
  - `string? NpcCompendium.SkillKey(string name, NpcRuleBook rules)`: `Name` igual ignorando caixa dá o `Id`; `"Language (English)"` dá `"language:English"` no formato `{id}:{especialidade}`.

**Decisão:** o combatente usa `CombatantKind.Npc`, não `Generic`. `FighterSheet.tsx:19` rotula `Generic` como "Inimigo genérico", o que seria errado aqui. Gerados já usam `Npc` com `NpcId` nulo, então esse caminho está coberto.

- [ ] **Step 1: Escrever os testes**

- `Adam_Smasher_joins_a_fight`: cria encontro, `POST .../combatants` com `compendiumNpcId: "adam-smasher"`, `side: "Enemy"`. Confere HP 75, nome "Adam Smasher" e as armas com ataque do livro.
- `Count_numbers_the_copies`: `count: 2` gera 2 combatentes com nomes distintos.
- `A_profile_cannot_join`: `morgan-blackhand` devolve 400 com "Este NPC não tem ficha oficial." em `pt-BR`.
- `Unknown_npc_is_404`.
- `A_compendium_npc_can_attack`: Smasher ataca outro combatente com a primeira arma, com sucesso, e o resultado usa `attackBase`.
- `Null_stats_stay_out_of_the_snapshot`: `Stats` do fighter não tem `EMP`, e rolar iniciativa funciona.
- `Incomplete_sheet_still_fights`: unit test de `FighterViews.Compendium` com ficha sem `Armor`, `Weapons` e `Skills`: SP 0, sem exceção, `evasion` ausente tratada como 0.
- Em `NpcCompendiumDataTests`, `Skills_map_to_the_rule_book`: toda perícia de `Skills` e `Weapons[].Skill` dá `SkillKey` não nulo, exceto as de uma lista explícita `KnownExceptions` no próprio teste.

- [ ] **Step 2: Rodar e ver falhar**

Run: `cd backend && dotnet test --filter "FullyQualifiedName~NpcCompendium"`
Expected: FAIL.

- [ ] **Step 3: Implementar `FighterViews.Compendium` e `SkillKey`**

Valores: `Source = $"{npc.Sources[0].Book} p.{npc.Sources[0].Page}"`; `Stats` com `ArmoredStats` por cima de `Stats`, sem nulos; `Body = Stats["BODY"]`; SP = maior `Sp` de `Location` `Body`/`Head`; `Cyberarm` por `Cyberware` contendo "Cyberarm"; armas com `Key = $"w{i}"`. O `FighterViews` recebe `NpcCompendium` e `NpcRuleBook` pelo construtor; conferir se já é registrado com acesso a eles.

- [ ] **Step 4: Adicionar o ramo em `AddCombatantsAsync`**

Antes do `else throw`. Entrada `Profile` lança `RequestValidationException.For("compendiumNpcId", L.T("Este NPC não tem ficha oficial.", "This NPC has no official sheet."))`. A mensagem do `else` passa a citar o compêndio. Usa `Npc(s, added, NameOf(r.Name ?? npc.Name, i, count), r.Side, CombatantKind.Npc, snapshot)`.

- [ ] **Step 5: Rodar e ver passar**

Run: `cd backend && dotnet test`
Expected: tudo PASS.

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "Compêndio de NPCs: entrada no Friday Night Firefight"
```

### Task 4: Lista em `/npcs`

**Files:**
- Create: `frontend/src/api/npcCompendium.ts`, `features/npc-compendium/filterNpcs.ts`, `filterNpcs.test.ts`, `NpcCompendiumPage.tsx`
- Modify: `frontend/src/App.tsx`, `frontend/src/components/layout/AppShell.tsx`

**Interfaces:**
- Consumes: as rotas da Task 2.
- Produces:
  - tipos `NpcSummary`, `NpcDetail`, `NpcSheet`, `NpcWeapon`, espelhando os DTOs (camelCase);
  - `useNpcCompendium()`, `useCompendiumNpc(id: string)`, query keys `npcCompendiumKeys`;
  - `filterNpcs(list: NpcSummary[], f: NpcFilters): NpcSummary[]`, com `NpcFilters = { q: string; book: string; role: string; sheet: 'all' | 'sheet' | 'profile' }`;
  - `npcBooks(list)` e `npcRoles(list)`: valores únicos ordenados, para os selects.

- [ ] **Step 1: Escrever `filterNpcs.test.ts`**

- `finds by name ignoring accents and case`: `q: 'leon'` acha `Kinneth "Con" de Léon`.
- `finds by alias`: `q: 'malarkey'` acha pelo apelido.
- `filters by book in any source`: NPC com duas fontes aparece nos dois livros.
- `filters by role and by sheet kind`.
- `empty filters keep the list`.

- [ ] **Step 2: Rodar e ver falhar**

Run: `cd frontend && npm test -- --run filterNpcs`
Expected: FAIL, módulo não existe.

- [ ] **Step 3: Implementar `filterNpcs.ts` e `npcCompendium.ts`**

Normalizar com `normalize('NFD').replace(/\p{M}/gu, '').toLowerCase()`. `staleTime: Infinity`, como `useEncounterRules`.

- [ ] **Step 4: Rodar e ver passar**

Run: mesmo comando. Expected: PASS.

- [ ] **Step 5: Implementar a página, a rota e o menu**

`NpcCompendiumPage` usa `page.page`, `hud-title`, `styles.cards`, `styles.card`, `styles.avatar` de `Characters.module.css`, com `Skull` ou `UserRound` no avatar. Card com link para `/npcs/{id}`, Role, Rank, Rep, afiliação e `StateTag tone="warn"` "sem ficha oficial" em perfis. Busca e `Select`s no topo. `Spinner`, `ErrorState` e `EmptyState` com botão "Limpar filtros". Rotas `/npcs` e `/npcs/:id` em `App.tsx`. Item no `others` de `AppShell.tsx`, depois de `/enemies`, com `active: location.pathname.startsWith('/npcs')`.

- [ ] **Step 6: Verificar**

Run: `cd frontend && npx tsc -b && npm run lint && npm test -- --run`
Expected: sem erros de tipo, 0 erros de lint, testes passando.

- [ ] **Step 7: Commit**

```bash
git add frontend
git commit -m "Compêndio de NPCs: lista com busca e filtros"
```

### Task 5: Ficha em `/npcs/:id`

**Files:**
- Create: `features/npc-compendium/NpcDossierPage.tsx`, `features/npc-compendium/NpcSheetPanels.tsx`, `features/npc-compendium/npcFormat.ts` (+ `.test.ts`)
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Consumes: `useCompendiumNpc`.
- Produces: `formatPrinted(value: number | null | undefined): string`, que devolve `—` para nulo.

- [ ] **Step 1: Teste de `npcFormat`**

`formatPrinted(null) === '—'`, `formatPrinted(0) === '0'`, `formatPrinted(17) === '17'`.

- [ ] **Step 2: Rodar e ver falhar, implementar, rodar e ver passar**

Run: `cd frontend && npm test -- --run npcFormat`
Expected: FAIL antes, PASS depois.

- [ ] **Step 3: Implementar a página**

Cabeçalho com avatar, nome, apelidos, Role, Rank, Rep, afiliação e fontes (`Livro p.N`). Biografia. Quem tem ficha ganha painéis no estilo da aba Ficha: STATs (com valor com armadura entre parênteses quando diferir, como no livro), HP, Seriously Wounded e Death Save, armas, armadura, perícias, equipamento, cyberware e regras especiais, além de `sheetNote` quando houver. Perfil mostra um `StateTag` e o texto "O livro não traz ficha deste personagem." Id desconhecido: `ErrorState` com link de volta para `/npcs`. Só leitura.

- [ ] **Step 4: Verificar**

Run: `cd frontend && npx tsc -b && npm run lint && npm test -- --run`
Expected: tudo limpo.

- [ ] **Step 5: Commit**

```bash
git add frontend
git commit -m "Compêndio de NPCs: ficha e perfil"
```

### Task 6: Adicionar ao combate pela interface

**Files:**
- Create: `features/npc-compendium/AddToCombatDialog.tsx`, `features/npc-compendium/openEncounters.ts` (+ `.test.ts`)
- Modify: `features/npc-compendium/NpcDossierPage.tsx`, `features/combat/AddCombatantsDialog.tsx`

**Interfaces:**
- Consumes: `useEncounters()`, `encounterApi.get`, `encounterApi.command(id, 'combatants', { version, side, compendiumNpcId, count })`. Conferir o caminho exato em `AddCombatantsDialog.tsx`.
- Produces: `AddToCombatDialog({ npc, open, onClose })`, `openEncounters(list: EncounterSummary[]): EncounterSummary[]`.

- [ ] **Step 1: Teste de `openEncounters`**

Tira os encerrados, mantém a ordem, e devolve `[]` para lista vazia. Usar os valores reais de `EncounterStatus`.

- [ ] **Step 2: Rodar e ver falhar, implementar, rodar e ver passar**

Run: `cd frontend && npm test -- --run openEncounters`
Expected: FAIL antes, PASS depois.

- [ ] **Step 3: Implementar o diálogo**

`Modal` com `Select` de combates abertos, `Segmented` de lado (`Enemy` por padrão) e `NumberInput` de quantidade (1 a 12). Ao confirmar: busca `encounterApi.get(id)` para pegar a `version` atual, envia o comando e navega para `/combat/{id}`. Erro vira toast com `problemMessage`, e o diálogo fica aberto. Sem combate aberto: texto e link para `/combat`. Botão "Adicionar ao combate" na ficha, só para `kind === 'Sheet'`.

- [ ] **Step 4: Nova origem em `AddCombatantsDialog`**

`Source` ganha `'compendium'`. `Select` lista só NPCs com ficha, e o bloco de lado e quantidade já existente vale para ela. Envia `compendiumNpcId`.

- [ ] **Step 5: Verificar**

Run: `cd frontend && npx tsc -b && npm run lint && npm test -- --run`
Expected: tudo limpo.

- [ ] **Step 6: Commit**

```bash
git add frontend
git commit -m "Compêndio de NPCs: adicionar ao combate"
```

### Task 7: Ferramenta de extração

**Files:**
- Create: `tools/npc-compendium/extract.mjs`

**Interfaces:**
- Produces: em `tools/npc-compendium/out/` (no `.gitignore`), `blocks.json` com `{ book, printedPage, rawName, level, rep, role, hp, seriouslyWounded, deathSave, stats, rawText }` e `report.txt` com a contagem por livro, nomes encontrados, possíveis fichas genéricas e blocos que não foram entendidos.

- [ ] **Step 1: Implementar o parser**

Modelo de `tools/night-market/extract.mjs`: pasta por argumento, padrão `%USERPROFILE%/Downloads/Cyberpunk PDFs`. Rodar `pdftotext -layout` por página para saber a página. Layouts:
- Edgerunners Mission Kit: `NAME ... REP ... SERIOUSLY ... HP`;
- Danger Gal Dossier: `level ... rep`, linha de números seguida de `INT` e variante `INT 5 REF 8(6) ...`;
- Tales of the RED: `name X (Rep N)`.

A página impressa vem do rodapé do PDF, com fallback para um deslocamento por livro definido no topo do script.

- [ ] **Step 2: Rodar e ler o relatório**

Run: `node tools/npc-compendium/extract.mjs`
Expected: `report.txt` lista Adam Smasher, Falco, Coldfire, The Clown. Blocos não entendidos aparecem listados, não somem.

- [ ] **Step 3: Conferir contra os pilotos**

Os valores de HP, Death Save e STATs de `blocks.json` para os pilotos da Task 1 batem com o `npc-compendium.json`.

- [ ] **Step 4: Commit**

```bash
git add tools/npc-compendium .gitignore
git commit -m "Compêndio de NPCs: extração dos blocos de ficha"
```

### Task 8: Dados por livro (um lote por commit)

**Files:**
- Modify: `data/source/npc-compendium.json`

**Interfaces:**
- Consumes: `tools/npc-compendium/out/blocks.json`, os PDFs.

Lotes, nesta ordem: (1) Edgerunners Mission Kit, incluindo a Crew; (2) Core, incluindo a confirmação de Morgan Blackhand, Rogue, Santiago, Johnny Silverhand, Alt Cunningham, Spider Murphy, Saburo Arasaka e Max Hammerman; (3) Danger Gal Dossier; (4) Tales of the RED; (5) Interface RED 2 a 5; (6) os demais: Black Chrome, Mixing Drinks, Toggle's Temple, Karaoke Night, Hornet's Pharmacy e as DLCs restantes.

Para **cada lote**:

- [ ] **Step 1:** Listar os candidatos do relatório e dos textos, aplicando o critério "Quem entra" da spec.
- [ ] **Step 2:** Escrever as entradas, conferindo cada número no PDF.
- [ ] **Step 3:** Escrever `summary` e `specials` em PT e EN, sem copiar frases.
- [ ] **Step 4:** Rodar `cd backend && dotnet test --filter "FullyQualifiedName~NpcCompendiumDataTests"`. Expected: PASS.
- [ ] **Step 5:** Commit com `git commit -m "Compêndio de NPCs: <livro>"`.

Depois do último lote:

- [ ] **Step 6: Reconciliar**

Todo bloco do `report.txt` tem de estar no JSON ou numa lista `tools/npc-compendium/discarded.txt`, com o motivo (generic ou fora do critério). Expected: nenhum bloco sem destino.

### Task 9: Documentação e verificação final

**Files:**
- Modify: `README.md`, `docs/DESENVOLVIMENTO.md`

- [ ] **Step 1: Documentar**

README: seção "🗂️ Compêndio de NPCs" depois de "Gerador de Inimigos". `DESENVOLVIMENTO.md`: seção com o formato do JSON, como rodar a extração, como corrigir um NPC e os critérios de entrada. Atualizar a contagem de testes.

- [ ] **Step 2: Verificar tudo**

Run: `cd backend && dotnet test` e `cd frontend && npm run build && npm run lint && npm test -- --run`
Expected: tudo passa.

- [ ] **Step 3: Testar no app de verdade**

Gerar o exe com `deploy\desktop\publicar.ps1` e rodar numa porta separada com banco temporário (`Urls`, `ConnectionStrings__Sqlite`, `Desktop__OpenBrowser=false`), sem encerrar o app do Yuri na 5090. Conferir `GET /api/npc-compendium`, adicionar Adam Smasher a um combate e atacar. Pedir ao Yuri a conferência visual em `/npcs` nos dois idiomas, porque não há driver de navegador nesta máquina.

- [ ] **Step 4: Commit**

```bash
git add README.md docs
git commit -m "Compêndio de NPCs: documentação"
```
