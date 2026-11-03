# Mesa Virtual: link geral de entrada e imagens de NPC — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dois recursos novos na mesa:
- Jogadores entram por um link único e escolhem um assento livre.
- NPCs do gerador e do compêndio ganham imagem, e os tokens herdam essa imagem.

**Architecture:**
- As imagens de NPC ficam no banco, em duas tabelas no molde de `CharacterPortrait`.
- O token guarda a origem (`NpcId` ou `CompendiumNpcId`), e `SceneViews` decide a imagem na ordem do spec.
- A entrada é um singleton em memória (`TableLobby`). Ele vale junto com a troca da chave do assento (`TableSeat.Token`) ao escolher e ao liberar.

**Tech Stack:** .NET 10, EF Core (Postgres e SQLite), xUnit, FluentAssertions, SignalR, React 19, TypeScript, react-query, Vitest, PixiJS (sem mudanças no board).

**Spec:** `docs/superpowers/specs/2026-11-03-mesa-entrada-e-imagens-npc-design.md`

## Global Constraints

- **Branch:** `feature/mesa-virtual`, continuando depois de d4b5c91. Nada de push ou tag sem pedido do Yuri.
- **Ambiente:**
  - Rodar .NET com `export PATH="$USERPROFILE/.dotnet:$PATH"`.
  - Antes dos testes, pare qualquer API de dev que esteja rodando, porque ela trava o `bin/`.
- **Imagens:** PNG, JPEG, WebP ou GIF, até 2 MB (`CharacterService.PortraitMaxBytes`). O navegador reduz com `preparePortrait`.
- **Textos:**
  - Interface com `t(pt, en)`; backend com `L.T(pt, en)`.
  - Comentários e nomes de código em inglês; documentação em português.
- **Migrações:**
  - Postgres: `dotnet ef migrations add NpcPortraits --project src/CyberpunkDb.Infrastructure --startup-project src/CyberpunkDb.Api --output-dir Persistence/Migrations`.
  - SQLite: `dotnet ef migrations add NpcPortraits --project src/CyberpunkDb.Migrations.Sqlite --startup-project src/CyberpunkDb.Api --output-dir Migrations -- --provider Sqlite`.
  - As duas rodam a partir de `backend/`.
- **Porta dos jogadores:** só alcança `/api/table/play/**`. As rotas novas do jogador ficam ali e as do mestre em `/api/table/...`, fora de `/play`.
- **Testes de integração:** compartilham um `ApiFactory` (coleção `api`), então o `TableLobby` é o mesmo em todos. Todo teste da entrada começa definindo `open` pela API do mestre e só confere os assentos que ele mesmo criou.
- **Arquivos com acentos ou barra invertida:** editar com Edit/Write, nunca com heredoc.

## Review Focus

1. **Editar um token pelo inspetor apaga a origem.** O `GmPanels` monta o `TokenInput` a partir do token. Se ele esquecer `npcId` ou `compendiumNpcId`, a imagem some ao salvar. Testes: `tokenInputOf` no Vitest (Task 5) e o PUT que preserva a origem (Task 2).
2. **NPC excluído com token no mapa.** O mestre precisa continuar movendo e editando esse token. Teste na Task 2: atualizar o token mantendo o `npcId` do NPC excluído responde 200.
3. **Vazamento de chave pela entrada.** O JSON da lista nunca pode conter `Token`. Teste na Task 3: o corpo cru não contém a chave de nenhum assento criado.
4. **A entrada fecha entre listar e tocar.** O claim responde 404 com mensagem, e a tela volta para "entrada fechada" sem quebrar. Teste na Task 3 (404 com a entrada fechada) e tratamento na Task 6.
5. **Imagem trocada com o mapa aberto.** O cache de texturas do Pixi usa a URL como chave, então a URL tem que mudar. Teste na Task 2: depois de trocar a imagem, o `imageUrl` do token tem outro `v=`.

---

### Task 1: Imagens de NPC salvos e do compêndio (backend)

**Files:**
- Create:
  - `backend/src/CyberpunkDb.Domain/Entities/NpcPortrait.cs` (classes `NpcPortrait` e `CompendiumPortrait`)
  - `backend/src/CyberpunkDb.Application/Npcs/NpcPortraitService.cs`
  - as duas migrações `NpcPortraits`
  - `backend/tests/CyberpunkDb.Tests/Integration/NpcPortraitsApiTests.cs`
- Modify:
  - `Application/Abstractions/IAppDbContext.cs` e `Infrastructure/Persistence/AppDbContext.cs`: `DbSet<NpcPortrait> NpcPortraits`, `DbSet<CompendiumPortrait> CompendiumPortraits`
  - `Infrastructure/Persistence/CatalogModelConfiguration.cs`:
    - tabelas `NpcPortraits` (chave `NpcId`, FK para `Npc` com cascata) e `CompendiumPortraits` (chave `CompendiumId`, máx. 80);
    - o mesmo `Data` e `ContentType` de `CharacterPortraits`.
  - `Application/Characters/CharacterService.cs`: `PortraitTypes` vira `internal static readonly`, para ser reaproveitado.
  - `Application/Npcs/NpcService.cs`: `DuplicateAsync` copia a `NpcPortrait`.
  - `Application/DependencyInjection.cs`: `AddScoped<NpcPortraitService>()`.
  - `Api/Controllers/NpcsController.cs` e `NpcCompendiumController.cs`.

**Interfaces:**
- Produces:
  - `NpcPortrait { int NpcId; string ContentType; byte[] Data; DateTime UpdatedAt }`
  - `CompendiumPortrait { string CompendiumId; string ContentType; byte[] Data; DateTime UpdatedAt }`
  - `NpcPortraitService(IAppDbContext db, NpcCompendium compendium, SceneService scenes)`, com:
    - `Task<IReadOnlyDictionary<int, DateTime>> SavedStampsAsync(CancellationToken)`
    - `Task<IReadOnlyDictionary<string, DateTime>> CompendiumStampsAsync(CancellationToken)`
    - `Task<(byte[] Data, string ContentType, DateTime UpdatedAt)> GetSavedAsync(int npcId, CancellationToken)` e `GetCompendiumAsync(string id, CancellationToken)`: 404 se não houver imagem.
    - `Task SetSavedAsync(int npcId, string contentType, Stream content, long length, CancellationToken)` e `SetCompendiumAsync(string id, ...)`: 404 se o NPC ou o verbete não existir; 400 para tipo ou tamanho inválido.
    - `Task DeleteSavedAsync(int npcId, CancellationToken)` e `DeleteCompendiumAsync(string id, CancellationToken)`.
    - Cada Set e Delete termina chamando `scenes.RepublishTokensOfAsync(...)` (Task 2). Nesta task, crie em `SceneService` a assinatura `public Task RepublishTokensOfAsync(Func<SceneToken, bool> shows, CancellationToken ct)` com corpo `Task.CompletedTask`; a Task 2 implementa.
  - Rotas, com o mesmo formato de resposta das rotas de retrato em `CharactersController`:
    - `GET|PUT|DELETE /api/npcs/{id:int}/portrait` e `GET /api/npcs/portraits`;
    - `GET|PUT|DELETE /api/npc-compendium/{id}/portrait` e `GET /api/npc-compendium/portraits`. Declare `portraits` antes de `{id}` para não colidir.
    - PUT é multipart com o campo `file` e `RequestSizeLimit(PortraitMaxBytes + 64 KB)`; responde 204.

- [ ] **Step 1: Escrever os testes que falham** em `NpcPortraitsApiTests` (use `TableFoundationApiTests.Png`):
  - `Saved_npc_image_round_trips`: PUT, depois GET devolve os mesmos bytes e `image/png`; `GET /api/npcs/portraits` contém o id; DELETE, depois GET dá 404.
  - `Compendium_image_round_trips`: o mesmo, com o primeiro id de `GET /api/npc-compendium`.
  - `Unknown_compendium_id_is_404`: PUT em `/api/npc-compendium/nao-existe/portrait` dá 404.
  - `Duplicate_copies_the_image`: POST `/api/npcs/{id}/duplicate`, e a cópia tem a imagem.
  - `Deleting_the_npc_drops_its_image`: DELETE no NPC, e `portraits` não contém mais o id.
  - `Rejects_non_images`: PUT com `text/plain` dá 400.
  - Para criar um NPC salvo, use o mesmo helper de `NpcsApiTests`.
- [ ] **Step 2:** `dotnet test backend/CyberpunkDb.sln --filter NpcPortraitsApiTests`. Esperado: FAIL, porque as rotas não existem (404 ou 405).
- [ ] **Step 3:** Implementar entidades, configuração, migrações (as duas), o serviço, as rotas e a cópia no `DuplicateAsync`.
- [ ] **Step 4:** Rodar o filtro de novo. Esperado: PASS. Depois a suíte inteira: tudo verde.
- [ ] **Step 5: Commit** com a mensagem `Mesa virtual: imagens de NPC salvos e do compêndio`.

### Task 2: Origem e imagem dos tokens (backend)

**Files:**
- Modify:
  - `Application/Encounters/EncounterState.cs`: `CombatSnapshot.CompendiumId` (`string?`)
  - `Application/Encounters/Combat/FighterViews.cs`: `Compendium(npc)` preenche `CompendiumId = npc.Id`
  - `Application/Table/SceneState.cs`: `SceneToken.NpcId` (`int?`), `SceneToken.CompendiumNpcId` (`string?`)
  - `Application/Table/SceneContracts.cs`:
    - `TokenInput` ganha `int? NpcId = null, string? CompendiumNpcId = null` no fim;
    - `TokenView` ganha `int? NpcId, string? CompendiumNpcId` depois de `Controllable`.
  - `Application/Table/SceneViews.cs`, `Application/Table/SceneService.cs` (o construtor recebe `NpcCompendium`), `Api/Controllers/TablePlayController.cs` (o construtor recebe `NpcPortraitService`)
- Create: `Application/Table/TokenImages.cs`
- Test: `backend/tests/CyberpunkDb.Tests/Integration/TableTokenImagesApiTests.cs`

**Interfaces:**
- Consumes: `NpcPortraitService` e `RepublishTokensOfAsync` (Task 1).
- Produces:
  - `public enum TokenImageKind { Character, Npc, Compendium }`
  - `public sealed record TokenImageSource(TokenImageKind Kind, string Key, DateTime Stamp)`
  - `public sealed record TokenImages(IReadOnlyDictionary<int, DateTime> Characters, IReadOnlyDictionary<int, DateTime> Npcs, IReadOnlyDictionary<string, DateTime> Compendium)`, com `static TokenImages None`.
  - `SceneViews.ImageSource(SceneToken t, TokenImages images) -> TokenImageSource?`: null quando há `ImageMediaId` ou quando nada tem imagem. A ordem é Character, depois Npc, depois Compendium.
  - `ForGm` e `ForSeat` recebem `TokenImages` em vez de `IReadOnlySet<int>`. As URLs:
    - mestre: `/api/characters/{id}/portrait?v={ticks}`, `/api/npcs/{id}/portrait?v={ticks}` e `/api/npc-compendium/{escaped}/portrait?v={ticks}`;
    - jogador: `/api/table/play/tokens/{id}/portrait?seat={token}&v={ticks}`.
  - `SceneService.PlayerTokenImageAsync(TableSeat seat, string tokenId, CancellationToken) -> TokenImageSource`: substitui `PlayerTokenCharacterAsync` e responde 404 quando não há imagem ou o token não está visível.
  - `TablePlayController.TokenPortrait` decide pelo `Kind` e busca os bytes em `CharacterService` ou `NpcPortraitService`.
  - `PortraitsAsync` vira `ImagesAsync(SceneState, CancellationToken) -> Task<TokenImages>`. Ela busca `UpdatedAt` só dos ids usados pelos tokens sem `ImageMediaId`.
  - Validação em `TokenAsync(id, input, existing?, ct)`:
    - mais de um entre `CharacterId`, `NpcId` e `CompendiumNpcId` dá 400 com o campo `source`;
    - `NpcId` inexistente dá 400 em `npcId`, e `CompendiumNpcId` com `compendium.Find == null` dá 400 em `compendiumNpcId`;
    - as duas conferências só acontecem quando o valor difere do token existente (spec, NPC excluído).
  - `AddEncounterTokensAsync` copia `c.Snapshot?.NpcId` e `c.Snapshot?.CompendiumId`.
  - `RepublishTokensOfAsync`: carrega todas as cenas, mantém as que têm algum token com `shows(t)` e chama `PublishAsync`, sem trava, porque só lê.

- [ ] **Step 1: Escrever os testes que falham** em `TableTokenImagesApiTests`:
  - `Saved_npc_token_shows_its_image_to_gm_and_player`:
    - cria um NPC salvo com imagem, uma cena ativa e um token `{ npcId }` visível a todos;
    - para o mestre, `imageUrl` começa com `/api/npcs/{id}/portrait?v=`;
    - pelo `state` do jogador, `imageUrl` começa com `/api/table/play/tokens/` e o GET dessa URL na porta dos jogadores devolve os bytes.
  - `Compendium_fight_token_shows_the_book_npc_image`:
    - imagem no primeiro verbete com ficha;
    - encontro com `compendiumNpcId`, vinculado à cena, e depois `encounter-tokens`;
    - o token tem `compendiumNpcId` e `imageUrl` do compêndio.
  - `Hidden_token_image_is_404_for_players`: o token com `visibility: Gm` dá 404 na rota do jogador.
  - `Token_media_wins_over_npc_image`: com `imageMediaId` e `npcId` juntos, `imageUrl` é `/api/table/media/...`.
  - `Two_sources_are_rejected`: `characterId` e `npcId` juntos dão 400.
  - `Unknown_npc_is_rejected_but_a_deleted_one_keeps_working`:
    - `npcId: 999999` num token novo dá 400;
    - cria um token com um NPC válido, apaga o NPC, e um PUT no token com o mesmo `npcId` e outro rótulo dá 200, com `imageUrl` nulo.
  - `Updating_keeps_the_source`: PUT com o mesmo `npcId` mantém `npcId` na resposta.
  - `Changing_the_image_changes_the_url`: guarda o `imageUrl`, faz PUT de outra imagem no NPC, faz GET da cena e confere que o `imageUrl` mudou.
- [ ] **Step 2:** `--filter TableTokenImagesApiTests`. Esperado: FAIL, de compilação ou de asserção.
- [ ] **Step 3:** Implementar o que está em Interfaces e atualizar quem chamava `PortraitsAsync` e `PlayerTokenCharacterAsync`.
- [ ] **Step 4:** Rodar o filtro e depois a suíte inteira. Esperado: PASS.
- [ ] **Step 5: Commit** com a mensagem `Mesa virtual: tokens herdam a imagem de NPCs salvos e do compêndio`.

### Task 3: Entrada pelo link geral (backend)

**Files:**
- Create:
  - `Application/Table/TableLobby.cs`
  - `backend/tests/CyberpunkDb.Tests/Unit/Table/TableLobbyTests.cs`
  - `backend/tests/CyberpunkDb.Tests/Integration/TableLobbyApiTests.cs`
- Modify:
  - `Application/Table/TableContracts.cs`: `SeatDto` ganha `bool Claimed` no fim, mais os DTOs novos.
  - `Application/Table/SeatService.cs`: o construtor recebe `TableLobby` e `ITableNotifier`.
  - `Application/Table/ITableNotifier.cs`: `Task SeatsAsync(CancellationToken ct)`, com implementação vazia em `NullTableNotifier`.
  - `Api/Table/HubTableNotifier.cs`: envia `"seats"`, sem argumentos, ao grupo `Gm`.
  - `Api/Hubs/TableHub.cs`: em `OnConnectedAsync` e `OnDisconnectedAsync` de um assento, envia `"seats"` ao grupo `Gm`.
  - `Application/DependencyInjection.cs`: `AddSingleton<TableLobby>()`.
  - `Api/Controllers/TableController.cs` e `TablePlayController.cs`.

**Interfaces:**
- Produces:
  - `TableLobby`, thread-safe com `lock`:
    - `bool Open { get; }` e `void SetOpen(bool open)`;
    - `bool IsClaimed(int seatId)`, `bool TryClaim(int seatId)` (false se já estava pego), `void MarkClaimed(int seatId)` e `void Release(int seatId)`.
  - DTOs:
    - `LobbySeatDto(int Id, string Label, string Color, string? CharacterName, bool HasPortrait)`;
    - `LobbyDto(bool Open, IReadOnlyList<LobbySeatDto> Seats)`;
    - `LobbyStatusDto(bool Open)` e `SetLobbyRequest(bool Open)`;
    - `ClaimResultDto(string Token)`.
  - `SeatService`:
    - `AuthenticateAsync`: depois de validar, chama `lobby.MarkClaimed(seat.Id)` e, se o assento não estava pego, `notifier.SeatsAsync`.
    - `LobbyAsync(CancellationToken) -> LobbyDto`:
      - entrada fechada: `new(false, [])`;
      - aberta: assentos não revogados e não pegos, em ordem de id;
      - `CharacterName` é `Handle ?? Name` do PC, e `HasPortrait` diz se existe `CharacterPortrait`.
    - `LobbyPortraitAsync(int seatId, CancellationToken) -> (byte[] Data, string ContentType, DateTime UpdatedAt)`: 404 se a entrada estiver fechada, o assento estiver pego ou revogado ou o PC não tiver retrato.
    - `ClaimAsync(int seatId, CancellationToken) -> ClaimResultDto`:
      - entrada fechada: `NotFoundException` com `L.T("A entrada está fechada. Peça ao mestre para abrir.", "The table is closed. Ask the GM to open it.")`;
      - assento inexistente: 404;
      - assento revogado, ou `TryClaim` falso: `ConflictException` com `L.T("Esse personagem acabou de ser escolhido.", "That character was just taken.")`;
      - caso contrário, `Token = NewToken()` e salva. Se o salvamento falhar, `lobby.Release`. No fim, `notifier.SeatsAsync`.
    - `ReleaseAsync(int id, CancellationToken) -> SeatDto`: troca a chave, salva, chama `lobby.Release` e `notifier.SeatsAsync`.
    - `ToDto` preenche `Claimed = lobby.IsClaimed(s.Id)`. Por isso `ToDto` deixa de ser estático.
  - Rotas do jogador, sem `X-Seat`:
    - `GET /api/table/play/lobby`;
    - `GET /api/table/play/lobby/{seatId:int}/portrait` (`Cache-Control: no-store`);
    - `POST /api/table/play/lobby/{seatId:int}/claim`: depois de `ClaimAsync`, chama `presence.Disconnect(seatId)`. `TablePlayController` passa a receber `TablePresence`.
  - Rotas do mestre:
    - `GET /api/table/lobby` devolve `LobbyStatusDto`;
    - `PUT /api/table/lobby` com `SetLobbyRequest` devolve `LobbyStatusDto`;
    - `POST /api/table/seats/{id:int}/release` devolve `SeatDto` e depois chama `presence.Disconnect(id)`.

- [ ] **Step 1: Escrever os testes que falham.**
  - Unitários (`TableLobbyTests`):
    - `Starts_closed`;
    - `TryClaim_wins_once`: a segunda chamada devolve false;
    - `Release_frees_the_seat`;
    - `Parallel_claims_have_one_winner`: 50 tarefas `TryClaim(1)` dão exatamente um `true`.
  - Integração (`TableLobbyApiTests`, cada teste começa com `PUT /api/table/lobby`):
    - `Closed_lobby_lists_nothing_and_refuses_claims`: `open: false` e `seats` vazio; claim dá 404; retrato dá 404.
    - `Open_lobby_lists_free_seats_without_tokens`:
      - cria dois assentos;
      - o corpo cru da lista contém os dois rótulos e não contém nenhum dos `token`;
      - um assento revogado não aparece.
    - `Claim_rotates_the_token_and_hides_the_seat`:
      - o claim dá 200 com uma chave nova;
      - `state` com a chave nova dá 200 e com a antiga dá 401;
      - o assento sai da lista;
      - o `seats` do mestre mostra `claimed: true`.
    - `Second_claim_conflicts`: dois `POST claim` simultâneos (`Task.WhenAll`) dão um 200 e um 409.
    - `Individual_link_marks_the_seat_taken`: `GET /api/table/play/me` com a chave original tira o assento da lista.
    - `Release_returns_the_seat_and_kills_the_key`: depois do claim, release; a chave do claim dá 401 e o assento volta à lista.
    - `Lobby_controls_are_gm_only`: `PUT /api/table/lobby` e `POST /api/table/seats/1/release` pela porta dos jogadores dão 404.
- [ ] **Step 2:** `--filter "TableLobby"`. Esperado: FAIL.
- [ ] **Step 3:** Implementar o que está em Interfaces. Atualizar os testes que constroem ou comparam `SeatDto`, se houver.
- [ ] **Step 4:** Rodar o filtro e depois a suíte inteira. Esperado: PASS.
- [ ] **Step 5: Commit** com a mensagem `Mesa virtual: entrada pelo link geral com escolha de assento`.

### Task 4: Imagens de NPC nas telas do gerador e do compêndio

**Files:**
- Create:
  - `frontend/src/api/npcImages.ts`
  - `frontend/src/components/ui/PortraitField.tsx`, com estilos no CSS module da própria pasta ou reaproveitando `styles.portrait` do padrão de `CharacterUi.Portrait`
- Modify:
  - `features/enemies/EnemyGeneratorPage.tsx`: o campo aparece quando `currentId != null`; senão, aparece a dica "Salve o NPC para pôr uma imagem".
  - `features/enemies/SheetPreview.tsx` (`NpcLibrary` mostra a miniatura de 28 px quando há stamp)
  - `features/npc-compendium/NpcDossierPage.tsx` (campo no cabeçalho do dossiê)
  - `features/npc-compendium/NpcCompendiumPage.tsx` (`NpcCard` mostra a miniatura no lugar do ícone)

**Interfaces:**
- Consumes: as rotas da Task 1.
- Produces:
  - `npcImageApi`:
    - `savedStamps(): Promise<Record<string, string>>` e `compendiumStamps()`;
    - `savedUrl(id: number, stamp: string)` e `compendiumUrl(id: string, stamp: string)`;
    - `uploadSaved(id, file)` e `uploadCompendium(id, file)`, com PUT multipart e o mesmo tratamento de erro de `characterApi.uploadPortrait`;
    - `removeSaved(id)` e `removeCompendium(id)`.
  - `npcImageKeys = { saved: ['npc-images','saved'], compendium: ['npc-images','compendium'] }`.
  - `PortraitField({ url, alt, onUpload(file: File): Promise<void>, onRemove(): Promise<void>, size?: number })`: passa por `preparePortrait`, mostra `PortraitError` e `problemMessage` num toast e invalida a query de stamps no chamador.

- [ ] **Step 1:** Implementar a API e o componente. Ligar o componente nas quatro telas, invalidando `npcImageKeys` depois de enviar ou remover.
- [ ] **Step 2:** `npm run lint` e `npx tsc -b`, em `frontend/`. Esperado: nenhum aviso novo.
- [ ] **Step 3:** Conferir no navegador, com o app em dev: subir uma imagem num NPC salvo e num do compêndio; a miniatura aparece na lista e some ao remover.
- [ ] **Step 4: Commit** com a mensagem `Imagens de NPC no gerador e no compêndio`.

### Task 5: Escolha da origem no painel da cena (frontend da mesa)

**Files:**
- Create: `frontend/src/features/table/sources.ts` e `sources.test.ts`
- Modify:
  - `api/tableTypes.ts`: `TokenView` e `TokenInput` ganham `npcId: number | null` e `compendiumNpcId: string | null`; `Seat` ganha `claimed: boolean`.
  - `features/table/GmPanels.tsx`: "Novo token" e `TokenInspector`.
  - `features/table/GmTablePage.tsx`: carrega `api.npcs()` e `npcCompendiumApi.list()` para as fontes.

**Interfaces:**
- Produces, em `sources.ts`:
  - `type TokenSourceKind = 'character' | 'npc' | 'compendium'`;
  - `interface TokenSource { kind: TokenSourceKind; id: string; name: string }`;
  - `buildSources(characters, npcs, compendium): TokenSource[]`: PCs usam `handle ?? name`. O compêndio entra inteiro, porque NPCs só de perfil também podem ter token.
  - `matchSources(list, query, limit = 20): TokenSource[]`: sem diferenciar maiúsculas e acentos, nome que começa com a busca vem antes de nome que só contém.
  - `sourceFields(src: TokenSource | null): { characterId: number | null; npcId: number | null; compendiumNpcId: string | null }`.
  - `sourceOf(token, list): TokenSource | null`.
  - `tokenInputOf(tk: TokenView): TokenInput`, movido do `GmPanels` para cá. Ele é usado pelo inspetor e inclui `npcId` e `compendiumNpcId`.
- O "Novo token" troca o `<select>` de personagem por um campo de busca com uma lista de até 20 resultados e o grupo à direita ("PC", "NPC" ou "Compêndio"). Ao escolher, o rótulo vazio é preenchido com o nome. O inspetor mostra `Origem: <grupo>: <nome>`.

- [ ] **Step 1: Escrever os testes que falham** em `sources.test.ts`:
  - `matchSources` ignora acentos (`"jose"` encontra `"José"`) e põe o prefixo antes de quem só contém;
  - `sourceFields` de um npc dá `{ characterId: null, npcId: 7, compendiumNpcId: null }`;
  - `tokenInputOf` preserva `npcId` e `compendiumNpcId`;
  - `sourceOf` acha o compêndio pelo id.
- [ ] **Step 2:** `npx vitest run src/features/table/sources.test.ts`. Esperado: FAIL.
- [ ] **Step 3:** Implementar `sources.ts` e trocar o painel e o inspetor.
- [ ] **Step 4:** Vitest inteiro, lint e `tsc -b`. Esperado: PASS e nenhum aviso novo.
- [ ] **Step 5: Commit** com a mensagem `Mesa virtual: token a partir de PC, NPC salvo ou do compêndio`.

### Task 6: Tela de entrada, aba Convites e documentação

**Files:**
- Create:
  - `frontend/src/features/table/lobby.ts` e `lobby.test.ts`
  - `frontend/src/features/table/LobbyPage.tsx`
- Modify:
  - `api/table.ts`:
    - `lobbyApi = { list(), claim(seatId), portraitUrl(seatId) }`;
    - `playerApi(seat).me()`;
    - `tableApi.lobby()`, `tableApi.setLobby(open)` e `tableApi.releaseSeat(id)`;
    - `tableKeys.lobby` e `tableKeys.lobbyStatus`.
  - `api/tableTypes.ts`: `Lobby`, `LobbySeat`, `ClaimResult`.
  - `features/table/useTableHub.ts`: o handler `seats?: () => void`.
  - `App.tsx`: dentro do ramo `/mesa/j/`, `/mesa/j/` e `*` vão para `LobbyPage`; `/mesa/j/:token` continua em `PlayerTablePage`.
  - `PlayerTablePage.tsx`:
    - com `state` válido, `rememberSeat(token)`;
    - com 401, `forgetSeat()`, e o card "Convite inválido" ganha o botão "Escolher personagem", que navega para `/mesa/j/`.
  - `GmPanels.tsx`, em `InvitesTab`:
    - bloco "Link geral" com a URL do túnel, um botão de copiar e um `Switch` "Entrada aberta". O texto "não mande este endereço" vira "Mande este link aos jogadores; eles escolhem o personagem enquanto a entrada estiver aberta.";
    - cada assento mostra "livre", "na mesa" ou "conectado" (de `claimed` e `onlineSeats`) e um botão "Liberar", visível quando `claimed`.
  - `GmTablePage.tsx`: o handler `seats` do hub invalida `tableKeys.seats` e `tableKeys.online`.
  - `README.md` e `docs/DESENVOLVIMENTO.md`: na seção Mesa Virtual, incluir o link geral, a entrada, o Liberar e as imagens de NPC; atualizar a contagem de testes.

**Interfaces:**
- Produces, em `lobby.ts`:
  - `SEAT_KEY = 'ncdb.table.seat'`;
  - `rememberSeat(token)`, `savedSeat(): string | null` e `forgetSeat()`, todos com `try/catch` em volta do `localStorage`;
  - `claimErrorMessage(e: unknown): { closed: boolean; text: string }`: 404 dá `closed: true`, 409 dá o texto "Esse personagem acabou de ser escolhido", e o resto usa `problemMessage`.
- `LobbyPage`:
  1. Se `savedSeat()` existir, chama `playerApi(saved).me()`. Se der certo, `navigate('/mesa/j/' + saved, { replace: true })`; se não, `forgetSeat()`.
  2. Lê `useQuery(lobbyApi.list)` com `refetchInterval: 5000`. Com `open: false`, mostra o card "A entrada está fechada, peça ao mestre".
  3. Com a entrada aberta, mostra uma grade de cartões com o retrato (`lobbyApi.portraitUrl`) ou um círculo com a cor e as `initials()` de `tokens.ts`, o `characterName` e o `label`.
  4. Ao tocar: claim, depois `rememberSeat`, depois navega. Em caso de erro, `claimErrorMessage`, toast e `refetch`.
  
  Visual igual ao `styles.gate` e `gateCard` de `Table.module.css`, que funciona no celular.

- [ ] **Step 1: Escrever os testes que falham** em `lobby.test.ts`:
  - `rememberSeat` e `savedSeat` fazem ida e volta, e `forgetSeat` limpa;
  - com `localStorage` lançando erro (`vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error() })`), `savedSeat()` devolve null;
  - `claimErrorMessage(new ApiError(409, {...}))` não tem `closed`, e com 404 tem `closed: true`.
- [ ] **Step 2:** `npx vitest run src/features/table/lobby.test.ts`. Esperado: FAIL.
- [ ] **Step 3:** Implementar o que está nos Files acima.
- [ ] **Step 4:** Vitest, lint e `tsc -b`, e depois `dotnet test` inteiro. Esperado: tudo verde.
- [ ] **Step 5: Ponta a ponta no Edge**, com playwright-core nos scripts do scratchpad e dev nas portas 5080 e 5081:
  1. Mestre: criar dois assentos com PCs e abrir a entrada.
  2. Jogador A (porta 5081) abre `/`, cai em `/mesa/j/`, toca no PC 1 e entra.
  3. Jogador B vê só o PC 2.
  4. Mestre: Liberar o PC 1. O A é derrubado e, ao recarregar, cai na entrada.
  5. Um token de NPC com imagem aparece com a imagem no A.
  6. Captura no viewport de celular.
- [ ] **Step 6: Commit** com a mensagem `Mesa virtual: tela de entrada, controles na aba Convites e documentação`.

### Task 7: Exe e verificação final

- [ ] **Step 1:** `deploy/desktop/publicar.ps1` (v1.7.0, mesma versão, porque ainda não foi lançada). Esperado: `artifacts\NightCityItemDB.exe`, o zip e o `.sha256`.
- [ ] **Step 2:** Rodar o exe com dados temporários em portas livres (como antes: 5190 e 5191, com `%LOCALAPPDATA%` apontado para uma pasta temporária). Conferir:
  - a migração `NpcPortraits` aplicada no SQLite;
  - o fluxo de entrada;
  - a imagem de um NPC no token.
- [ ] **Step 3:** Atualizar a memória `virtual-table-plan.md` com o estado final.
