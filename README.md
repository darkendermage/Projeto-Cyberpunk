# Night City Item Database

Catálogo e gerenciador de itens para **Cyberpunk 2020 / Cyberpunk RED**: armas, munições, upgrades de armas (acessórios, Rebuilds e Tech Upgrades), itens avulsos, armaduras (incluindo Linear Frames, roupas e ACPA), cyberware (inclusive Fashionware), Netrunning (Cyberdecks, Programas, Black ICE e Hardware), drogas, veículos e melhorias de QG. Tem filtros avançados por categoria, busca global, fichas detalhadas, favoritos, listas, comparação e área administrativa com importação e exportação. As páginas de referência trazem regras rápidas, tabelas (alcance das armas em metros ou tiles de 2 m e Critical Injuries), glossário e os estilos de Artes Marciais com seus Special Moves. O **Gerador de Inimigos** monta NPCs (Mook, Lieutenant, Mini-Boss e Boss) nos modos automático, semiautomático e manual, com validação única baseada nos livros.

- **Backend:** C# / ASP.NET Core 8 Web API, EF Core 8, FluentValidation
- **Frontend:** React 18 + TypeScript (Vite), TanStack Query, React Router, CSS Modules
- **Banco:** PostgreSQL 16 (principal, com migrations). SQLite como modo local sem instalação.

## Executável para Windows (para enviar a amigos)

```powershell
powershell -ExecutionPolicy Bypass -File deploy\desktop\publicar.ps1
```

O script gera:

- `artifacts\NightCityItemDB.exe` (cerca de 50 MB): um arquivo só, com .NET, frontend, catálogo e configuração embutidos. Não precisa instalar nada.
- `artifacts\NightCityItemDB-win-x64.zip`: o `.exe` mais o `LEIA-ME.txt`, pronto para enviar.

Como o executável funciona:

- **Ao abrir:** dois cliques abrem uma janela de console e o navegador em `http://localhost:5090` (porta diferente da Coleção MESBG, que usa a 5080, então os dois rodam juntos). Clicar de novo com o programa aberto só reabre o navegador.
- **Onde ficam os dados:** num banco SQLite em `%LOCALAPPDATA%\NightCityItemDB\catalogo.db`, fora da pasta do programa. Trocar o `.exe` por uma versão nova mantém favoritos, listas e itens cadastrados.
- **Atualizações:** antes de uma migration, o programa guarda uma cópia do banco (as 3 mais recentes). Quando o catálogo embutido muda, ele é reimportado pelo `slug`, sem tocar em favoritos e listas.
- **Configuração:** a padrão está em `deploy/desktop/appsettings.Desktop.json`. Um `appsettings.Desktop.json` ao lado do `.exe` sobrescreve o padrão (por exemplo, para trocar a porta).
- **Aviso do Windows:** o `.exe` não é assinado, então o SmartScreen pode avisar na primeira execução ("Mais informações" > "Executar assim mesmo").

## Executar localmente (desenvolvimento)

Pré-requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e Node.js 20 ou superior. Para usar o PostgreSQL, você também precisa de Docker ou de um PostgreSQL 14+ instalado.

### 1. Banco de dados

**Opção A: PostgreSQL (recomendado)**

```bash
docker compose up -d          # sobe o Postgres em localhost:5432 (usuário/senha: cyberpunk)
```

Se você já tem um PostgreSQL, crie o banco `cyberpunkdb` e ajuste `ConnectionStrings:Postgres` em `backend/src/CyberpunkDb.Api/appsettings.json`.

**Opção B: SQLite (sem instalar nada)**

Basta rodar a API com `Database__Provider=Sqlite` (passo 2). O arquivo `cyberpunk.db` é criado automaticamente.

### 2. API

```bash
cd backend
dotnet run --project src/CyberpunkDb.Api --launch-profile http
# ou, sem Postgres:
# PowerShell:  $env:Database__Provider="Sqlite"; dotnet run --project src/CyberpunkDb.Api --launch-profile http
# bash:        Database__Provider=Sqlite dotnet run --project src/CyberpunkDb.Api --launch-profile http
```

Na primeira execução, a API aplica as migrations (Postgres) ou cria o schema (SQLite) e importa o catálogo de `data/seed/` quando o banco está vazio.

- API: http://localhost:5080/api
- Swagger: http://localhost:5080/swagger

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
```

Acesse **http://localhost:5173**. O Vite encaminha `/api` para `http://localhost:5080`; para outro endereço, defina `VITE_API_URL`.

Para produção, rode `npm run build` e copie `frontend/dist` para `backend/src/CyberpunkDb.Api/wwwroot`. A API passa a servir o site.

### Testes

```bash
cd backend && dotnet test     # 138 testes: filtros, validação, gerador de NPCs e integração da API (SQLite + seed real)
cd frontend && npm test       # modelo de filtros, codificação de URL, conversão para a API, estado do editor de NPCs e tabela de alcance
```

## Arquitetura

```
backend/
  src/CyberpunkDb.Domain          entidades e enums (rótulos pt-BR em [Display])
  src/CyberpunkDb.Application     schema das categorias, motor de filtros, serviços, validação
  src/CyberpunkDb.Infrastructure  EF Core (TPT), migrations Postgres, seed
  src/CyberpunkDb.Api             controllers REST, ProblemDetails
  tests/CyberpunkDb.Tests         unitários + integração (WebApplicationFactory)
frontend/src
  api/        cliente tipado + hooks React Query
  lib/        lógica pura de filtros (testada) e formatação
  features/   catalog (abas, cards, lista, filtros, ficha), search, favorites, lists, compare, admin, reference
  components/ layout (header com skyline, sidebar), UI base, arte vetorial dos itens
data/
  source/     compêndio original (markdown)
  source/errata/  correções conferidas com os PDFs (um arquivo por livro)
  seed/       catálogo no formato de importação (gerado)
tools/compendium-import/convert.mjs   conversor compêndio -> seed
```

### Modelo de dados

- **Table-per-type:** `Items` guarda os campos comuns (nome, preço, fabricante, fonte, página, sistema, origem, tags, texto de busca). Cada categoria tem sua tabela com colunas tipadas, como `Weapons` (ROF, dano, pente...), `ArmorItems` e `CyberwareItems`.
- **Relacionamentos:** armas e munições ligadas por calibre (`WeaponCalibers` e `AmmunitionCalibers`), cyberware ligado à sua peça base (`RequiresItemId`), além de fabricantes, livros, favoritos e listas.
- **Campos flexíveis** apenas onde fazem sentido: `ExtraAttributesJson` (`jsonb` no Postgres).
- **Subtipos com atributos próprios:** cada campo declara a quais subtipos se aplica. A API rejeita, por exemplo, SP em roupas, e a interface esconde o filtro correspondente.

### Filtros

Cada categoria declara seus campos uma única vez, em `Application/Catalog/Categories/*Category.cs`, com uma expressão C# tipada (`w => w.Rof`). Dessa declaração saem:

- o predicado SQL de cada operador (`eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `between`, `in`, `nin`, `all`, `isnull`);
- as facetas com contagem (cada uma calculada sem o próprio filtro);
- a ordenação, com valores vazios sempre por último;
- o schema que o frontend usa para montar filtros, cards, ficha, comparação e o formulário de edição.

No painel, as alterações ficam em rascunho até o clique em **Aplicar Filtros**. Atributos numéricos com poucos valores (ROF, dados, alcance) usam seletores de mínimo e máximo preenchidos pelo próprio schema. A chave "Operadores avançados" libera =, ≠, >, < e "sem valor".

O catálogo abre em **tabela** paginada. As colunas de atributos de cada categoria vêm de `TableFields`, e em telas estreitas as colunas Fabricante e Fonte passam para baixo do nome. A **grade** de cards continua disponível.

Critérios de campos diferentes são combinados com **E**. Valores do mesmo campo são combinados com **OU**, ou com **todos** no caso de propriedades. Toda a filtragem, paginação e busca acontece no backend. A URL guarda o estado, então dá para compartilhar um filtro como link.

**Adicionar um atributo** exige três passos: criar a propriedade na entidade, adicionar uma linha `Number(...)`/`Choice(...)` na categoria e gerar uma migration. **Adicionar uma categoria** exige criar a entidade, uma classe `*Category`, registrá-la em `CategoryRegistry` e gerar uma migration.

PostgreSQL e SQLite têm conjuntos de migrations separados, porque os tipos de coluna diferem. Gere os dois:

```bash
cd backend
dotnet tool restore
# PostgreSQL
dotnet ef migrations add NomeDaMudanca --project src/CyberpunkDb.Infrastructure --startup-project src/CyberpunkDb.Api --output-dir Persistence/Migrations
# SQLite (executável desktop)
dotnet ef migrations add NomeDaMudanca --project src/CyberpunkDb.Migrations.Sqlite --startup-project src/CyberpunkDb.Api --output-dir Migrations -- --provider Sqlite
```

## Categorias e tabelas de referência

- **Cyberware** inclui o Fashionware (tipo "Fashionware"), como no Core (p.111): 7 Option Slots sem peça base.
- **Netrunning** reúne Cyberdecks, Programas, Black ICE e Hardware de Cyberdeck, com classe, ATK, DEF, REZ, PER e SPD (Black ICE) e os slots: os que cada item ocupa e, nos decks, os livres, os só de Programa e os só de Hardware (Core p.196–208; Interface RED 3 p.39–42). Agents e Apps continuam em Itens Avulsos.
- **Veículos** têm o rank de Moto do Nomad para entrar no Family Motorpool: Core p.162 e o valor "Nomad Access" de Black Chrome, 12 Days of Vehiclemas e Interface RED 4. Fica vazio quando o livro diz que o veículo não pode ser obtido pelo Moto (bicicleta, JetBoy Hoverboard, Sky Queen).
- **Upgrades de Armas** têm o campo "Compatível com": as classes de arma em que o upgrade cabe, filtráveis e exibidas em grupos (Pistolas, Shoulder Arms...). As condições ("não Exóticas", "só Shoulder Arms") seguem em "Restrições de compatibilidade". A ficha da arma lista as munições e os upgrades compatíveis.
- **Tabelas** traz a tabela de DV por alcance do Core (p.173), tiro único e Autofire, em metros ou em tiles de 2 m (a medida que o próprio livro usa para áreas, p.174), e as tabelas de Critical Injury do corpo e da cabeça (p.187–188). A ficha de cada arma mostra a linha da sua classe.

A troca de categorias tem migration própria (`NetrunningAndMoto`): as linhas de Fashionware e dos itens de netrunning mudam de tabela mantendo o Id, então favoritos, listas e NPCs salvos continuam apontando para eles.

## Gerador de Inimigos

Página `/enemies`. Toda ficha, gerada ou montada à mão, passa pelo mesmo validador (`Application/Npcs/Validation/NpcValidator.cs`), exposto em `POST /api/npcs/validate`. O frontend não contém regras do jogo: ele envia a ficha e mostra o relatório (orçamentos, valores derivados, erros, avisos e fontes).

- **Automático:** escolhe categoria (e Hardened), perfil opcional, seed opcional e implantes obrigatórios/proibidos. O gerador distribui STATs e perícias pelos pesos do perfil, compra equipamento e cyberware dentro dos orçamentos e valida. Se a ficha não passar, tenta outra seed (até 25 vezes) e, no fim, explica o motivo.
- **Semiautomático:** os valores travados (cadeado), o nome e o conceito são mantidos; o gerador completa o resto com o orçamento que sobra. Escolhas impossíveis (STATs travados acima do orçamento, obrigatório que depende de um proibido, cyberware obrigatório caro demais) são recusadas antes de gerar, com a sugestão de ajuste.
- **Manual:** editor completo com validação ao vivo. Implantes são instalados por `POST /api/npcs/cyberware/install`, que usa o mesmo planner do gerador (adiciona a peça base, aloca slots e pares, recusa proibidos).

### Regras e fontes

Os parâmetros ficam em um único arquivo, `backend/src/CyberpunkDb.Application/Npcs/Rules/npc-rules.json`. Cada valor traz a página conferida no PDF (`source`) ou a marca `complementary` quando é uma regra adotada pela aplicação. Um teste garante que nenhum valor fique sem uma das duas. A página mostra tudo isso em **Fontes e parâmetros**.

| Regra | Fonte |
|---|---|
| Templates Mook/Lieutenant/Mini-Boss/Boss e Hardening (STAT, perícia, HP/SP sugeridos, bases, orçamentos, qualidade de arma, Role) | DGD p.152 |
| Perícias gratuitas (Native Language 4, mais 13 perícias em 2), STAT entre 2 e 8, LUCK fora do orçamento | DGD p.151–152 |
| Skill Base = STAT + nível, com penalidade de armadura | DGD p.7 |
| HP, Seriously Wounded, Death Save | CP:R p.79 |
| Lista de perícias, STAT ligado e custo (x2) | CP:R p.81–85 |
| Preço de armas Poor/Excellent; exóticas só na qualidade padrão | CP:R p.342 |
| Opção ocupa 1 slot quando não indicado; Internal/External/Fashionware com 7 slots; instalação incluída no preço | CP:R p.110–111 |
| Speedware exclusiva; Cyberaudio Suite única; Chipware Socket ocupa 1 slot e recebe 1 chip | CP:R p.112–113 e p.359 |
| Implanted Linear Frame muda HP e Death Save | CP:R p.367 |
| Hardened sem itens Poor Quality, exceto Mooks | IR5 p.106 |

O DGD (p.150) chama os templates de "guidelines, not hard rules". Por isso, violar um orçamento ou máximo é um erro que bloqueia o status **Válido**, mas o Mestre pode aceitá-lo com uma justificativa: a ficha fica **Válida com exceções**. Erros mecânicos (peça base ausente, slots estourados, item inexistente) não aceitam exceção. Faixas sugeridas (HP, SP, bases) geram só avisos. Uma ficha com erros só pode ser salva como **Rascunho**. O status é sempre recalculado no servidor.

Regras complementares (sem valor oficial): o (x2) do Core também conta no orçamento de perícia do DGD; uma armadura por local; capacidade do corpo (dois olhos, dois braços, duas pernas, um Neural Link); probabilidade de Role por categoria; e os oito perfis de inimigo, que orientam o gerador mas não são regras.

**Divergência registrada:** o catálogo marca o Chipware Socket com 0 slot. O validador segue o Core (p.359), via `cyberware.overrides` em `npc-rules.json`.

## Dados

| Origem | O que é | Como aparece |
| --- | --- | --- |
| `Compendium` | 1 item convertido de `data/source/compendio_completo_cyberpunk_red.md` e ainda não conferido (Nanoswarm Incubator) | aviso na ficha: "não conferido com os livros" |
| `Verified` | 1.058 itens conferidos com PDFs oficiais (*Core Rulebook*, *Black Chrome*, *Black Chrome+*, *Interface RED 2 a 5*, *Edgerunners Mission Kit*, *Danger Gal Dossier*, *Tales of the RED*, *12 Days of ...*, *Woodchipper's Garage*, *Toggle's Temple*, *Hornet's Pharmacy*, *All About Agents*, *Segotari Power*, *Going Quiet*, *Cyberware Enhancements*, *No Place Like Home*, *Mixing Drinks–Changing Lives*, *Invented Tech Upgrades*); correções em `data/source/errata/` | selo **CONFERIDO**, com fonte e página na ficha |
| `Demo` | nenhum no catálogo atual (os de Fashionware e roupas foram trocados pelos do *Core Rulebook*) | selo **DEMO** no card |
| `Custom` | itens cadastrados na Administração | selo **CUSTOM** |

O compêndio não traz alcance nem munição compatível por arma. Esses valores são **derivados da classe da arma**, com exceções para armas exóticas e combinadas. A página *Tabelas* da aplicação documenta essa regra. Itens duplicados entre tabelas do compêndio foram mantidos uma única vez.

Para regenerar o seed depois de editar o compêndio: `node tools/compendium-import/convert.mjs`. A importação atualiza os itens pelo `slug`.

O conversor aplica depois as erratas de `data/source/errata/`, um arquivo JSON por PDF conferido:

- `updates` corrige itens pelo slug. O slug não muda, então favoritos e listas continuam valendo. Em `attributes`, `null` apaga o valor.
- `additions` inclui itens que faltavam no compêndio.
- `remove` tira do catálogo entradas duplicadas do compêndio.
- `move` passa um item para outra categoria: troca `category` e `attributes` inteiros e mantém slug, textos e fonte. Ao reimportar, a aplicação recria esses itens na tabela nova e reaponta favoritos e entradas de lista.
- `source`, no topo do arquivo, vale para toda entrada que não informe a própria fonte.

Os arquivos `zz-*` rodam por último e documentam mudanças que cruzam vários livros: `zz-move-fashionware.json` e `zz-move-netrunning.json` (mudança de categoria, com os atributos conferidos), `zz-nomad-access.json` (rank de Moto dos veículos) e `zz-weapon-compatibility.json` (tags "Compatível com" dos upgrades de armas). O campo `about` de cada um diz as páginas usadas.

Todo item tocado pela errata recebe a origem `Verified`. As notas de cada um dizem a página do PDF e o que estava errado.

Ao reimportar um catálogo atualizado, a aplicação apaga os itens que vieram de um seed anterior e não estão mais no atual (origens `Compendium`, `Verified` e `Demo`). Itens `Custom` nunca são apagados.

## Observações

- Não há autenticação. A área de Administração e os favoritos e listas servem a um único perfil local. Coloque a API atrás de autenticação antes de expô-la publicamente.
- Os efeitos visuais (scanlines, glitch, animações, intensidade do neon, skyline) podem ser desligados na engrenagem do cabeçalho. A preferência do sistema por movimento reduzido é respeitada.
