# Mesa Virtual: link geral de entrada e imagens de NPC — design

**Data:** 2026-11-03
**Status:** aprovada em conversa, aguardando revisão do texto

## Objetivo

Duas melhorias na Mesa Virtual (v1, branch `feature/mesa-virtual`):

1. **Link geral.** O mestre manda um único link para todos. Antes de entrar, cada jogador escolhe um dos personagens que o mestre deixou disponíveis. Os links individuais continuam existindo.
2. **Imagens de NPC.** NPCs salvos no Gerador de Inimigos e NPCs do compêndio ganham uma imagem. Quando o mestre põe PCs e NPCs no mapa, os tokens já saem com a imagem de cada um.

Sucesso significa:

- Com a entrada aberta, um jogador no celular abre o endereço do túnel, toca no seu PC e entra na mesa sem receber link próprio.
- Duas pessoas nunca ficam com o mesmo assento.
- Com a entrada fechada, o link geral não mostra nem entrega nada.
- O token de um NPC com imagem mostra essa imagem para o mestre e para os jogadores que podem ver o token, venha ele de um combate ou do painel "Novo token".

## Decisões tomadas

| Pergunta | Decisão |
|---|---|
| Quem escolhe o personagem no link geral | O jogador, entre os assentos livres. Só PCs (assentos), nunca NPCs |
| Proteção do link geral | Interruptor Entrada aberta/fechada, mais o botão Liberar. Sem aprovação do mestre por pedido |
| Quem põe NPCs no mapa | O mestre (painel da cena ou "Criar tokens do combate") |
| Quais NPCs têm imagem | NPCs salvos do gerador e NPCs do compêndio. Genéricos continuam com iniciais ou imagem posta no token |
| Onde fica a imagem do NPC | No banco, como o retrato dos PCs (opção A) |
| Onde fica "quem pegou qual assento" | Na memória do app. Zera quando o app reinicia, e a entrada começa fechada (opção A) |

## Fora do escopo

- Jogador escolher um NPC aliado para controlar.
- Aprovação do mestre para cada entrada.
- Imagem de grupo para inimigos genéricos.
- Barra de vida em token criado pelo painel "Novo token" (continua vindo de "Criar tokens do combate").
- Recuperar a origem de NPCs do compêndio que já estão em combates salvos antes desta mudança.
- Levar a imagem do NPC no arquivo JSON de exportação do gerador.

## Parte 1: imagens de NPC

### Dados

Duas tabelas novas, no mesmo molde de `CharacterPortrait` (bytes no banco, migrações em Postgres e SQLite):

- `NpcPortrait`: `NpcId` (chave e FK para `Npcs`, apaga junto), `ContentType`, `Data`, `UpdatedAt`.
- `CompendiumPortrait`: `CompendiumId` (texto, chave; o id do verbete no JSON embutido), `ContentType`, `Data`, `UpdatedAt`.

Mesmas regras do retrato do PC: PNG, JPEG, WebP ou GIF, até 2 MB. O navegador reduz a imagem antes de enviar (`prepareImage` com os limites de retrato).

### API (porta do mestre)

- `GET/PUT/DELETE /api/npcs/{id}/portrait`.
- `GET/PUT/DELETE /api/npc-compendium/{id}/portrait`. O PUT recusa um id que não existe no compêndio.
- Os resumos e detalhes de NPC salvo e o resumo e dossiê do compêndio ganham `hasPortrait`.
- `NpcService.DuplicateAsync` copia a imagem. Excluir o NPC apaga a imagem (cascata).

### Combate

- `CombatSnapshot` ganha `CompendiumId` (string, opcional), preenchido por `FighterViews.Compendium`. Fica no JSON do encontro, então não precisa de migração.
- O snapshot de NPC salvo já guarda `NpcId`.

### Tokens

- `SceneToken` ganha `NpcId?` e `CompendiumNpcId?`, salvos no JSON da cena.
- `TokenInput` aceita os dois, e o servidor recusa um id inexistente.
- No máximo um vínculo de origem por token: personagem, NPC salvo ou NPC do compêndio. Mandar mais de um dá erro de validação.
- `AddEncounterTokensAsync` copia, de cada combatente, `Snapshot.NpcId` e `Snapshot.CompendiumId` para o token.
- **Ordem da imagem**, igual para mestre e jogador, decidida no servidor (`SceneViews`):
  1. `ImageMediaId` do token;
  2. retrato do PC (`CharacterId`);
  3. imagem do NPC salvo (`NpcId`);
  4. imagem do NPC do compêndio (`CompendiumNpcId`);
  5. sem imagem (o cliente desenha as iniciais).
- A existência das imagens é consultada a cada montagem da cena, como hoje já se faz com os retratos de PC (`PortraitsAsync` passa a devolver os três conjuntos). Trocar a imagem do NPC muda os tokens na próxima vez que a cena for enviada.
- Para que a imagem nova chegue aos clientes, a URL leva a data da imagem (`?v=<ticks>`), e quem estiver com a cena aberta recebe a cena de novo quando a imagem muda. Para isso, os serviços de retrato chamam o notificador da mesa: a cena ativa e as cenas abertas pelo mestre são reenviadas.

### Rotas de imagem

- **Mestre:** usa as rotas da porta do mestre (`/api/characters/{id}/portrait`, `/api/npcs/{id}/portrait`, `/api/npc-compendium/{id}/portrait`).
- **Jogador:** a rota que já existe, `GET /api/table/play/tokens/{tokenId}/portrait?seat=`, passa a servir a imagem que o token mostra, seguindo a mesma ordem (menos `ImageMediaId`, que já tem rota própria). Ela continua aceitando só tokens visíveis ao assento na cena ativa.

### Telas

- **Gerador de Inimigos:** com um NPC salvo aberto, aparece um campo "Imagem" com enviar, trocar e tirar. Antes do primeiro salvamento, o campo explica que é preciso salvar primeiro. A biblioteca de NPCs mostra a miniatura.
- **Compêndio:** o dossiê ganha o mesmo campo "Imagem", e os cartões da lista mostram a miniatura de quem tem uma.
- **Mesa (painel da cena, "Novo token"):** o seletor de personagem vira uma busca agrupada com PCs, NPCs salvos e NPCs do compêndio. Ao escolher, o rótulo do token vem preenchido com o nome, e o token sai com o vínculo de origem certo.
- **Inspetor do token:** mostra a origem do token, por exemplo "NPC: Royce" ou "Compêndio: Adam Smasher". A imagem posta direto no token continua tendo prioridade.

## Parte 2: link geral de entrada

### Estado

Um singleton `TableLobby` (Application) guarda na memória:

- `Open`: começa `false` sempre que o app abre.
- `Claimed`: o conjunto de ids de assentos já pegos.
- Uma trava (`lock`) que torna a escolha atômica.

Nada disso vai para o banco. A chave do assento continua sendo a coluna `TableSeat.Token`, que é trocada na escolha e no Liberar.

### Regras

- **Assentos que aparecem na entrada:** não revogados, não pegos e com a entrada aberta.
- **Escolher (claim):**
  - Com a entrada fechada, o servidor recusa (404 com mensagem).
  - Se o assento já foi pego ou foi revogado, o servidor responde com conflito (409).
  - Caso contrário, marca o assento como pego, gera uma chave nova, salva, derruba conexões abertas com a chave antiga (`TablePresence`) e devolve a chave nova.
  - A troca da chave garante que só um aparelho fica com o assento, mesmo se um link individual antigo dele já tiver circulado.
- **Entrar por link individual** (`GET /api/table/play/me` válido) também marca o assento como pego.
- **Liberar (mestre):** gera uma chave nova, derruba as conexões do assento e o tira de `Claimed`, para que ele volte para a lista.
- **Abrir e fechar** a entrada não mexe em quem já está na mesa.

### API

Na porta dos jogadores, liberada pelo `PlayerSurfaceGuard` porque fica dentro de `/api/table/play`:

- `GET /api/table/play/lobby` devolve `{ open, seats: [{ id, label, color, characterName?, hasPortrait }] }`. Nunca devolve chaves.
- `GET /api/table/play/lobby/{seatId}/portrait` devolve o retrato do PC do assento. Só funciona com a entrada aberta e o assento livre.
- `POST /api/table/play/lobby/{seatId}/claim` devolve `{ token }`.

Essas três rotas não pedem `X-Seat`.

Na porta do mestre, sob a mesma proteção de Host e Origin das outras rotas da mesa:

- `GET /api/table/lobby` devolve `{ open }`.
- `PUT /api/table/lobby` com `{ open }`.
- `POST /api/table/seats/{id}/release`.
- `SeatDto` ganha `claimed` e `connected` (este vem do `TablePresence`).
- O mestre recebe pelo hub um evento `seats` quando alguém escolhe um assento, entra, sai ou é liberado, e a aba Convites atualiza sozinha.

### Telas

- **Jogador, `/mesa/j/` sem código:** é a tela de entrada.
  - Se o `localStorage` tem uma chave guardada, a tela testa `me`. Se a chave valer, vai direto para `/mesa/j/<chave>`.
  - Com a entrada fechada, mostra "A entrada está fechada, peça ao mestre" e consulta de novo a cada 5 s.
  - Com a entrada aberta, mostra um cartão para cada assento livre, com retrato ou cor e iniciais, o nome do PC e o rótulo.
  - Ao tocar num cartão: claim, grava a chave no `localStorage` e vai para `/mesa/j/<chave>`.
  - Se der 409, mostra "Esse personagem acabou de ser escolhido" e recarrega a lista.
- **Jogador, `/mesa/j/<chave>`:** ao validar a chave, grava-a no `localStorage`. Se a chave for recusada (assento liberado ou revogado), apaga a chave guardada e oferece um botão "Escolher personagem", que leva à tela de entrada.
- **Mestre, aba Convites:**
  - um bloco "Link geral" com o endereço do túnel, o botão copiar e o interruptor Entrada aberta/fechada;
  - em cada assento, o estado (livre, na mesa ou conectado) e o botão Liberar;
  - os links individuais continuam como estão.

## Erros e casos de borda

- **Túnel reiniciado no meio da sessão:** o endereço muda e o `localStorage` antigo não vale no endereço novo. O jogador cai na tela de entrada, e o seu assento continua pego. O mestre clica em Liberar.
- **App reiniciado:** todos os assentos voltam a ficar livres e a entrada começa fechada. As chaves antigas continuam válidas no banco. Quem ainda tiver um link individual entra normalmente, e o assento é marcado como pego.
- **Assento revogado:** some da entrada, e o claim responde com conflito.
- **Duas escolhas ao mesmo tempo:** a trava garante que só uma vence.

## Testes

**xUnit:**

- Entrada:
  - com a entrada fechada, listar, ver retrato e escolher respondem 404;
  - a lista nunca contém chaves e não mostra assentos pegos nem revogados;
  - duas escolhas simultâneas do mesmo assento dão uma vitória e um 409;
  - a chave nova funciona e a antiga dá 401;
  - Liberar invalida a chave e devolve o assento à lista;
  - o `me` por link individual marca o assento como pego;
  - as rotas de controle não respondem na porta dos jogadores.
- Imagens:
  - enviar, ler e apagar a imagem de NPC salvo e de NPC do compêndio;
  - um id inexistente do compêndio dá 404;
  - duplicar o NPC copia a imagem;
  - um token de combatente do compêndio ou de NPC salvo traz `imageUrl` para o mestre e para o jogador;
  - o jogador recebe a imagem pela rota do token, e um token oculto dá 404;
  - a ordem de prioridade é respeitada;
  - um token com dois vínculos de origem é recusado.

**Vitest:** a decisão da tela de entrada (chave guardada, entrada fechada, lista, conflito).

**Manual (Edge real e exe publicado):**

1. Subir imagens num NPC do gerador e num NPC do compêndio.
2. Criar tokens do combate e conferir as imagens.
3. Abrir o túnel, abrir a entrada e entrar pelo celular escolhendo o PC.
4. Tentar o mesmo PC em outro navegador.
5. Liberar e entrar de novo.

## Arquivos principais

- **Novos:**
  - `Domain/Entities/NpcPortrait.cs`, `CompendiumPortrait.cs`
  - migrações em Postgres e SQLite
  - `Application/Table/TableLobby.cs`
  - `Application/Npcs/NpcPortraitService.cs`
  - `frontend/src/features/table/LobbyPage.tsx` (ou dentro de `PlayerTablePage.tsx`), mais `lobby.ts` com teste
- **Alterados:**
  - `SceneState.cs`, `SceneContracts.cs`, `SceneViews.cs`, `SceneService.cs`
  - `EncounterState.cs`, `FighterViews.cs`
  - `NpcService.cs`
  - `SeatService.cs`, `TableContracts.cs`
  - `TableController.cs`, `TablePlayController.cs`, `NpcsController.cs`, `NpcCompendiumController.cs`
  - `HubTableNotifier.cs`
  - frontend: `GmPanels.tsx`, `PlayerTablePage.tsx`, `SheetPreview.tsx` / `EnemyGeneratorPage.tsx`, `NpcDossierPage.tsx`, `NpcCompendiumPage.tsx`, `api/*`
  - docs: `README.md` e `docs/DESENVOLVIMENTO.md`
