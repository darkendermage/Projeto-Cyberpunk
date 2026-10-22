# Night City Item Database: guia técnico

Documentação para quem vai mexer no código. A apresentação do aplicativo para jogadores está no [README](../README.md).

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
- `artifacts\NightCityItemDB.exe.sha256`: o hash que a atualização automática confere.

Parâmetros opcionais: `-Versao 1.3.0` (padrão `0.0.0-dev`) e `-Repositorio dono/repo` (o repo do GitHub de onde o programa busca atualizações; sem ele, a atualização automática fica desligada). As versões oficiais saem pelo GitHub Actions (abaixo), não deste script rodado à mão.

Como o executável funciona:

- **Ao abrir:** dois cliques abrem uma janela de console e o navegador em `http://localhost:5090` (porta diferente da 5080 usada pela API em desenvolvimento). Clicar de novo com o programa aberto só reabre o navegador.
- **Onde ficam os dados:** num banco SQLite em `%LOCALAPPDATA%\NightCityItemDB\catalogo.db`, fora da pasta do programa. Trocar o `.exe` por uma versão nova mantém favoritos, listas e itens cadastrados.
- **Atualizações:** antes de uma migration, o programa guarda uma cópia do banco (as 3 mais recentes). Quando o catálogo embutido muda, ele é reimportado pelo `slug`, sem tocar em favoritos e listas.
- **Configuração:** a padrão está em `deploy/desktop/appsettings.Desktop.json`. Um `appsettings.Desktop.json` ao lado do `.exe` sobrescreve o padrão (por exemplo, para trocar a porta).
- **Aviso do Windows:** o `.exe` não é assinado, então o SmartScreen pode avisar na primeira execução ("Mais informações" > "Executar assim mesmo").

### Lançar uma versão (atualização automática)

```bash
git tag -a v1.3.0 -m "O que mudou nesta versão"
git push origin v1.3.0
```

O workflow `.github/workflows/release.yml` roda os testes do backend e do frontend, gera o executável com essa versão e cria a Release no GitHub com `NightCityItemDB.exe`, `NightCityItemDB.exe.sha256` e o `.zip`. A mensagem da tag vira as notas da versão. A tag precisa estar no formato `vX.Y.Z`.

Nos executáveis já distribuídos (`Desktop/AppUpdater.cs`):

- Ao abrir, o site consulta `GET /api/app/update`, que lê a última Release do repositório (cache de 1 hora; sem internet, nada aparece).
- Havendo versão maior, aparece um aviso no topo com **Atualizar agora**, as novidades e a opção de esconder até a próxima versão.
- **Atualizar agora** chama `POST /api/app/update`: baixa o `.exe` novo ao lado do atual (`.new`), confere o SHA-256, renomeia o atual para `.old`, põe o novo no lugar e reinicia. A aba aberta espera o programa voltar e recarrega. O `.old` é apagado na abertura seguinte.
- O banco fica em `%LOCALAPPDATA%`, então favoritos, listas e personagens continuam. Se a versão nova tiver migration, o backup automático continua valendo.
- A pasta do `.exe` precisa ser gravável (Downloads, Documentos, Área de Trabalho). Em `Arquivos de Programas`, o aviso explica o que fazer.
- O repositório vem do build (`-p:UpdatesRepository`, que o workflow preenche com o próprio repo). `"Updates": { "Enabled": false }` num `appsettings.Desktop.json` ao lado do `.exe` desliga a checagem.
- Executáveis anteriores a esse recurso não sabem se atualizar: a primeira versão com atualização automática precisa ser enviada à mão.

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
cd backend && dotnet test     # 335 testes: filtros, validação, gerador de NPCs, regras de personagem, Night Market, atualização automática e integração da API (SQLite + seed real)
cd frontend && npm test       # filtros, URL, editor de NPCs, tabela de alcance, perícias, Lifepath, anatomia do cyberware e Night Market
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
  features/   catalog (abas, cards, lista, filtros, ficha), search, favorites, lists, compare, admin, reference, characters, night-market
  components/ layout (header com skyline, sidebar), UI base, arte vetorial dos itens
data/
  source/     compêndio original (markdown)
  source/errata/  correções conferidas com os PDFs (um arquivo por livro)
  source/night-market-index.json, black-chrome-booths.json  extraídos dos PDFs (Night Market)
  seed/       catálogo no formato de importação (gerado)
tools/compendium-import/convert.mjs   conversor compêndio -> seed
tools/night-market/extract.mjs       Night Market Index e booths do Black Chrome a partir dos PDFs
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

## Personagens

Páginas `/characters` (gerenciador) e `/characters/{id}/{aba}`. Cada personagem é uma linha própria no banco, com a ficha em JSON (`Characters.SheetJson`), o histórico em `CharacterEvents` e o retrato em `CharacterPortraits`. Trocar de personagem nunca mistura dados: o editor de cada ficha é recriado ao trocar.

### Criação

| Método | STATs | Perícias | Equipamento |
|---|---|---|---|
| **Streetrat** | uma linha inteira da tabela da Role, 1d10 ou escolhida (p.73–77) | template fixo da Role (p.86–87) | pacote da Role (p.98, 103, 117) + 500 eb |
| **Edgerunner** | 1d10 por STAT, cada um na sua coluna (p.77); aceita dados físicos digitados | 86 pontos nas 20 perícias da Role, de 2 a 6 (p.88) | pacote da Role + 500 eb |
| **Complete Package** | 62 pontos, 2 a 8 (p.78); variantes de 50 a 80 pontos | 86 pontos, máx. 6, 13 básicas ≥ 2 (p.90) | 2.550 eb gerais + 800 eb só de moda (p.104–105) |

O idioma de origem entra no nível 4 sem custo (p.45); Streetslang não conta como idioma de origem. Perícias (x2) custam o dobro. A conclusão é recusada enquanto houver erro bloqueante; ao concluir, o Streetrat e o Edgerunner recebem o pacote da Role com a Humanity Loss pré-definida, e o Complete Package guarda a sobra do orçamento geral e perde a de moda. Variantes de campanha (ex.: Major Hero) ficam gravadas no personagem e aparecem marcadas como variante.

### Abas

- **Personagem:** STATs efetivos (base nunca é sobrescrita; cyberware como Grafted Muscle and Bone Lace e os Implanted Linear Frames já entram no valor), Habilidade de Role clicável com o resumo das regras, perícias com LVL/MOD/BASE e busca em português ou inglês, HP e Wound States, Death Save e sua penalidade (só avança por ação explícita), Critical Injuries com Quick Fix e Treatment, armaduras com ablação, armas com munição, Autofire/Supressão e anexos, LUCK, modificadores temporários, progressão por IP e histórico.
- **Caminho de Vida:** tabelas do Lifepath geral e um Lifepath por Role do personagem (com as ramificações do livro), relações (amigos, inimigos, aliados, amores, contatos), moda e estilo, moradia e lifestyle com pagamento mensal e status calculado.
- **Equipamento:** saldo e lançamentos, loja, itens recebidos sem custo (troca entre jogadores, presente, saque; só depois da criação), inventário com estado (equipado, carregado, guardado; armadura na cabeça ou no corpo), venda e descarte, upgrades das armas e os vestíveis com Option Slots. Disparar, recarregar e a ablação de armadura continuam na aba Personagem.
- **Cyberware:** na clínica, "Onde instalar" põe uma Option direto num vestível do inventário (Smart Glasses, Battleglove, Smart Ears...) sem precisar de Cybereye ou Cyberarm; o painel "Vestíveis com slots" (aba Equipamento) mostra slots, a mão da luva e o que ela cobre. Busca e categoria empilhadas na clínica, modelo anatômico em SVG com as regiões do livro, slots por região, clínica filtrada pela região, prévia da instalação (peças-base incluídas, lado, custo, faixa de Humanity Loss, teto da terapia), remoção, substituição e terapia.

### Exportar e importar

Cada personagem pode ser exportado para um arquivo `.cyberpunk.json` (botão de download no cartão do gerenciador ou no cabeçalho da ficha) e importado em outro computador ou banco pelo botão **Importar** do gerenciador. O arquivo leva a ficha, o histórico e o retrato; valores calculados não vão, o servidor recalcula ao importar. A importação sempre cria um personagem novo (com "(importado)" no nome se já houver um igual). Os itens são reencontrados pelo identificador do catálogo (`slug`), não pelo número interno, e os que o catálogo de destino não tiver ficam de fora com aviso, junto com o cyberware instalado neles. Um arquivo editado à mão entra com as pendências apontadas pela validação. O formato tem versão (`formatVersion`): um aplicativo antigo recusa arquivos de uma versão mais nova.

### Criação, loja e efeitos

- A criação tem seis passos: Identidade, **Caminho de Vida** (como no livro, logo depois da Role, CP:R p.43), STATs, Perícias, Equipamento e Resumo. O botão **Rolar o que falta** (também na aba Caminho de Vida) rola só as tabelas em branco: origem cultural, tabelas gerais e de estilo, amigos/amores/inimigos (uma vez só, mesmo que o resultado seja zero) e o fluxograma da Role seguindo as ramificações; perguntas de escolha ficam para o jogador. O Lifepath é opcional e não impede a conclusão.
- A loja (criação e "Adicionar itens") é paginada no servidor e alcança o catálogo inteiro; clicar no nome mostra a descrição do item.
- Os efeitos de cyberware ficam em `cyberwareEffects` no JSON de regras: bônus de STAT com teto e acúmulo, bônus de perícia (somados à BASE quando valem sempre, só anotados quando dependem da situação, como "+2 em testes que envolvem visão") e efeitos de jogo exibidos no painel "Efeitos do cyberware". Um teste confere que cada efeito cita página e aponta para um item existente no catálogo.

Atalhos: Ctrl+S salva na hora; Alt+1 a Alt+4 trocam de aba (Personagem, Caminho de Vida, Equipamento e Cyberware). Campos de texto salvam sozinhos em fila; ações de regra são comandos no servidor. Toda gravação leva a versão da ficha: se outra janela salvou antes, a API responde 409 em vez de sobrescrever.

### Regras e fontes

As regras ficam em `backend/src/CyberpunkDb.Application/Characters/Rules/character-rules.json`, no mesmo formato do Gerador de Inimigos: cada valor tem a página (`source`) ou a marca `complementary`, e um teste garante isso. Perícias, STATs e as regras estruturais de cyberware vêm de `npc-rules.json`, compartilhado.

| Regra | Fonte |
|---|---|
| Métodos de criação, orçamentos, tabelas de STAT e perícias das 10 Roles | CP:R p.73–90 |
| Pacotes iniciais das Roles e Humanity Loss pré-definida | CP:R p.98, 103, 117 |
| Lifepath geral e por Role | CP:R p.45–69 |
| HP, Seriously/Mortally Wounded, Death Save, Critical Injuries, Quick Fix e Treatment | CP:R p.79, 186–188, 222 |
| Humanidade, EMP efetiva e terapia | CP:R p.80, 229–230 |
| Armadura (maior SP, penalidade uma vez, ablação), Autofire, Supressão, 3 Attachment Slots | CP:R p.95, 173–174, 184–186 |
| HL fixa na criação e rolada depois; membro de carne; coberturas e Hardened Shielding | CP:R p.110–116 |
| Full Body Conversion: 0 HL e sem redução do teto nas peças listadas, slots dobrados | Interface RED 3 p.93 |
| Vestíveis com Option Slots: Smart Glasses (2 Cybereye, par conta como um), Smart Lens (1, sem par), Battleglove (3 de braço), Smart Gloves MechaMan/CyberDude/Dynalar, Smart Ears (2 Cyberaudio), capacete MechaMan; a luva desliga as Options do braço por baixo | CP:R p.347, 354; DGD p.148; BC p.37; IR4 p.67; IR5 p.99–100 |
| Lifestyle, moradia e semana de carência | CP:R p.377–378 |
| Custos de IP e multiclasse | CP:R p.142, 410 |
| Efeitos de cyberware na ficha (STAT, perícias, efeitos de jogo) | CP:R p.358–367 (`cyberwareEffects`) |
| Resumo das Habilidades de Role | CP:R p.144–162 |

Divergências registradas:

- **Seriously Wounded:** o livro diz "menos que" metade do HP. A comparação é configurável (`comparison` no JSON) para quem quiser "igual ou abaixo".
- **Wolvers e Tool Hand:** o catálogo liga essas opções ao Cyberarm, mas o Core permite instalá-las como única peça num membro de carne. Os pacotes de Solo, Tech e Nomad usam isso.
- **Revenda:** o Core não fixa preço de revenda (p.385). O valor combinado na mesa entra como lançamento manual.

Regras complementares (sem valor oficial): Options em vestíveis não custam Humanity (os livros não dizem; a Tetra do DGD, p.59, leva "Battleglove w/ Tool Hand" em GEAR com cyberware "None"; ajustável em `wearableHumanityLoss`), uma cobertura por membro, peças removidas voltam ao inventário, dois braços e duas pernas de carne, e um Lifepath por Role no multiclasse (o Core só o roda na criação, p.53, e não o menciona no multiclasse, p.143: o da criação é mantido e o da Role nova é opcional).

### Extensões preparadas

- **Módulos de Role:** a interface `IRoleModule` (`Characters/Roles/RoleModules.cs`) recebe os dados próprios de cada Role em `RoleData` e entra na validação. Nenhum módulo é obrigatório.
- **Moradia:** o pagamento mensal é registrado por ação; cobrança automática e consequências de inadimplência ficam para depois. O status já é calculado com a semana de carência.
- **QG** (HQ IP, instalações): o catálogo já tem as melhorias de QG; a ficha ainda não as vincula.

## Night Market

Página `/night-market` (em **Outros**) e `/night-market/{id}`. Cada mercado é uma linha própria no banco (`NightMarkets.StateJson`): booths, listagens, vendas e histórico. As compras são avulsas: vão para o livro do mercado e não mexem nas fichas dos personagens. Como nas outras áreas, o frontend não tem regra de jogo; preços com condição e Haggle são calculados no servidor a cada leitura e compra. Toda gravação leva a versão do mercado (409 se outra janela salvou antes).

- **Mercados:** escolha um mercado pronto, ajuste organizador, Operator Rank, tipos de mercadoria (rolados, sugeridos pelo livro ou escolhidos), linhas por tipo (dados físicos), livros do acervo, seed, chance de itens Damaged e golpe, e gere. A mesma seed monta o mesmo mercado; **Rolar de novo** troca a seed e mantém o livro de vendas.
- **CBK:** o app do Black Chrome (p.6–8): abas por categoria, busca, mapa de booths `@A1`..., cards com foto, fabricante, preço, sales pitch, **Buy Now!!!** e "Pick-up or Browse at Booth". A **Visão do Mestre** mostra golpes, origem de cada listagem, avisos e as ferramentas de edição (condição, estoque, booth, preço combinado, remover, adicionar do catálogo, do índice ou nome e preço).
- **Haggle:** COOL + Trading + Operator Rank + 1d10 dos dois lados, com críticos e empate para o Defensor; os dados podem ser digitados. Vencendo, um acordo do Rank ou abaixo.
- **Eventos:** espaços "à escolha do Mestre" (escolher ou sortear), Midnight Market, contador de visitantes, Door Prize da Molly (roleta) e Mystery Boxes do Mr. Amaze.
- **Night Market Index:** as 1.177 linhas do índice (v1.1, jan. 2025), com filtro por seção, categoria de preço e "com ficha"/"só no índice"; 954 abrem a ficha do catálogo.
- **Ferramentas do Fixer:** quanto um Fixer paga por um item (condição, se o vendedor avisa, fica quieto ou disfarça, e Haggle de quem vende), teste de Haggle e a Table of Mystery avulsa (Mystery Box ganha com Achievement).

| Mercado | Organizador | Fonte |
|---|---|---|
| Night Market (Core) e Personalizado | Fixer de Operator 5+ | CP:R p.337–339 |
| A 3-Piece's Joint · B Woodchipper's Block Party · C Molly Anderson's Market · D Mister K's Midnight Market · E Ms. Mynah's · F Rex Royale's Casino | Operator 5, 5, 6, 9, 6 e 8 | BC p.131–155 (Molly também BC+ p.2–6) |
| Midnight With the Upload · Must Have Cyberware Deals (Mr. Amaze) · Woodchipper's Garage | The Upload · Mr. Amaze · Woodchipper (Nomad) | IR3 p.25–30, 37–50 |
| Hornet's Pharmacy · Mystery Boxes | Hornet · Mr. Amaze | IR4 p.3–8, 23–28 |
| Toggle's Temple (arsenal: armas só emprestadas, entrada 20eb) | Toggle | IR5 p.35–60 |

### Regras e fontes

Os parâmetros ficam em `backend/src/CyberpunkDb.Application/NightMarkets/Rules/night-market-rules.json`, no mesmo formato das outras regras (página em `source` ou marca `complementary`, conferido por teste). A aba **Fontes e parâmetros** mostra tudo.

| Regra | Fonte |
|---|---|
| Gerador: 1d6 duas vezes sem repetir, 1d10 linhas por tipo, d100 em 20 faixas sem repetir; todas as qualidades presentes; Option traz a peça Foundational; cyberware instalado pelo ripper do vendedor, Fashionware no local | CP:R p.338–339 |
| Midnight Market: 1d10+5 itens raros, Fixer de Rank 9 | CP:R p.161, 339 |
| Reach, Night Market a partir do Rank 5, Haggle e acordos por Rank | CP:R p.159–161 |
| Empate fica com o Defensor; críticos (10 soma, 1 subtrai) | CP:R p.129–130 |
| Oito categorias de preço; sem Fixer, só até Premium | CP:R p.385 |
| Preço Poor/Excellent das armas | CP:R p.342 (`npc-rules.json`) |
| CBK, booths `@#`, Buy Now!!!, taxa por listagem | BC p.6–8 |
| Damaged 1 categoria abaixo, Destroyed 2, Operator 4+ percebe o dano, poucos compram Damaged de 20eb ou menos | IR5 p.5 |
| Mystery Boxes: troca por Achievement, Table of Mystery 1d10 × 1d6 | IR4 p.24, 27–28 |
| Door Prize: bônus de Smash e máscara, exemplo das mil balas de Junk Ammunition | BC+ p.2–3 |
| Categoria de disponibilidade diferente do preço | IR2 p.66; IR5 p.37 |

Divergências registradas:

- **Very Heavy Melee Weapon:** a tabela do gerador diz 100eb (Premium); a de armas (CP:R p.340) e o catálogo dizem 500eb. Vale o catálogo.
- **Itens que o catálogo não tem:** comidas e bebidas da tabela, Bows & Crossbows genérico, Radiation Suit e Shovel or Axe entram com nome e preço da tabela.
- **Black Chrome+ no catálogo:** o slug de "Black Chrome+" vira `black-chrome` na importação, então os 12 itens do BC+ aparecem com a fonte "Black Chrome". O estoque da Molly usa os slugs dos itens para não depender disso.

Regras complementares (sem valor oficial): a roleta do Door Prize (o BC+ só narra o sorteio) e o prêmio a cada 50 visitantes; o sorteio de ajuda das escolhas do Mestre e do Midnight Market (Luxury ou mais, ou arma exótica); itens de outros livros por tipo sorteado; a chance de itens Damaged na segunda mão; piso em Cheap ao descer de categoria; porcentagens arredondadas para baixo; e o estoque, que só é controlado quando o Mestre informa (o Core lista tipos, não o estoque).

### Dados extraídos dos PDFs

`node tools/night-market/extract.mjs ["pasta dos PDFs"]` (precisa do `pdftotext` no PATH) gera `data/source/night-market-index.json` (o índice, ligado ao catálogo pelo nome) e `data/source/black-chrome-booths.json` (os booths de cada listagem do Black Chrome). As poucas linhas que o layout do PDF quebra estão corrigidas à mão no próprio script, cada uma com a página. Os dois arquivos vão embutidos na aplicação.

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

- Não há autenticação. A área de Administração, os favoritos, as listas e os personagens servem a um único perfil local. Coloque a API atrás de autenticação antes de expô-la publicamente.
- Os efeitos visuais (scanlines, glitch, animações, intensidade do neon, skyline) podem ser desligados na engrenagem do cabeçalho. A preferência do sistema por movimento reduzido é respeitada.
