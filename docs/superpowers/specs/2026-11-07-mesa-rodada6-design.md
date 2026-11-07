# Mesa virtual, rodada 6: design

Data: 2026-11-07. Branch `feature/mesa-rodada6`, saída de `feature/mesa-imagens` (ainda não mesclada).
Aprovado pelo usuário no chat ("boa, mete marcha").

## Objetivo

Dez pedidos da mesa do usuário, já validados no código. O layout de equipamento fica fora; é TODO de uma rodada própria.

## 1. Sessões como porta de entrada

- `/mesa` mostra a lista de sessões, como a tela de personagens. Cada sessão traz nome, datas, número de mensagens e número de combates.
- A sessão aberta tem "Continuar", que leva a `/mesa/jogar`, a mesa de hoje.
- "Nova sessão" pede o **nome da nova sessão**, encerra a aberta (que fica com o nome que já tem) e entra na mesa.
- Sessões encerradas: "Ler" (chat arquivado e combates), renomear e apagar. A aberta só pode ser renomeada.
- Cenas, convites e lugares valem para todas as sessões. A sessão separa só o chat e os combates.
- O link dos jogadores (`/mesa/j/:token`) não muda.
- Dentro da mesa, o botão de sessões leva de volta a `/mesa`.
- API: `NewSessionRequest.Name` passa a nomear a sessão nova. Sem nome, a nova se chama "Sessão N", com N = maior número em uso + 1, para não repetir nome depois de apagar sessões.

## 2. Música

- "Minimizar" recolhe o player numa barra com título, volume, mudo e expandir. A música continua tocando.
- O iframe continua montado, posicionado fora da tela com 200×200. O YouTube não tem modo só áudio, então o vídeo ainda carrega. O usuário aceitou isso.
- O estado minimizado é de cada pessoa, guardado em `TablePrefs.musicMinimized`.
- O "Esconder" que parava a música sai. Para silenciar a própria tela fica o mudo.
- Pausa sincronizada:
  - a checagem a cada 3 s também força a pausa quando a mesa está pausada e o player está tocando ou carregando;
  - uma faixa nova carregada com a música pausada é só preparada (`cue`), sem tocar.
- A decisão vem de uma função pura `musicAction(state, music, unlocked)`.

## 3. Importar ficha pelo jogador (opção A)

- `POST /api/table/play/character/import` recebe o JSON exportado (o mesmo formato de `/api/characters/import`).
- Só funciona se o lugar não tem personagem; se já tem, a resposta é 409.
- Cria a ficha no banco do mestre, liga ao lugar e avisa a mesa pelo hub (seats e party).
- Na tela do jogador sem personagem aparece o botão "Importar minha ficha (.json)". Ao terminar, a aba Personagem já mostra a ficha.
- O mestre continua podendo ligar ao lugar uma ficha que ele já tem.

## 4. Solo: Combat Awareness na mesa

- O Precision Attack (3/6/9 pontos → +1/+2/+3) entra no cálculo de ataque da ficha, como um `AttackBonus`. Assim o mesmo número aparece na ficha, na aba Personagem, nos cards e nas rolagens. O FighterViews deixa de somar por conta própria, para não contar duas vezes.
- O bloco "Combat Awareness" aparece:
  - na aba Personagem do jogador, quando ele é Solo;
  - na aba Seleção do mestre, num token de Solo.
- O bloco mostra a distribuição e os efeitos:
  - Precision +N e Initiative +N;
  - Deflection −N, com "disponível" ou "usado nesta rodada";
  - Spot Weakness +N, com "disponível" ou "usado nesta rodada";
  - Threat Detection e Fumble Recovery, apenas como texto.
- O "disponível" ou "usado" vem do `FighterView.RoundFlags`, que é novo.
- "Redistribuir" abre o mesmo `RoleSplitDialog`. Se o personagem está num combate ativo, o registro do combate anota "redistribui o Combat Awareness (gasta uma Ação, CP:R p.146)". Não há bloqueio.
- O cartão de ataque mostra as notas de Combat Awareness:
  - por alvo: Spot Weakness e Damage Deflection;
  - do ataque: Precision Attack.

## 5. Dados no cartão de ataque

- O cartão mostra os dados com os mesmos chips das rolagens:
  - o d10 do ataque e o extra do 10/1;
  - a rolagem de defesa do alvo;
  - cada dado de dano;
  - os 2d6 da Critical Injury.

## 6. Ver ficha nos cards

- Um ícone de ficha nos cards de PC da faixa da cena.
- O jogador vê o ícone só no próprio card, e ele abre a "Minha ficha".
- O mestre vê o ícone em todos os PCs, e ele abre a ficha completa e editável no mesmo diálogo, com o nome do personagem no título.

## 7. Ataque da aba Seleção

- As armas da aba Seleção do mestre começam a mira rápida, com o mesmo fluxo dos cards. O "Dano" continua rolando no chat.

## 8. Botão de recarregar no card

- Na linha da arma, os botões extras ficam com `width: auto`. O nome da arma ocupa o resto da linha.

## Testes

- Backend (integração):
  - nome da sessão nova;
  - numeração das sessões depois de apagar;
  - importação pelo jogador: sucesso, 409 com personagem, 401 sem lugar;
  - Precision na ficha sem contar duas vezes no combate;
  - RoundFlags no FighterView;
  - notas na `TableAttack`;
  - registro de redistribuição em combate.
- Frontend (unidade):
  - `musicAction`;
  - formatação dos dados do cartão (`attackDice`).
- Edge:
  - lista de sessões, nova sessão e continuar;
  - música minimizada (só com rede);
  - importação de ficha;
  - cards com ícone de ficha e recarregar.
