# Compêndio de NPCs: design

**Data:** 2026-10-30
**Status:** aprovada

## Objetivo

Uma tela nova, com a mesma estética de Personagens, que lista os NPCs com nome próprio do livro Core e dos suplementos de Cyberpunk RED. O mestre consulta a ficha na mesa e coloca o NPC no Friday Night Firefight com um clique.

Sucesso significa:

- Todo NPC nomeado dos PDFs disponíveis está na lista, com livro e página.
- Quem tem ficha no livro aparece com os números do livro.
- Quem só existe na história aparece como perfil, sem nenhum número inventado.
- Um NPC com ficha entra em combate no app com HP, armadura e armas certos.

## Decisões tomadas

| Pergunta | Decisão |
|---|---|
| NPC sem ficha no livro | Entra como perfil, marcado "sem ficha oficial" |
| Uso | Consulta e combate. Copiar para o Gerador de Inimigos fica fora |
| Quem entra | Só personagens com nome próprio |
| Onde ficam os dados | Arquivo JSON embutido no backend (opção A) |
| Retratos | Ícone padrão nesta versão. Não extraímos arte dos livros |
| Biografias | Resumos curtos escritos por nós, em PT e EN, sem copiar o texto dos livros |

## Fora do escopo

- Editar NPCs do compêndio dentro do app.
- Copiar um NPC do compêndio para o Gerador de Inimigos.
- Aplicar sozinho, no combate, as regras especiais de um NPC. Elas aparecem como texto.
- Favoritos e listas de NPCs.
- Extrair retratos dos PDFs.
- Fichas genéricas, como "Bozo Mook" e "NCPD Officer".

## Quem entra

**Fontes:** os PDFs em `%USERPROFILE%\Downloads\Cyberpunk PDFs`.

**Entra** quem tem nome próprio e atende a uma destas condições:

- tem ficha no livro (kind `sheet`);
- tem pelo menos um parágrafo próprio sobre a pessoa (kind `profile`).

**Não entra:**

- nome citado de passagem, sem texto próprio;
- autor de citação ou de artigo fictício;
- marca ou mascote;
- ficha genérica, mesmo que tenha apelido de categoria;
- PC pré-gerado, a não ser que o livro o trate como personagem da história.

**Ficha com rótulo no lugar de nome:** "The Clown" e "Boom-Boom" entram, porque identificam uma pessoa só. "Road Ganger" e "Philharmonic Vampyre Mook" não entram, porque são tipos.

**Um NPC em vários livros:** é uma entrada só, com todas as fontes. Se houver mais de uma ficha, vale a mais recente, e o campo `sheetNote` registra a diferença.

**Levantamento inicial:** cerca de 200 fichas nos PDFs, somando nomeados e genéricos, principalmente no Danger Gal Dossier, em Tales of the RED, na Interface RED e no Edgerunners Mission Kit. O número final sai da extração. Adam Smasher tem ficha no Edgerunners Mission Kit. Morgan Blackhand, Rogue, Santiago, Johnny Silverhand e Alt Cunningham ainda precisam de confirmação nome por nome. Se algum tiver ficha, entra como `sheet`.

## Arquitetura

```
PDFs ──> tools/npc-compendium/extract.mjs ──> rascunho (fora do repositório)
                                                   │ conferência contra o PDF, biografias PT/EN
                                                   v
         data/source/npc-compendium.json (embutido no Application)
                                                   │
                         NpcCompendium (singleton, carrega e indexa)
                            │                                │
         NpcCompendiumController                 EncounterService.AddCombatantsAsync
         GET /api/npc-compendium[/{id}]          r.CompendiumNpcId -> CombatSnapshot
                            │                                │
                 /npcs, /npcs/:id                 Friday Night Firefight
```

O JSON não passa pelo banco. Não há migration, e corrigir um NPC é editar o arquivo e soltar uma versão.

## Dados: `data/source/npc-compendium.json`

Uma lista de entradas. Os números e os nomes de armas, equipamentos e perícias ficam em inglês, como nos livros. Só `summary`, `specials[].text` e `sheetNote` têm PT e EN.

```json
{
  "id": "adam-smasher",
  "name": "Adam Smasher",
  "aliases": [],
  "kind": "sheet",
  "role": "Solo",
  "roleRank": 9,
  "rep": 10,
  "affiliation": "Arasaka",
  "sources": [{ "book": "Edgerunners Mission Kit", "page": 0 }],
  "summary": { "pt": "...", "en": "..." },
  "sheetNote": null,
  "sheet": {
    "hp": 75,
    "seriouslyWounded": null,
    "deathSave": 17,
    "stats": { "INT": 6, "REF": 8, "DEX": 8, "TECH": 6, "COOL": 5, "WILL": 8, "MOVE": 8, "BODY": 17, "EMP": null },
    "armoredStats": {},
    "weapons": [
      {
        "name": "Adam Smasher's Fists", "skill": "Brawling", "attackBase": 18, "damage": "5d6", "rof": 2,
        "magazine": null, "class": null, "melee": true, "notes": "Halves SP"
      }
    ],
    "armor": [{ "location": "Body", "name": "...", "sp": 0 }],
    "skills": { "Evasion": 0 },
    "gear": [],
    "cyberware": [],
    "specials": [{ "name": "FBC Body", "text": { "pt": "...", "en": "..." } }]
  }
}
```

Os valores `page: 0`, `sp: 0` e `Evasion: 0` acima são só ilustração do formato. O teste de dados recusa página 0.

Regras do formato:

- `id`: slug único do nome.
- `kind` é `sheet` ou `profile`. Em `profile`, `sheet` é `null` e `role`, `roleRank` e `rep` são opcionais.
- `sources` tem pelo menos uma entrada, com nome do livro e página impressa.
- `stats` guarda o valor impresso. Campo `—` no livro vira `null`.
- `armoredStats` guarda os valores entre parênteses, como `REF 8(6)`, que são os valores com a penalidade da armadura. Fica vazio quando o livro não os traz.
- `skills` guarda a Skill Base impressa, sem recalcular.
- `attackBase` é o total de ataque impresso (STAT + perícia + bônus).
- `class` é um valor do enum `WeaponClass` quando a arma do livro corresponde a um. O campo fica `null` em armas fora da tabela, como os punhos do Smasher, e nesses casos `melee` diz como a arma se comporta.
- Regras especiais são parafraseadas em poucas frases, com a mecânica preservada.

## Backend

Código novo em `backend/src/CyberpunkDb.Application/Npcs/Compendium/`:

- **`CompendiumNpc.cs`:** records do JSON.
- **`NpcCompendium.cs`:** carrega o recurso embutido e indexa por `id`, como `NpcRuleBook.LoadEmbedded()`. É registrado como singleton em `DependencyInjection.cs`.
- **DTOs:** `CompendiumNpcSummary` (lista) e `CompendiumNpcDetail` (ficha completa). Os textos com PT e EN saem como uma string só, escolhida por `L.T`.

No csproj, um novo `EmbeddedResource` aponta para `data/source/npc-compendium.json`, como `night-market-index.json`.

**API:** `NpcCompendiumController`, só leitura:

| Rota | Resposta |
|---|---|
| `GET /api/npc-compendium` | Lista de `CompendiumNpcSummary` |
| `GET /api/npc-compendium/{id}` | `CompendiumNpcDetail`, ou 404 com mensagem em PT ou EN |

**Combate:**

- `AddCombatantsRequest` ganha `string? CompendiumNpcId = null`.
- `AddCombatantsAsync` ganha um ramo para esse campo. O nome padrão do combatente é o do NPC, e `Count` funciona como nos outros tipos.
- `FighterViews.Compendium(CompendiumNpc)` monta o `CombatSnapshot`:
  - `Stats` usa `armoredStats` quando existir, senão `stats`. Campos `null` ficam de fora.
  - `MaxHp` vem de `hp`, e `Body` de `stats.BODY`.
  - `MaxSpBody` e `MaxSpHead` vêm de `armor`.
  - `Skills` traduz os nomes para as chaves de `NpcRuleBook.Skills`. As perícias que não baterem com nenhuma chave ficam de fora do snapshot e continuam visíveis na ficha.
  - Cada arma vira um `CombatWeapon` com `AttackBase = attackBase`, usando o caminho que o genérico já tem.
  - `Source` traz o nome do livro e a página.
  - `Cyberarm` é verdadeiro quando a lista `cyberware` tem um Cyberarm.
- Adicionar uma entrada `profile` devolve erro de validação: "Este NPC não tem ficha oficial."
- `id` desconhecido devolve 404.
- O `CombatantKind` é `Npc`, como nos NPCs gerados, que também não têm ficha salva por trás. `Generic` faria a tela do combate rotular o NPC como "Inimigo genérico".

## Frontend

Código novo em `frontend/src/features/npc-compendium/`. Reaproveita `Characters.module.css` e os componentes de `ui.tsx`.

- **`/npcs`:** título, busca por nome e apelido, filtros por livro, Role e "tem ficha", e a grade de cards no estilo de `CharacterCard`. O card mostra ícone, nome, Role, Rep, afiliação e o selo "sem ficha oficial" quando for o caso. A filtragem roda no cliente, porque a lista é pequena.
- **`/npcs/:id`:** cabeçalho com nome, Role, Rep e fontes, seguido da biografia. Quem tem ficha ganha STATs, HP com Seriously Wounded e Death Save, armas, armadura, perícias, equipamento, cyberware e regras especiais. Os painéis são só leitura e seguem o visual da aba Ficha.
- **Adicionar ao combate:** botão na ficha, oculto em perfis. Abre um diálogo para escolher um combate aberto, o lado e a quantidade, e depois leva ao combate.
- **`AddCombatantsDialog`:** nova origem "Compêndio", com busca entre os NPCs que têm ficha.
- **Menu:** novo item "Compêndio de NPCs" / "NPC Compendium", em `AppShell.tsx`.
- **Idioma:** textos da interface com `t()`. Nomes de NPC, armas e equipamentos ficam como no livro.
- **Lógica pura:** `filterNpcs` num arquivo próprio, com testes.

## Extração e conferência

`tools/npc-compendium/extract.mjs` segue o modelo de `tools/night-market/extract.mjs`:

1. Roda o `pdftotext -layout` em cada PDF, com a pasta configurável.
2. Encontra os blocos de ficha pelos cabeçalhos `NAME`/`LEVEL` e `STATS`. Os layouts conhecidos são o do Edgerunners Mission Kit, o do Danger Gal Dossier e o de Tales of the RED, além das variações das Interface RED.
3. Gera um rascunho com os blocos e as páginas, fora do repositório, para revisão.

O script não publica nada direto. O JSON final é conferido ficha a ficha contra o PDF, e as biografias são escritas à mão. Blocos que o parser não entender são tratados manualmente, e a contagem final compara os blocos encontrados com as entradas e os genéricos descartados, para nenhuma ficha sumir em silêncio.

Se a página impressa diferir da página do PDF, vale a impressa.

Como a tarefa é grande, a entrega é feita em lotes por livro, e cada lote passa pelos testes de dados antes do próximo.

## Erros

- JSON inválido ou ausente no recurso embutido: falha ao iniciar, como nos outros rule books. O teste de dados pega isso antes.
- `id` desconhecido na API: 404 traduzido.
- Falha ao carregar na tela: `ErrorState`, como em Personagens.
- Sem combate aberto no diálogo: mensagem com link para criar um.
- Lista vazia pelos filtros: `EmptyState` com opção de limpar.

## Testes

**Dados (`NpcCompendiumDataTests`):**

- ids únicos e em formato de slug, nomes únicos;
- toda entrada tem fonte com página maior que 0;
- `summary` com PT e EN preenchidos;
- `sheet` só em `kind: sheet`, e obrigatória nele;
- STATs conhecidos, com HP e Death Save presentes;
- dano de arma em formato válido para o motor de combate;
- `class`, quando presente, é um valor válido, e toda arma tem `attackBase`.

**API e combate:**

- lista e detalhe, em PT e EN;
- 404 em id desconhecido;
- adicionar Adam Smasher a um combate: HP 75, armas e SP corretos;
- `Count` gera cópias com nomes numerados;
- adicionar um `profile` devolve 400;
- atacar com um NPC do compêndio funciona até o fim.

**Frontend:** testes de `filterNpcs`, além de `tsc`, lint, testes e build.

**Manual:** navegador em `/npcs`, com filtros, ficha, perfil e adicionar ao combate, nos dois idiomas.

## Documentação

- README: seção do Compêndio de NPCs.
- `docs/DESENVOLVIMENTO.md`: formato do JSON, como rodar a extração e como corrigir um NPC.
