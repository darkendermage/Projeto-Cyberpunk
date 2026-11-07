# Mesa Virtual, rodada 5: imagens sobre o mapa (camadas) — design

**Data:** 2026-11-07
**Status:** aprovada em conversa, aguardando revisão do texto
**Branch:** `feature/mesa-imagens` (a partir de `feature/mesa-rodada4`, ainda não mesclada)

## Objetivo

O mestre e os jogadores colocam mais imagens por cima do mapa da cena, como móveis, tapetes, decoração e marcas. As imagens ficam numa aba nova, **"Imagens"**, que funciona como as camadas do Photoshop: lista ordenada, olho, cadeado e opacidade.

Sucesso significa:

- **Mestre:** adiciona uma imagem, arrasta, redimensiona pelas alças, gira e muda a ordem em relação às outras imagens. Os jogadores veem o resultado na hora.
- **Jogador:** envia uma imagem pelo túnel e mexe só nas suas.
- **Cadeado:** com a imagem travada, ninguém mexe nela até destravar; o mestre sempre pode destravar.
- **Olho do mestre:** a imagem some das telas dos jogadores, como na preparação de uma cena.
- **Tokens:** as imagens nunca ficam por cima dos tokens.

## Decisões tomadas

| Pergunta | Decisão |
|---|---|
| Jogadores podem pôr imagens | Sim, livre. O mestre vê tudo e pode ocultar, travar ou apagar qualquer uma. Há limites de tamanho e de quantidade. |
| "O mestre ter ou não a opção de redimensionar" | Cadeado por imagem. Travada: ninguém move nem redimensiona. Destravada: o dono (e sempre o mestre) arrasta e redimensiona pelas alças. |
| Onde as imagens ficam em relação aos tokens | Só abaixo dos tokens. Entre elas, a ordem é a da lista. |
| Modelo de dados | Uma lista própria por cena (`SceneState.Images`), não tokens. Tokens têm barra de HP, anel de turno e vínculo com o combate. |

## Fora do escopo

- Imagens acima dos tokens (telhado, fumaça).
- Modos de mistura, filtros, recorte e máscara.
- Grupos de camadas.
- Copiar imagens entre cenas.
- Biblioteca de imagens reaproveitáveis.
- Aprovação do mestre antes de a imagem de um jogador aparecer.
- Animação (GIF tocando como camada). O GIF aparece parado no primeiro quadro, como o fundo já faz.

---

## Dados

`SceneState.Images: List<SceneImage>` (padrão vazio, então cenas antigas carregam sem a lista):

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | string | Gerado pelo servidor. |
| `MediaId` | Guid | Arquivo já enviado (`MediaService`). |
| `Name` | string | Até 60 caracteres. Vem preenchido com o nome do arquivo. |
| `X`, `Y` | double | Centro da imagem, em células. |
| `Width`, `Height` | double | Em células, livres (frações), de 0,25 a 200. |
| `Rotation` | double | Em graus, 0 a 360. |
| `Opacity` | double | De 0,1 a 1. |
| `Order` | int | Posição na lista: maior desenha por cima. O servidor renumera de 0 a n−1 a cada mudança. |
| `Locked` | bool | Cadeado. |
| `LockedByGm` | bool | Cadeado posto pelo mestre: só o mestre tira. |
| `Hidden` | bool | Oculta dos jogadores (só o mestre usa). |
| `OwnerSeatId` | int? | Assento do jogador que pôs a imagem. Nulo quando foi o mestre. |

**Limites:**
- **Total:** no máximo 200 imagens por cena (`MaxImages`).
- **Jogador:** no máximo 10 imagens dele por cena (`MaxImagesPerSeat`).
- **Arquivo do jogador:** até 2 MB, já reduzido no navegador como o retrato (`preparePortrait`, lado maior até 1024 px).
- **Arquivo do mestre:** valem os limites de mapa que já existem (`MAP_LIMITS`, até 4096 px e 12 MB; o servidor aceita até 15 MB).

## Desenho no tabuleiro

**Ordem das camadas do Pixi:**
- **Hoje:** fundo → grade → cenário → áreas → tokens.
- **Passa a ser:** fundo → **imagens** → grade → cenário → áreas → tokens.

**Como cada imagem é desenhada:**
- `Sprite` com âncora no centro, girado e com a opacidade da imagem.
- A ordem é dada por `Order`.
- A textura é carregada com o `loadTexture` que já existe.

**Visibilidade por tela:**
- **Jogadores:** recebem só as imagens com `Hidden = false`.
- **Mestre:** recebe todas. As ocultas aparecem semitransparentes, como já acontece com os tokens ocultos.

**Seleção:**
- Clicar num ponto da imagem sem token em cima seleciona a imagem. O token tem prioridade.
- A seleção de imagens convive com a de tokens e áreas, pelo mesmo `Selection`, que ganha `images`.
- Delete, Esc e Ctrl+A valem para imagens também, respeitando as permissões.

**Alças** (só na imagem selecionada, destravada e que a tela pode editar):
- **Cantos:** quatro alças redimensionam a partir do canto oposto, mantendo a proporção. Shift solta a proporção.
- **Giro:** uma alça acima da imagem gira em passos de 15°. Shift deixa o giro livre.
- **Arrastar o corpo:** move a imagem, alinhando o centro a meia célula. Alt solta o alinhamento.
- **Ao vivo:** enquanto alguém arrasta, os outros veem a imagem deslizar pelo hub, como o `tokenDrag`, num evento novo `imageDrag` com posição, tamanho e giro.
- **Ao soltar:** a mudança é salva por uma rota só (`PUT …/images/{id}`).

## Permissões

| Ação | Mestre | Dono (jogador) | Outro jogador |
|---|---|---|---|
| Adicionar | Sim | Sim (até 10 por cena) | — |
| Mover, redimensionar, girar, opacidade, renomear | Sempre (destrava se precisar) | Se não travada | Não |
| Mudar a ordem | Qualquer uma | Só as suas, trocando de lugar com as vizinhas da lista | Não |
| Travar ou destravar | Qualquer uma. O cadeado do mestre só ele tira. | As suas, quando o cadeado não é do mestre | Não |
| Ocultar dos jogadores | Sim | Não | Não |
| Apagar | Qualquer uma | As suas, se não travadas | Não |

**Imagem oculta:** o servidor não manda para os jogadores nem a imagem nem a URL dela.

**Imagens do jogador sem cena ao vivo:** as rotas do jogador valem só na cena ao vivo, como as áreas e tokens dele hoje.

## API

**Mestre** (`TableController`, cena `{id}`):
- `POST /api/table/scenes/{id}/images { mediaId, name?, x, y, width?, height? }`. Sem tamanho, a imagem cabe em 4×4 células mantendo a proporção do arquivo.
- `PUT /api/table/scenes/{id}/images/{imageId} { name, x, y, width, height, rotation, opacity, locked, hidden }`.
- `POST /api/table/scenes/{id}/images/{imageId}/order { above: imageId | null }`: põe a imagem logo abaixo, na lista, da imagem `above`, que passa a ser desenhada logo por cima dela. Null põe no topo da lista (desenhada por cima de todas).
- `DELETE /api/table/scenes/{id}/images/{imageId}`.
- O upload continua sendo `POST /api/table/media`.

**Jogador** (`TablePlayController`, cena ao vivo):
- `POST /api/table/play/media`: upload até 2 MB, PNG, JPEG, WebP ou GIF. Os arquivos ficam no mesmo `MediaService`.
- `POST /api/table/play/images`, `PUT /api/table/play/images/{imageId}`, `POST /api/table/play/images/{imageId}/order`, `DELETE /api/table/play/images/{imageId}`: os mesmos corpos do mestre, sem `hidden`, e com as regras da tabela de permissões.

**Concorrência:** as mudanças de imagem seguem as de token (última gravação vale) e passam pela trava de cena (`SceneLocks`). Cada mudança publica a cena para o mestre e para cada assento com a visão dele (`SceneViews`).

**Respostas:**
- `SceneView.Images: IReadOnlyList<SceneImageView>`, com URL de mídia já resolvida e `editable` calculado para quem está vendo.
- Quem não pode editar recebe 403. Uma imagem que não existe ou está oculta para o jogador recebe 404. Limites estourados recebem 400 com mensagem clara (PT/EN).

## Aba "Imagens"

Aparece para o mestre e para os jogadores, ao lado das outras abas do painel direito.

- **Topo:**
  - Botão "Adicionar imagem": escolhe o arquivo, que vai para o centro da área visível do mapa.
  - Mostra quanto o jogador já usou do limite ("3/10").
- **Lista**, de cima (desenhada por cima) para baixo. Cada linha tem:
  - miniatura e nome (renomeia com dois cliques);
  - 👁 oculta, só o mestre;
  - 🔒 cadeado (o do mestre aparece diferente);
  - opacidade (controle deslizante, salva ao soltar);
  - ↑↓ para a ordem, mais arrastar e soltar entre as linhas;
  - apagar, com confirmação;
  - a etiqueta do dono ("Mestre" ou o nome do assento).
- **Clique na linha:** seleciona a imagem no mapa, e selecionar no mapa destaca a linha.
- **Jogador:** vê as imagens visíveis de todos, mas só as dele têm os controles ativos. As outras mostram a dica "Imagem de {dono}".

## Testes

- **Backend:**
  - Adicionar, mover, redimensionar (limites de 0,25 a 200), girar, opacidade (0,1 a 1) e renomear.
  - Ordem: renumeração e `above`.
  - Permissões: dono, outro jogador, mestre, cadeado comum, cadeado do mestre.
  - Oculta: some da visão do jogador, e a URL não vaza.
  - Limites: 10 por jogador, 200 por cena, upload do jogador de 2 MB e tipos aceitos.
  - Cena antiga sem `images` carrega.
- **Vitest** (funções puras):
  - tamanho inicial que cabe em 4×4 com a proporção;
  - redimensionar por um canto (com e sem proporção);
  - giro em passos de 15°;
  - alinhamento a meia célula;
  - teste de clique dentro de um retângulo girado;
  - ordem da lista e o `above` de "subir" e "descer".
- **Edge** (mestre e dois jogadores, banco descartável):
  - O jogador A envia uma imagem, que aparece para B e para o mestre.
  - B não consegue mexer nela.
  - O mestre redimensiona, trava e oculta, e a imagem some para os jogadores.

## Entrega

- Commits por parte, nesta ordem: backend (dados, rotas e permissões), tabuleiro (desenho, seleção e alças), aba Imagens, upload do jogador.
- Build e testes só ao final, como na rodada 4.
- Revisão final da branch e exe gerado de novo.
- Merge e release só quando o Yuri pedir.
