# Mesa Virtual, rodada 4: ficha, combate, regras, sessões e música — design

**Data:** 2026-11-07
**Status:** aprovada em conversa, aguardando revisão do texto
**Branch:** `feature/mesa-rodada4` (a partir do master 47b0782, v1.7.1)

## Objetivo

Os jogadores da mesa do Yuri pediram 17 melhorias depois da v1.7.1. O pacote G, de música pelo YouTube, foi pedido depois, na mesma conversa. Entram como 7 pacotes. Cada um é entregue, testado e commitado por vez, nesta ordem:

**B → A → C → F → D → E → G**

Primeiro vêm os bugs. O pacote D, de regras, é o mais pesado e fica para depois que a base estiver estável.

| # | Pedido | Pacote |
|---|---|---|
| 1 | Posição da imagem manual na ficha | A |
| 2 | Jogadores veem os jogadores da mesa mesmo fora da cena | B |
| 3 | Dinheiro e IP no mesmo lugar na ficha | A |
| 4 | Popup "Minha ficha" na mesa, com todas as telas do personagem | E |
| 5 | Cor dos dados | E |
| 6 | Bônus de Nomad e de Tech não entram na ficha | A |
| 7 | Sessões salvas na mesa | E |
| 8 | Faixa ordenada pela iniciativa, com iniciativa manual do mestre | C |
| 9 | Armadura equipada em combate não aparece na hora | B |
| 10 | Acerto e dano visíveis a todos, com crítico rolado pelo jogador | C (visível) + D (crítico) |
| 11 | Botão de passar o turno para o jogador | C |
| 12 | Ícones de lesão crítica em volta do token | F |
| 13 | Barras de HP não atualizam para os outros jogadores | B |
| 14 | Esquiva com REF 8+ | D |
| 15 | Modificadores do Solo (Combat Awareness) e outras regras | D |
| 16 | Ranger Combat Boomerang | D |
| 17 | Botão "Ataque" da aba Personagem igual aos cartões | C |
| — | Música da mesa pelo YouTube | G |

## Decisões tomadas

| Pergunta | Decisão |
|---|---|
| Quem decide a esquiva (REF 8+) | O alvo. Num PC com assento, decide o jogador dele; num NPC ou PC sem assento, o mestre. O mestre pode decidir por qualquer um |
| "DV da armadura" no pedido 14 | Entendido como o DV normal de alcance da arma |
| O que o jogador rola no crítico | Só a Critical Injury (dois ou mais 6 no dano). O 10/1 natural do ataque é automático |
| Popup da ficha | Editável, só do próprio personagem do convite |
| O que é uma sessão | Histórico por sessão. As cenas continuam compartilhadas; "Nova sessão" arquiva o chat e registra os combates |
| Retrato | O jogador ajusta o enquadramento (arrastar e zoom). Vale na ficha, na lista e no token |
| Jogadores ativos | Um cartão por jogador da mesa na faixa, para mestre e jogadores, mesmo sem token na cena |
| Cor dos dados | Cada um escolhe a sua. Todos veem os dados de quem rolou na cor dessa pessoa |
| HP de NPC | Continua oculto dos jogadores. Eles passam a ver o dano causado, mas não o HP restante |
| Ícones no token | Só as Critical Injuries. Condições, como fogo, continuam em texto |
| YouTube | Só como música e ambiente sincronizados (opção 2). Vídeo de fundo do mapa fica fora |

## Fora do escopo

- Vídeo como fundo do mapa, seja do YouTube ou de arquivo.
- Arremessar qualquer arma corpo a corpo (regra genérica). Só o Ranger Boomerang ganha o comportamento de arremesso.
- Outras Role Abilities que somam em perícias, além de Moto (Nomad), Field Expertise (Tech) e Threat Detection (Solo).
- Sessões separando cenas e convites (campanhas).
- Ícones para condições.
- Jogador apagar, duplicar, importar ou exportar o personagem pela mesa.

---

## Pacote B: sincronização e bugs (itens 9, 13, 2)

### B1. Ficha → mesa ao vivo (item 9)
**Como é hoje:** só `TablePartyService.AdjustOwnArmorAsync` avisa a mesa quando uma ficha muda. Equipar armadura, recarregar, curar ou mexer no inventário pela tela de personagens deixa o combate e a faixa velhos até alguém recarregar a página.

**Mudança:**
- **Avisar a mesa de dentro do `CharacterService`:** depois de toda alteração salva numa ficha, ele chama um observador novo, `ICharacterObserver.ChangedAsync(characterId)`. A implementação na camada Table faz duas coisas:
  - Se o PC está num combate ativo ligado à cena ao vivo, chama o `IEncounterObserver.ChangedAsync` com o detalhe recarregado. O combate vai então para o mestre e para cada assento.
  - Avisa os clientes que a ficha mudou com o evento novo `party` do hub. O mestre e os jogadores invalidam a faixa, e o jogador dono invalida também `playerCharacter` e `playerSheet`.
- **Combate em andamento:** quando a mudança vem do próprio `EncounterService`, como no dano de um ataque, o observador não reenvia o combate, porque o `EncounterService` já envia. Isso evita envio em dobro e loop.
- **Ponto único:** o aviso é disparado num só lugar, no método que salva a ficha, e não espalhado por cada operação.

### B2. Barras de HP (item 13)
**Sintoma relatado:** quando alguém leva tiro, a barra de HP só atualiza para quem atirou e para o mestre.

**Método:**
- Reproduzir no Edge com dois assentos conectados, um atirando e outro olhando, antes de mexer em código.
- Achar a causa pelo caminho `EncounterBroadcast` → `HubTableNotifier` → `useTableHub` → cache do React Query → `BoardData.fighters` → `BoardController.setData`.

**Suspeitas a conferir:**
- Token de PC ligado só por `characterId`, sem `combatantId`: o `drawToken` procura o lutador só por `combatantId`.
- Grupos do hub de cada assento depois de reconectar.
- A faixa do jogador lendo HP de `party`, invalidado sem refetch.

**Entrega:**
- A correção da causa.
- Um teste para cada causa, no vitest ou no backend, para o erro não voltar.

### B3. Cartões do grupo (item 2)
**Mudança:**
- **Endpoint:** `GET /api/table/scenes/{id}/party` e `GET /api/table/play/party` passam a devolver todo assento não revogado com personagem, e não só os PCs com token na cena. Cada item ganha:
  - `seatId`, `online` (vindo do `TablePresence`) e `inScene`;
  - `imageUrl`, o retrato do PC servido pelo media da mesa, como os tokens já fazem.
- **Faixa (`buildStrip`):** os PCs com token continuam como hoje. Os assentos sem token na cena entram no fim, como cartões esmaecidos marcados "fora da cena".
  - Clicar não seleciona nada no mapa.
  - Não podem ser alvo de mira.
- **Conexão:** o ponto de conectado aparece em todos os cartões de PC. Quando um jogador conecta ou desconecta, os jogadores também recebem o evento `party`, e não só o mestre (`seats`).

**Testes:**
- Backend: party com assento sem token, revogado, offline e online.
- Vitest: `buildStrip` com membros fora da cena.

---

## Pacote A: ficha (itens 1, 3, 6)

### A1. Enquadrar o retrato
- **Dado:** `CharacterIdentity.PortraitFocus { X, Y, Zoom }`, com X e Y em % (padrão 50/50) e Zoom de 1 a 3 (padrão 1). Pode ser nulo e vale o centro.
- **Ficha:** o botão "Enquadrar" no retrato da ficha liga o modo de ajuste.
  - Arrastar move o ponto focal e um controle deslizante dá o zoom.
  - "Salvar" grava o enquadramento pelo editor da ficha, que já existe. "Cancelar" volta ao anterior.
- **Onde vale:**
  - No retrato da ficha e na lista de personagens, com `object-position` e `transform: scale` dentro da moldura que já tem `overflow: hidden`.
  - No token: o `TokenView` ganha `imageFocus` quando a imagem vem do retrato do PC, e o `BoardController` posiciona e escala o sprite dentro da máscara com esse foco.
- **Testes:** backend (o foco é salvo e limitado aos intervalos) e uma função pura no vitest que calcula o deslocamento do sprite.

### A2. Saldo e IP juntos
- No topo da aba Personagem, na coluna de identidade logo abaixo do retrato, entra o bloco "Carteira" com dois números grandes lado a lado:
  - **Saldo:** em eb.
  - **IP:** disponível, com o total ganho em menor.
- O Saldo sai da lista de identidade. O IP continua também onde já aparece, no painel de progressão, porque é lá que se gasta.

### A3. Bônus de Role nas perícias (item 6)
**Como é hoje:** o `CharacterCalculator.ComputeSkills` não soma nenhum Rank de Role. O `character-rules.json` só descreve as regras em texto.

**Regras (CP:R):**
- **Moto (Nomad, p.161):** soma o Rank em Drive Land Vehicle, Pilot Air Vehicle, Pilot Sea Vehicle e nas perícias Tech de veículos.
- **Maker (Tech, p.147–149):** a cada Rank ganho, o Tech põe 1 Rank em duas especialidades diferentes, entre Field, Upgrade, Fabrication e Invention. **Field Expertise** soma o Rank dessa especialidade em Basic Tech, Cybertech, Electronics/Security Tech, Weaponstech e Tech de veículos.
- **Threat Detection (Solo, p.146):** +1 por ponto em Perception. Pertence ao Combat Awareness do pacote D, mas usa o mesmo mecanismo e entra aqui.

**Mudança:**
- **Dado de regras:** `character-rules.json` ganha, na `ability` de cada Role, o bloco opcional `skillBonuses`, uma lista de `{ skills: [...ids], from: "rank" | "split:<chave>", label }`. O código lê esse bloco e nada fica fixo no C#.
- **Ficha:** `CharacterRole.Split`, um dicionário `chave → pontos`, guarda a distribuição do Tech (`field`, `upgrade`, `fabrication`, `invention`) e do Solo (`damageDeflection`, `fumbleRecovery`, `initiativeReaction`, `precisionAttack`, `spotWeakness`, `threatDetection`).
- **Validação:**
  - **Tech:** a soma das especialidades é no máximo 2 × Rank, porque cada Rank dá 1 em duas especialidades diferentes, e cada especialidade fica entre 0 e o Rank.
  - **Solo:** a soma é no máximo o Rank, e Fumble Recovery recebe só 0 ou 4.
- **Tela:** "Distribuir" ao lado da Role Ability na ficha, com um campo numérico por chave e o total usado/disponível. Para o Solo, aparece uma nota: "em combate, redistribuir custa uma Ação (CP:R p.146)".
- **Cálculo:** o `ComputeSkills` soma o bônus como `ValuePart`, por exemplo "Moto (Rank 4)" ou "Field Expertise 2", e ele aparece no tooltip da perícia.
- **Testes:**
  - Nomad Rank 4: Drive Land Vehicle +4.
  - Tech com Field 2: Weaponstech +2.
  - Distribuição inválida é recusada.
  - Ficha antiga, sem `Split`, continua válida.

---

## Pacote C: combate na mesa (itens 8, 11, 17 e a parte visível do 10)

### C1. Faixa na ordem da iniciativa (item 8)
- Com combate ativo na cena, o `buildStrip` ordena os cartões pela ordem da fila do combate (`queue`).
- Quem não está no combate vem depois. Os cartões de fora da cena (B3) ficam por último.
- Sem combate, a ordem continua a de hoje.

### C2. Iniciativa manual do mestre (item 8)
- Na aba Combate da mesa (`GmPanels` → `CombatTab`), cada linha da fila ganha:
  - um campo numérico da iniciativa, que salva ao sair do campo pela rota `SetInitiative`, que já existe;
  - as setas ↑↓, que usam a reordenação já existente.

### C3. Passar o turno (item 11)
- **Backend:** `POST /api/table/play/end-turn` no `TablePlayController`. Recusa com 409 se não for o turno do PC do assento, se não houver combate ou se houver um ataque pendente (D1). Por baixo, chama o `EncounterService.NextTurnAsync` com a versão atual.
- **Jogador:** botão "Passar o turno" na aba Iniciativa e na barra "Sua vez!", ativo só no turno dele.
- **Mestre:** continua com "Próximo turno", que força a passagem.

### C4. "Ataque" da aba Personagem (item 17)
- Em combate, os botões "Ataque" e "Autofire" do `CharacterPanel` chamam `quick.start(startAim(...))` com o modo certo. É o mesmo fluxo dos chips: alvo no mapa ou num cartão, modo e tiro mirado na `AimBar`.
- Fora do turno ficam desligados, com a dica "Aguarde seu turno".
- Fora de combate, continuam rolando no chat, como hoje.
- Some a nota "Defesa, alcance e a munição gasta são resolvidos pelo mestre" quando há combate.

### C5. Ataques visíveis a todos (parte do item 10)
- **Ficha de ataque do jogador:** o `TableAttacks.ForSeat` passa a mostrar a todos, em qualquer alvo, o total do ataque, o DV ou a defesa, a rolagem de defesa, os dados de dano e o dano causado.
- **Continua oculto do jogador:** só o `Through` (quanto passou para o HP) e o estado numérico do NPC. O HP restante do NPC já não está na mensagem.
- **Dados 3D:** os dados de ataque e de dano de qualquer alvo animam para todos (`throwOf`).
- **Testes:** `ForSeat` com alvo NPC passa a carregar o dano e os dados, mas não o `Through`.

---

## Pacote F: ícones de lesão crítica no token (item 12)
- **Mapa de ícones:** `injuryIcons.ts` liga cada nome de Critical Injury da tabela do Core a um ícone (perna, braço, mão, olho, cabeça/concussão, costelas/torso, objeto estranho, pulmão, coluna, queimadura, orelha) e tem um genérico de reserva. Os nomes vêm da tabela usada pelo combate.
- **Desenho:** o `BoardController.drawToken` troca o texto das lesões por círculos pequenos com o ícone desenhado em `Graphics`, sem assets novos.
  - Ficam em arco em volta do token, de cima para os lados, até 8 ícones; depois disso aparece "+N".
  - Passar o mouse sobre um ícone mostra o nome numa etiqueta.
- **Condições:** continuam como texto curto acima do token.
- **Testes:** vitest para o mapeamento (todo nome da tabela tem ícone ou cai no genérico) e para as posições no arco.

---

## Pacote D: regras de ataque (itens 14, 10 crítico, 15, 16)

### D1. Ataque pendente
- **Estado:** `EncounterState.Pending`, que pode ser nulo e guarda um ataque parado num passo:
  - `Seq`, atacante, arma, modo, distância e a rolagem de ataque já feita (d10, extra do 10/1 e total);
  - os alvos e, para cada um, `Step` (`Defense` ou `CriticalInjury`), `DecidedBy` (`seatId` ou mestre) e o que já foi resolvido;
  - os dados de dano, quando já rolados.
- **Fluxo:**
  1. O ataque rola o d10 com o 10/1 natural (D2) e calcula o total.
  2. Para cada alvo de tiro, fora de área e de corpo a corpo, com `CanDodge` (REF 8+), o alvo entra em `Defense`. Os outros alvos já resolvem o DV como hoje.
  3. Com todas as defesas decididas, o servidor rola o dano. Se um alvo atingido tiver dois ou mais 6, ele entra em `CriticalInjury`, e quem rola é o jogador do atacante ou o mestre, se o atacante é NPC.
  4. Com tudo resolvido, o ataque é aplicado como hoje: munição, cobertura, armadura, dano, `LastAttack`, desfazer e chat.
- **Rotas:**
  - `POST /api/encounters/{id}/pending/defense { targetId, dodge }`, para o mestre.
  - `POST /api/encounters/{id}/pending/critical { targetId }`, para o mestre.
  - `POST /api/table/play/defense { dodge }` e `POST /api/table/play/critical`, para o jogador, que só respondem pelo próprio PC.
  - `POST /api/encounters/{id}/pending/cancel`, para o mestre: desfaz o ataque pendente sem gastar nada.
- **Enquanto pendente:**
  - O combate recusa outras ações com 409 ("Há um ataque esperando…"): ataque, turno, dano, cura e iniciativa.
  - Só os passos pendentes e o cancelamento do mestre passam.
  - A munição só é gasta quando o ataque é aplicado.
- **Esquiva na tela:**
  - O defensor recebe o cartão "{atacante} atira em você com {arma}" com dois botões:
    - "Esquivar": Evasion +N, e o servidor rola o d10;
    - "Não esquivar": fica o DV {x} de alcance.
  - O total do ataque não aparece antes da decisão.
  - O mestre vê a mesma pergunta para NPCs e pode responder pelos PCs.
- **Critical Injury na tela:** o atacante recebe "Dois 6 no dano! Rolar Critical Injury". O servidor rola 2d6 na tabela de corpo ou cabeça, e a rolagem vai para o chat com dados 3D.
- **Onde aparece:** na mesa (mestre e jogadores) e na página /combat (`EncounterView`), por um cartão de pendência acima da fila.
- **Formulário completo e dados digitados:** o formulário do `AttackPanel` continua aceitando dados digitados. Se o mestre digitar a defesa ou a Critical Injury, aquele passo não fica pendente.
- **Testes:**
  - Alvo REF 7 não pausa.
  - Alvo REF 8 pausa, e esquivar muda o DV para a Evasion rolada.
  - Não esquivar usa o DV de alcance.
  - O passo de crítico rola a tabela.
  - O cancelamento não gasta munição.
  - Ações durante a pendência são recusadas.
  - O jogador não responde por outro PC.
  - O desfazer volta tudo depois de um ataque que teve pendência.

### D2. 10 e 1 naturais no ataque (CP:R p.130)
- O `AttackResolver` passa a tratar o d10 de ataque: no 10 rola mais 1d10 e soma; no 1 rola mais 1d10 e subtrai. Tudo automático.
- O resultado guarda os dois dados, e o cartão e os dados 3D mostram o extra.
- **Fumble Recovery do Solo (D3):** ignora o 1 natural. O dado não subtrai e não há falha crítica.
- **Testes:** com dados fixos para 10, 1 e Fumble Recovery.

### D3. Combat Awareness do Solo (item 15)
A distribuição vem de `CharacterRole.Split` (A3), e cada efeito cita CP:R p.146:
- **Precision Attack:** 3, 6 ou 9 pontos dão +1, +2 ou +3 no total de ataque das armas do Solo, no `FighterViews`.
- **Spot Weakness:** +N no dano, antes da armadura, do primeiro ataque que acerta na rodada. O combatente ganha uma marca por rodada.
- **Damage Deflection:** 2/4/6/8/10 pontos tiram 1/2/3/4/5 do primeiro dano sofrido na rodada, com marca por rodada.
- **Initiative Reaction:** +N na rolagem de iniciativa.
- **Fumble Recovery:** ver D2.
- **Threat Detection:** entra em Perception (A3).

As marcas por rodada zeram quando a rodada vira.

### D4. Outros modificadores de ataque (item 15, "outras regras")
- **Candidatos:** arma de qualidade Excellent, Smartgun Link com arma smart e Targeting Scope em tiro mirado.
- **Critério:** cada um só entra depois de eu confirmar a regra e o valor nos dados de livro do repositório (descrições, compêndio, `combat-rules.json`). Os que não forem confirmados ficam fora, e eu aviso o Yuri.
- **Conta visível:** o total de ataque passa a vir com as partes, `AttackParts`, por exemplo "Handgun 12, Precision Attack +1, Excellent +1". A `AimBar` e o cartão de ataque mostram a conta.

### D5. Ranger Combat Boomerang (item 16)
- **Fonte:** a descrição do item (`ranger-combat-boomerang`). É uma Heavy Melee Weapon Exotic de uma mão, que não faz ataque corpo a corpo e é arremessada.
  - Continua reduzindo o SP pela metade quando arremessada.
  - Arremessá-la conta como metade de um ataque de ROF 2.
  - Volta no início do próximo turno do atacante se ele tiver cyberware Targeting Scope; sem ele, cai perto.
- **Regras especiais por slug:** `combat-rules.json` ganha `specialWeapons`, por slug, para o código não ficar com o nome fixo. O boomerang recebe `{ thrownOnly: true, halvesArmor: true, rof: 2, returnsWith: "targeting-scope" }`.
- **No combate:**
  - A arma vira de arremesso: perícia Athletics (DEX + Athletics), DV pela linha de arremesso da tabela de alcance (a mesma do "Objeto arremessado"), dano de Heavy Melee (3d6) e `HalvesArmor`.
  - Não aparece como corpo a corpo, e a distância vem do mapa como num tiro.
- **Depois do arremesso:**
  - O combatente fica com a marca "boomerang arremessado", e a arma aparece desligada com essa dica.
  - No início do próximo turno do atacante, a marca some se ele tiver Targeting Scope instalado.
  - Sem o Scope, aparece "Pegar do chão" para o jogador e para o mestre, que limpa a marca.
- **Testes:** usa Athletics e o DV de arremesso, reduz SP, fica indisponível depois do arremesso, volta com Scope e não volta sem ele.

---

## Pacote E: popup da ficha, sessões e cor dos dados (itens 4, 7, 5)

### E1. Popup "Minha ficha" (item 4)
- **Servidor:** o `PlayerSurfaceGuard` passa a deixar o túnel chegar a `/api/characters/{id}/**` só quando o pedido traz a chave do convite (cabeçalho `Authorization: Bearer`) e `id` é o `CharacterId` desse assento. Também libera as leituras que a ficha usa: regras de personagem, catálogo e loja/clínica em GET, e retrato.
  - **Bloqueado:** apagar, duplicar, importar e exportar o personagem, e todo o resto da API (outros personagens, mesa do mestre, combates, atualizador).
  - **Onde fica a checagem:** um middleware/filtro único, com a lista de rotas permitidas explícita.
- **Frontend:**
  - O cliente da API de personagens passa a aceitar um contexto de "convite": base e cabeçalho com a chave.
  - O botão "Minha ficha" no topo da tela do jogador abre um modal grande, quase tela cheia, com o `EmbeddedCharacter`: as 4 abas reais (Personagem, Caminho de Vida, Equipamento, Cyberware), editáveis.
- **Sincronia:** toda alteração passa pelo `CharacterService`, então B1 já leva a mudança para a mesa. Equipar armadura no popup atualiza o combate e a faixa na hora.
- **Testes:**
  - Guard: o próprio personagem passa, outro id dá 404, apagar dá 404 e sem chave dá 404.
  - Checagem no Edge pelo porto do jogador.

### E2. Sessões (item 7)
- **Dados:** a entidade nova `TableSession { Id, Name, StartedAt, EndedAt? }` e a migração. `TableMessage.SessionId` aponta a sessão.
  - **Migração:** cria uma sessão "Sessão 1" com as mensagens que já existem.
  - Sempre há uma sessão aberta.
- **Combates:** o `Encounter` ganha `SessionId`, que pode ser nulo. Ele é preenchido quando um combate é criado ou ligado a uma cena pela mesa durante a sessão.
- **Mestre (aba Chat):**
  - "Nova sessão" substitui "Limpar histórico". O mestre dá um nome, que vem preenchido com a data. A sessão atual fecha com o nome, e uma nova abre com o chat vazio.
  - "Sessões anteriores" lista nome, datas e número de mensagens. Abrir uma mostra o chat dela (rolagens e ataques) em leitura e a lista dos combates com link para /combat.
  - Também dá para renomear ou apagar uma sessão antiga, apagar com confirmação.
- **Jogadores:** veem só a sessão aberta, como hoje.
- **Testes:** nova sessão, mensagens vão para a sessão aberta, listagem, leitura, apagar e migração das mensagens antigas.

### E3. Cor dos dados (item 5)
- **Dados:** `TableSeat.DiceColor` (#rrggbb, que pode ser nulo e cai na cor do assento). A cor do mestre fica numa configuração da mesa persistida no servidor, na tabela/registro `TableSettings` com a chave `gmDiceColor`.
- **Escolha:**
  - Jogador: um seletor de cor ao lado do botão dos dados 3D, pela rota `PUT /api/table/play/dice-color`.
  - Mestre: o mesmo seletor na tela dele.
- **Na rolagem:** cada mensagem de rolagem ou ataque grava a cor de quem rolou no momento (`diceColor` no payload). O `DiceTray` e o `diceScene` pintam o corpo do dado com essa cor e escolhem números claros ou escuros pelo contraste.
- **Testes:** vitest para a cor de contraste e backend para a cor gravada na mensagem.

---

## Pacote G: música da mesa (YouTube)
- **Estado no servidor:** `TableMusic { VideoId?, PlaylistId?, Playing, PositionSeconds, UpdatedAt, Volume }`, em memória e persistido em `TableSettings`.
  - Rotas do mestre: `PUT /api/table/music`, que recebe o link e é validado e analisado no servidor (watch, youtu.be, playlist), e `POST /api/table/music/{play|pause|seek|next}`.
  - Cada mudança vai para todos pelo evento `music` do hub.
  - O estado vai junto no `GET /api/table/play/state`, para quem entra no meio.
- **Recentes:** o mestre tem até 8 links recentes com nome, também em `TableSettings`.
- **Cliente:**
  - Um componente `MusicPlayer` com a YouTube IFrame Player API, carregada sob demanda só quando há música.
  - **Posição esperada:** `PositionSeconds + (agora − UpdatedAt)` quando está tocando.
  - **Correção da deriva:** se o player se afastar mais de 2 s da posição esperada, faz `seekTo`.
- **Jogadores:**
  - Player pequeno, de no mínimo 200×200 como pedem as regras do YouTube, num canto da área do mapa. Dá para recolher para uma pílula que continua tocando.
  - Na primeira vez aparece "Ativar som", porque o navegador exige um clique. Volume e mudo ficam em `tablePrefs`.
- **Mestre:** o painel "Música" com link, play/pausa, avançar na faixa, próxima e recentes.
- **Erros:** se o vídeo não deixa incorporar (erros 101/150) ou o link é inválido, aparece um aviso claro para o mestre.
- **Testes:** backend (análise do link e estado) e vitest (cálculo da posição esperada e decisão de corrigir).

---

## Testes e entrega
- Em cada pacote rodam os testes do dotnet e do vitest, mais a checagem no Edge com o mestre e dois jogadores (scripts playwright-core na pasta temporária), como nas rodadas anteriores.
- O Yuri vê os textos novos de interface em PT e EN, porque a interface é bilíngue: `t(pt, en)` e `L.T`.
- Commit por pacote em `feature/mesa-rodada4`.
- No fim, revisão de código, exe gerado de novo e verificado. Merge, push e release (v1.8.0) só quando o Yuri pedir.
