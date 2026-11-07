# Mesa rodada 6: plano de implementação

> Para agentes: executar com superpowers:executing-plans (inline). Os passos usam checkbox.

**Objetivo:** os oito blocos da spec `docs/superpowers/specs/2026-11-07-mesa-rodada6-design.md`.
**Arquitetura:** backend ASP.NET Core/EF (Application + Api), SignalR `TableHub`; frontend React + React Query + Pixi.
**Spec:** docs/superpowers/specs/2026-11-07-mesa-rodada6-design.md

## Restrições globais

- Todo texto novo é bilíngue: `t(pt, en)` no frontend, `L.T(pt, en)` no backend.
- dotnet fica em `~/.dotnet`, então os comandos usam `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"`.
- Pare a API local antes de rodar `dotnet test`, porque ela trava as DLLs.
- Nenhum merge, push ou release sem pedido do usuário.

## Foco da revisão

1. Importação pelo jogador: um lugar com personagem não pode trocar de ficha. O JSON é o mesmo do import do mestre e passa pela mesma validação.
2. Precision não pode contar duas vezes: o card em combate deve mostrar o mesmo número da ficha.
3. A música minimizada continua tocando e sincronizada; a pausa do mestre pausa todo mundo.
4. `/mesa` mudou de função: menu, links internos e scripts do Edge que esperavam a mesa em `/mesa` precisam ir para `/mesa/jogar`.
5. O RoundFlags exposto ao jogador não pode revelar nada além do que a rodada já mostra. São flags do próprio Solo, então não há segredo.

---

### Tarefa 1: card (recarregar) e dados no cartão de ataque

**Arquivos:**
- `frontend/src/features/table/Table.module.css`
- `frontend/src/features/table/chat/ChatPanel.tsx`
- novo: `frontend/src/features/table/chat/attackDice.ts` e `attackDice.test.ts`
- `backend/src/CyberpunkDb.Application/Table/TableAttacks.cs`
- `EncounterService.cs`, ApplyAttackAsync (nota de Precision)
- `frontend/src/api/tableTypes.ts`

**Passos:**
- [ ] Teste `attackDice`: a partir de uma `TableAttack`, devolve os grupos `{ label, dice: {value, sides}[] }` para ataque (d10 e extra), defesa por alvo, dano por alvo e crítico.
- [ ] Implementar `attackDice` e o `AttackView` com os chips `die`. Exibir também `target.notes` e `attack.notes`.
- [ ] Backend:
  - `TableAttackTarget.Notes` e `TableAttack.Notes`, opcionais;
  - `From` copia as notas do alvo filtradas por Combat Awareness, mais as notas do ataque;
  - teste de integração: um Solo com Spot Weakness atacando, a `TableAttack` do chat traz a nota.
- [ ] CSS: `.weaponChipGroup > :not(:first-child) { flex: none; width: auto; }`.

### Tarefa 2: Precision na ficha, RoundFlags e registro da redistribuição

**Arquivos:**
- `CharacterRules` (cálculo de `AttackBonuses` da arma)
- `FighterViews.cs`
- `FighterView`, que ganha `RoundFlags`
- `EncounterService` (`ViewsAsync` preenche os flags) e um novo método `LogAwarenessAsync(characterId)`
- `CharactersController`, endpoint de role-split
- testes em `CombatAwarenessApiTests`

**Passos:**
- [ ] Testes:
  - `Precision_shows_on_the_sheet`: na ficha, `AttackTotal` da arma +1 com 3 pontos;
  - `Precision_attack_3_points_adds_1`: o teste existente, ajustado para comparar com a ficha (o card igual à ficha, sem +2);
  - `Round_flags_show_used_deflection`;
  - `Split_in_a_fight_is_logged`.
- [ ] Implementar e rodar `CombatAwarenessApiTests` até passar.

### Tarefa 3: bloco Combat Awareness (frontend)

**Arquivos:**
- novo: `frontend/src/features/table/AwarenessBlock.tsx`, usado em `PlayerPanels.tsx` (CharacterPanel) e em `GmPanels.tsx` (TokenInspector)
- `RoleSplitDialog` reaproveitado
- novo helper puro `awarenessEffects(split, flags)` em `features/characters/roleSplit.ts`, com teste

**Passos:**
- [ ] Teste de `awarenessEffects`: com o split `{ precisionAttack: 6, damageDeflection: 4, spotWeakness: 2 }` e o flag `damageDeflection`, o resultado é Precision +2, Deflection −2 usado e Spot +2 disponível.
- [ ] Implementar o helper e o componente. O save chama o `role-split` da ficha, que já é aberto ao lugar pelo `SeatCharacterAccess`.

### Tarefa 4: ataque da aba Seleção e ver ficha nos cards

**Arquivos:**
- `GmPanels.tsx`: o TokenInspector recebe `onAim(weapon)`
- `GmTablePage.tsx`
- `SceneStrip.tsx`: novo `onSheet(entry)`
- `PlayerTablePage.tsx`
- `MySheetDialog.tsx`, que vira `SheetDialog`, com título e `player` opcionais

**Passos:**
- [ ] Ligar a arma do inspector ao `quick.start` do mestre, o mesmo dos cards.
- [ ] Ícone de ficha nos cards de PC:
  - jogador: só o próprio card, abrindo a "Minha ficha";
  - mestre: todos os PCs, abrindo o `SheetDialog` sem `player`.

### Tarefa 5: música minimizável e pausa

**Arquivos:**
- `music/sync.ts`: função `musicAction`, com teste em `sync.test.ts`
- `music/MusicPlayer.tsx`
- `tablePrefs.ts`: campo `musicMinimized`
- `Table.module.css`

**Passos:**
- [ ] Testes de `musicAction`:
  - mesa pausada e player tocando → `'pause'`;
  - mesa tocando, player pausado e som liberado → `'play'`;
  - mesa tocando e som não liberado → `'none'`.
- [ ] Implementar:
  - o intervalo usa `musicAction`;
  - faixa nova com a música pausada usa `cueVideoById` ou `cuePlaylist`;
  - minimizar põe o `musicBox` em modo barra, com o frame fora da tela (`position: fixed; left: -10000px; width: 200px; height: 200px`).

### Tarefa 6: importação de ficha pelo jogador

**Arquivos:**
- `TablePlayController`: `POST character/import`
- `SeatService`: novo `LinkImportedAsync(seat, characterId)`, ou reuso de um método existente
- `CharacterService.ImportAsync`
- notificação de seats e party
- `frontend/src/api/table.ts`: `playerApi.importCharacter`
- `PlayerTablePage.tsx`, no estado sem personagem
- teste `TableCharacterImportApiTests`

**Passos:**
- [ ] Testes:
  - um lugar sem personagem importa, o estado passa a ter personagem e a ficha existe;
  - um lugar com personagem recebe 409;
  - sem `X-Seat` a resposta é 401.
- [ ] Implementar.

### Tarefa 7: sessões como porta de entrada

**Arquivos:**
- `TableSessionService.StartNewAsync`: o nome passa a ser da sessão nova; a numeração usa o maior N em uso
- teste em `TableSessionsApiTests`
- novo: `frontend/src/features/table/SessionsPage.tsx`
- `App.tsx`: `/mesa` vira `SessionsPage` e `/mesa/jogar` vira `GmTablePage`
- links internos para `/mesa`
- `GmTablePage`: o botão Sessões navega para `/mesa`
- `SessionsDialog` passa a reaproveitar a leitura do arquivo (componente `SessionArchive`)

**Passos:**
- [ ] Testes de backend:
  - `New_session_takes_the_given_name`;
  - `Session_numbers_skip_used_ones`.
- [ ] Implementar a página e as rotas, e conferir os links (`grep "'/mesa'"`).

### Fim

- [ ] Suítes completas:
  - `dotnet test`;
  - `npx tsc -b`, `npx eslint .` e `npx vitest run`.
- [ ] Edge, com o script `r6.mjs`.
- [ ] Revisão final da branch inteira com um revisor novo.
- [ ] Commits e resumo do dia em `docs/2026-11-07-resumo.md`.
