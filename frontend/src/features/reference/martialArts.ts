// Martial Arts Forms and Special Moves, summarized from the official books (page numbers are the printed ones).

export interface SpecialMove {
  name: string;
  requirement: string;
  effect: string;
}

export interface MartialArtForm {
  name: string;
  source: 'Core Rulebook' | 'Interface RED 4' | 'Interface RED 5';
  page: number;
  description: string;
  moves: SpecialMove[];
  /** Innate fighting styles that NPCs have but characters cannot learn. */
  innate?: boolean;
}

export const SHARED_MOVES: SpecialMove[] = [
  {
    name: 'Recovery',
    requirement: 'Nenhum. Todos os estilos podem usar.',
    effect: 'Ao usar a Ação Get Up, tenta vencer DV13 com a Resolução de Special Move. Se passar, levantar não gasta a Ação.',
  },
];

export const MARTIAL_ARTS: MartialArtForm[] = [
  // ---------------------------------------------------------------- Core Rulebook
  {
    name: 'Aikido', source: 'Core Rulebook', page: 179,
    description: 'Estilo suave de varreduras e travas para desarmar o oponente e voltar a força dele contra ele mesmo.',
    moves: [
      { name: 'Disarming Combination', requirement: 'Acertou o mesmo alvo com um ataque de Brawling e um de Martial Arts neste turno.', effect: 'Uma vez por turno, DV15: um objeto nas mãos desse alvo passa para as suas ou cai no chão.' },
      { name: 'Iron Grip', requirement: 'Tem um alvo agarrado (Grab) que ainda não está sob Iron Grip.', effect: 'Uma vez por turno, como Ação, DV15: o alvo tem -2 a mais para escapar do Grapple e não faz ataques à distância até o Grapple acabar.' },
    ],
  },
  {
    name: 'Judo', source: 'Core Rulebook', page: 179,
    description: 'Estilo suave de agarrões, arremessos e escapadas.',
    moves: [
      { name: 'Counter Throw', requirement: 'Esquivou de todos os ataques corpo a corpo feitos contra você desde seu último turno.', effect: 'Uma vez por turno, como Ação, DV15: usa Throw num alvo em alcance cujo ataque você esquivou, sem precisar agarrá-lo. O arremesso não pode ser evitado.' },
      { name: 'Grab Escape', requirement: 'Acertou com 2 ataques corpo a corpo quem está te agarrando neste turno.', effect: 'Uma vez por turno, DV15: você sai do Grapple e o alvo sofre Broken Arm (você escolhe o braço), se ainda não tiver.' },
    ],
  },
  {
    name: 'Karate', source: 'Core Rulebook', page: 179,
    description: 'Estilo duro de golpes feitos para quebrar ossos e armaduras.',
    moves: [
      { name: 'Armor Breaking Combination', requirement: 'Acertou o mesmo alvo com uma arma corpo a corpo e um ataque de Martial Arts neste turno.', effect: 'Uma vez por turno, DV15: toda armadura usada pelo alvo é ablatada em 2 pontos a mais.' },
      { name: 'Bone Breaking Strike', requirement: 'WILL 8 ou mais.', effect: 'No lugar dos 2 ataques de Martial Arts, um golpe com a Resolução contra um alvo em alcance: se acertar, além do dano ele sofre Broken Ribs. Com -8, mira a cabeça como num Aimed Shot e causa Cracked Skull.' },
    ],
  },
  {
    name: 'Taekwondo', source: 'Core Rulebook', page: 180,
    description: 'Estilo duro de chutes altos e golpes precisos em pontos de pressão.',
    moves: [
      { name: 'Pressure Point Strike', requirement: 'WILL 8 ou mais.', effect: 'No lugar dos 2 ataques, um golpe com a Resolução: se acertar, além do dano o alvo sofre Spinal Injury. Com -8, mira a cabeça como num Aimed Shot e causa Brain Injury.' },
      { name: 'Flying Kick', requirement: 'MOVE 8 ou mais e ter andado ao menos 4 m/yds neste turno.', effect: 'No lugar dos 2 ataques, gasta a Ação e o resto do movimento voando em linha reta até um alvo a até 4 m/yds. Se acertar, causa o dano de Martial Arts no corpo, derruba o alvo (Prone) e o tira de moto ou veículo sem cabine fechada.' },
    ],
  },

  // ---------------------------------------------------------------- Interface RED 4 (Cyberfists of Fury)
  {
    name: 'Arasaka-te', source: 'Interface RED 4', page: 76,
    description: 'Criada pela Arasaka antes da 4ª Guerra Corporativa. Fácil de ensinar, virou franquia de dojôs em centros comerciais.',
    moves: [
      { name: 'Counter Strike', requirement: 'É seu turno e você levou dano de Brawling, Martial Arts ou arma corpo a corpo desde seu último turno.', effect: 'Uma vez por turno, DV15: no resto do turno, Aimed Shots com Martial Arts ou arma corpo a corpo contra quem te causou esse dano têm -5 em vez de -8.' },
      { name: 'Escape Hold', requirement: 'É seu turno e você é o defensor num Grapple.', effect: 'Uma vez por turno, DV15: você sai do Grapple.' },
    ],
  },
  {
    name: 'Arnis', source: 'Interface RED 4', page: 76,
    description: 'Arte filipina centrada em armas, tida como uma das mais letais do mundo.',
    moves: [
      { name: 'Coordinated Combination', requirement: 'Acertou o mesmo alvo com dois ataques de Martial Arts (Arnis) ou dois ataques com armas corpo a corpo Leves ou Médias (ou armas em cyberware ou Battleglove) neste turno.', effect: 'Uma vez por turno, DV15: o alvo sofre Torn Muscle, sem o dano extra.' },
      { name: 'Disarming Technique', requirement: 'É seu turno, você esquivou de ao menos um ataque com arma corpo a corpo e de todos eles desde seu último turno, e está empunhando uma arma corpo a corpo.', effect: 'Uma vez por turno, DV15: uma arma corpo a corpo de um alvo em alcance que te atacou com ela passa para as suas mãos ou cai no chão.' },
    ],
  },
  {
    name: 'Boxing', source: 'Interface RED 4', page: 76,
    description: 'Socos e defesas. Simples e versátil: desgasta o oponente ou acaba com ele numa saraivada de golpes.',
    moves: [
      { name: 'Knockout Punch', requirement: 'BODY 8 ou mais.', effect: 'No lugar dos 2 ataques, um soco com a Resolução a -5: se acertar, vale como Aimed Shot na cabeça e o alvo sofre Broken Jaw além do dano.' },
      { name: 'Punch Combination', requirement: 'Acertou o mesmo alvo com dois ataques de Brawling neste turno.', effect: 'Uma vez por turno, DV15: mais um ataque de Brawling contra esse alvo.' },
    ],
  },
  {
    name: 'Capoeira', source: 'Interface RED 4', page: 77,
    description: 'Possivelmente criada por pessoas escravizadas no Brasil. Movimentos fluidos como uma dança.',
    moves: [
      { name: 'Rhythmic Recovery', requirement: 'Errou um ataque de Martial Arts (Capoeira) neste turno.', effect: 'Uma vez por turno, DV15: um ataque de Brawling contra um alvo em alcance.' },
      { name: 'Slash Dance', requirement: 'Neste turno, causou Lesão Crítica com arma corpo a corpo Leve ou Média (ou em cyberware ou Battleglove), ou gastou mais de 3 de Luck num ataque com elas ou no Rhythmic Recovery.', effect: 'Uma vez por turno, DV15: um ataque de Martial Arts (Capoeira) contra um alvo em alcance.' },
    ],
  },
  {
    name: 'Choy Li Fut', source: 'Interface RED 4', page: 77,
    description: 'Arte chinesa rápida e versátil, com golpes amplos que favorecem a luta contra vários oponentes.',
    moves: [
      { name: 'Shaolin Step', requirement: 'MOVE 6 ou mais. É seu turno, você esquivou de ao menos um ataque e de todos desde seu último turno, e ainda não usou Run.', effect: 'Uma vez por turno, DV15: usa a Ação Run sem gastar Ação (só uma vez no turno).' },
      { name: 'Sweeping Fist', requirement: 'MOVE 6 ou mais. Acertou o mesmo alvo com dois ataques de Martial Arts (Choy Li Fut) neste turno.', effect: 'Uma vez por turno, DV15: um ataque de Brawling contra outro alvo em alcance que você não acertou neste turno.' },
    ],
  },
  {
    name: 'Drunken Fist', source: 'Interface RED 4', page: 77,
    description: 'Arte chinesa que imita um bêbado para atacar e se defender de forma inesperada.',
    moves: [
      { name: 'Environmental Improvisation', requirement: 'LUCK 4 ou mais (o atributo, não o saldo atual).', effect: 'Uma vez por turno, DV13: escolhe um objeto ou cobertura em alcance. Se o Mestre concordar que doeria, seus ataques de Brawling perto dele ignoram metade da armadura neste turno (e o Mestre pode subir o dano até 4d6).' },
      { name: 'Lucky Stumble', requirement: 'LUCK 4 ou mais. Você está sendo atacado sem perceber.', effect: 'DV15, mesmo inconsciente: pode esquivar normalmente, como se esquivasse de balas. Na primeira vez que der certo na sessão, recupera 2 de Luck.' },
    ],
  },
  {
    name: 'Gun Fu', source: 'Interface RED 4', page: 78,
    description: 'Metade luta corpo a corpo, metade tiro de pistola, tratando a arma como extensão do corpo. Letal em ambientes fechados.',
    moves: [
      { name: 'Combat Reload', requirement: 'Acertou um ataque de Martial Arts (Gun Fu) e ainda não recarregou neste turno.', effect: 'Uma vez por turno, DV15: recarrega a arma empunhada sem gastar Ação.' },
      { name: 'Woo Technique', requirement: 'Handgun 4 ou mais, ou Combat Awareness 1. É seu turno e você empunha uma arma de Handgun de uma mão, ROF 2, com ao menos 1d6 de dano.', effect: 'Uma vez por turno, DV15: até o fim do turno, ataques de Martial Arts (Gun Fu) alcançam 25 m/yds (contam como corpo a corpo, ignoram metade da armadura e qualquer um pode esquivar), usam o dano da arma se for maior e os efeitos da munição gasta. O nível de Gun Fu cai ao de Handgun, se for maior. Sem munição, o ataque falha.' },
    ],
  },
  {
    name: 'Jujutsu', source: 'Interface RED 4', page: 78,
    description: 'Arte japonesa que cede à força do oponente e a redireciona contra ele.',
    moves: [
      { name: 'Aiki', requirement: 'Acertou um ataque de Martial Arts que não seja Gun Fu e vai causar dano.', effect: 'Uma vez por turno, DV15: no resto do turno, usa o BODY do alvo em vez do seu para o dano de Brawling e Martial Arts contra ele.' },
      { name: 'Throwing Technique', requirement: 'WILL 6 ou mais. Acertou o mesmo alvo com dois ataques de Martial Arts (Jujutsu) neste turno.', effect: 'Uma vez por turno, DV15: usa Throw nesse alvo mesmo sem agarrá-lo, podendo usar o BODY dele para o dano do arremesso.' },
    ],
  },
  {
    name: 'Kendo', source: 'Interface RED 4', page: 79,
    description: 'O "Caminho da Espada" japonês moderno, com linhagem que remonta aos samurais.',
    moves: [
      { name: 'Cut the Bullet', requirement: 'WILL 8 ou mais, Melee Weapon 6 ou mais e ciente do atacante. Empunhando arma corpo a corpo, você é alvo de um tiro único, de uma arma arremessada ou está na área de um explosivo.', effect: 'Resolução contra o ataque: se vencer, o ataque é anulado; se perder, acerta (e o explosivo passa a ter você no centro). Pode sacar e guardar a arma sem Ação. Não conta como esquiva e não vale contra ataques não físicos.' },
      { name: 'Ki Ken Tai no Ichi', requirement: 'Você está em combate.', effect: 'Uma vez por turno, como Ação, DV15: no próximo turno, o primeiro Aimed Shot com arma corpo a corpo tem -2 em vez de -8.' },
    ],
  },
  {
    name: 'Krav Maga', source: 'Interface RED 4', page: 79,
    description: 'Arte israelense que adapta Aikido, Boxe, Judô, Karatê e luta livre para a defesa pessoal real.',
    moves: [
      { name: 'Contact Combat', requirement: 'Neste turno, causou Lesão Crítica com ataque ou Special Move de Aikido, Boxing, Judo, Karate, Krav Maga ou Wrestling.', effect: 'Uma vez por turno, DV15: um ataque de Brawling contra quem sofreu a Lesão Crítica.' },
      { name: 'Punishing Blow', requirement: 'Esquivou de ao menos um ataque corpo a corpo e de todos os ataques desde seu último turno.', effect: 'Uma vez por turno, DV15: um ataque de Brawling contra quem te atacou corpo a corpo.' },
    ],
  },
  {
    name: 'Kung Fu', source: 'Interface RED 4', page: 79,
    description: 'A arte marcial chinesa original, ancestral de muitos outros estilos.',
    moves: [
      { name: 'Five Forms', requirement: 'Kung Fu 4 ou mais.', effect: 'No lugar dos 2 ataques, escolhe dois animais e faz a Resolução contra um alvo: se acertar, o primeiro vale agora e o segundo no próximo ataque corpo a corpo que acertar no combate. Grou: Damaged Eye sem dano extra. Dragão: +1 no próximo ataque contra o alvo. Leopardo: +2 MOVE até o fim do turno. Serpente: recupera 1 de Luck. Tigre: a armadura do alvo ablata 1 a mais.' },
      { name: 'Superior Stance', requirement: 'É seu turno e você acertou um ataque de Martial Arts.', effect: 'Uma vez por turno, DV17: você vai para o topo da fila de Iniciativa.' },
    ],
  },
  {
    name: 'Kyudo', source: 'Interface RED 4', page: 80,
    description: 'A arquearia japonesa, esporte e meditação. Hoje vem com golpes corpo a corpo que complementam o arco.',
    moves: [
      { name: 'Hassetsu', requirement: 'Você não está Prone nem agarrado e empunha uma arma de Archery (pode sacá-la como parte do requisito).', effect: 'Uma vez por turno, como Ação, DV15: entra na Kyudo Stance, e o primeiro ataque ROF 1 de Archery de cada turno causa +2d6. A postura acaba ao andar mais de 6 m/yds, levar dano corpo a corpo, ser agarrado, cair, perder o último arco ou o combate terminar. Assumir a postura equivale a sacar uma arma.' },
      { name: 'Zaiteki', requirement: 'Acertou um Aimed Shot com arma de Archery neste turno.', effect: 'Uma vez por turno, DV15: recupera até 2 de Luck.' },
    ],
  },
  {
    name: 'Militech Commando Training', source: 'Interface RED 4', page: 80,
    description: 'Sistema criado pela Militech para ensinar soldados a usar facas de combate.',
    moves: [
      { name: 'Combat Knife Training', requirement: 'O primeiro ataque do turno é com arma corpo a corpo Leve ou Média.', effect: 'DV15: até o fim do turno, a arma causa 4d6, mas fica com ROF 1.' },
      { name: 'Commando Disarm', requirement: 'Você tem duas mãos livres.', effect: 'Como Ação, Resolução contra o Brawling de um alvo em alcance que empunha uma arma: se vencer, pega uma das armas dele e pode atacá-lo com ela como se ainda não tivesse usado a Ação.' },
    ],
  },
  {
    name: 'Muay Thai', source: 'Interface RED 4', page: 80,
    description: 'Arte tailandesa de golpes, com condicionamento físico extremo (e, muitas vezes, cyberware igualmente extremo).',
    moves: [
      { name: 'Conditioned Ferocity', requirement: 'É seu turno e você causou dano com ataque de Martial Arts desde o início do seu último turno.', effect: 'Uma vez por turno, DV15: até o fim do turno, Brawling causa +1d6 (até 4d6) e tem +1.' },
      { name: 'Conditioned Power', requirement: 'É seu turno e você causou dano com Brawling desde o início do seu último turno.', effect: 'Uma vez por turno, DV15: o próximo ataque de Martial Arts no turno causa +1d6.' },
    ],
  },
  {
    name: 'Multiarm Melee', source: 'Interface RED 4', page: 81,
    description: 'Criada no vale do Yukon, no Estado Livre do Alasca, para enfrentar os ursos aumentados da Biotechnica e lutar contra a Petrochem.',
    moves: [
      { name: 'Armed & Dangerous', requirement: 'É seu turno e você tem mais braços que o alvo de um ataque de Brawling.', effect: 'Antes de rolar, DV15 (DV17 se já usou no turno): rola o d10 duas vezes e escolhe; ou faz Aimed Shot de Brawling a -5 em vez de -8; ou, se for Solo, redistribui Combat Awareness sem Ação.' },
      { name: 'Smack Together', requirement: 'BODY 6 ou mais. Você é o atacante em dois ou mais Grapples.', effect: 'Uma vez por turno, como Ação, DV15: causa seu BODY direto no HP de todos que você está agarrando.' },
    ],
  },
  {
    name: 'PanzerFaust', source: 'Interface RED 4', page: 81,
    description: 'Criada por FBCs para FBCs: soltar toda a força de um corpo de metal contra um de carne.',
    moves: [
      { name: 'Borg Fist', requirement: 'BODY 10 ou mais.', effect: 'No lugar dos 2 ataques, Resolução contra um alvo em alcance com BODY menor que o seu: se vencer, um ataque de Martial Arts (PanzerFaust) que causa 5d6 (6d6 se sua Humanidade estiver abaixo de 0).' },
      { name: 'Inner Chrome', requirement: 'Você é FBC ou tem Humanidade abaixo de 0, está em combate, e algum cyberware seu sofreu Lesão Crítica (exceto perda) ou foi danificado, desativado ou inutilizado (EMP, Quickhack...).', effect: 'Uma vez por turno, DV11 +2 por cyberware afetado: até o fim do combate, todo o cyberware volta a funcionar e as Lesões que aceitam Quick Fix contam como tratadas (sem restaurar SP). Até seu próximo turno, o cyberware fica imune a esses efeitos; com Humanidade abaixo de 0 e DV 15+, até o fim do combate e também contra destruição.' },
    ],
  },
  {
    name: 'Silat', source: 'Interface RED 4', page: 81,
    description: 'Fusão de artes indígenas do Sudeste Asiático, com centenas de estilos diferentes.',
    moves: [
      { name: 'Internal Power', requirement: 'WILL 6 ou mais. Acertou o mesmo alvo com dois ataques de Martial Arts (Silat) ou com armas corpo a corpo Leves ou Médias.', effect: 'Uma vez por turno, DV15: um ataque de Brawling contra um alvo em alcance.' },
      { name: 'Violent Leverage', requirement: 'É seu turno e, neste turno, você iniciou um Grapple com alguém que ainda não tinha agarrado no combate.', effect: 'Uma vez por turno, DV15: um ataque com arma corpo a corpo contra um alvo em alcance.' },
    ],
  },
  {
    name: 'Sov-System', source: 'Interface RED 4', page: 82,
    description: 'Sucessora do Command Sambo do Exército Vermelho, moldada pela SovOil para ser mais popular que a Arasaka-te.',
    moves: [
      { name: 'Dirty Blow', requirement: 'BODY 4 ou mais.', effect: 'No lugar dos 2 ataques, um golpe com a Resolução: se acertar, além do dano o alvo sofre uma Lesão Crítica de corpo aleatória (Foreign Object é rolada de novo).' },
      { name: 'Revenge Strike', requirement: 'BODY 4 ou mais. É seu turno e você sofreu uma Lesão Crítica de ataque corpo a corpo ou Special Move desde seu último turno.', effect: 'Uma vez por turno, DV15: um ataque de Martial Arts (Sov-System) contra quem a causou. Se causar Lesão Crítica, recupera até 2 de Luck.' },
    ],
  },
  {
    name: 'Sumo', source: 'Interface RED 4', page: 82,
    description: 'Luta japonesa focada em mover o oponente com todo o peso do corpo.',
    moves: [
      { name: 'Deashi', requirement: 'BODY 7 ou mais. Você está para ser agarrado ou arremessado (até pelo Counter Throw do Judô).', effect: 'DV15: não é agarrado nem arremessado e move você e o oponente até 6 m/yds na direção que escolher.' },
      { name: 'Niramiai', requirement: 'COOL 4 ou mais. Você está num Facedown.', effect: 'DV15: +2 no Facedown.' },
    ],
  },
  {
    name: 'Tai Chi', source: 'Interface RED 4', page: 82,
    description: 'Arte chinesa que desvia ou anula ataques mantendo o equilíbrio interior.',
    moves: [
      { name: 'Joint Manipulation', requirement: 'Alguém em alcance errou um ataque corpo a corpo em você.', effect: 'DV15: escolhe uma mão do atacante, que fica inutilizada até o fim do próximo turno dele (sem soltar o que segura).' },
      { name: 'Lu', requirement: 'WILL 8 ou mais. Alguém em alcance acabou de passar numa Resolução de Special Move.', effect: 'Resolução contra o resultado dele: se vencer, o Special Move falha, a menos que ele responda com Lu e supere o seu resultado.' },
    ],
  },
  {
    name: 'Thamoc', source: 'Interface RED 4', page: 83,
    description: 'A "Arte do Confronto Moderno", criada para treinar polícias em retenção de arma e grappling.',
    moves: [
      { name: 'Advantaged Position', requirement: 'É seu turno e você é o atacante num Grapple.', effect: 'Uma vez por turno, DV15: no resto do turno, Aimed Shots com Martial Arts ou arma corpo a corpo contra quem você agarra têm -5 em vez de -8.' },
      { name: 'Weapon Retention', requirement: 'Você está para ser desarmado, de qualquer forma.', effect: 'DV15: a arma continua nas suas mãos.' },
    ],
  },
  {
    name: 'Thrash Sambo', source: 'Interface RED 4', page: 83,
    description: 'Fusão recente de Command Sambo com Thrash Boxing, estilo nascido nas boates do Leste Europeu nos anos 2020.',
    moves: [
      { name: 'Grit', requirement: 'Você está para sofrer uma Lesão Crítica.', effect: 'DV15: não sofre o dano extra da Lesão Crítica (o efeito continua).' },
      { name: 'Thick Skin', requirement: 'Você está levando dano de um ataque corpo a corpo.', effect: 'DV15: o dano cai 2 (ou 4, se for ataque de Martial Arts), depois da armadura.' },
    ],
  },
  {
    name: 'Wrestling', source: 'Interface RED 4', page: 83,
    description: 'Uma das artes marciais mais antigas: agarrar e imobilizar o oponente.',
    moves: [
      { name: 'Chokehold', requirement: 'É seu turno, você é o atacante num Grapple e ainda não usou Choke neste turno.', effect: 'Uma vez por turno, DV15: usa Choke sem gastar Ação (não de novo no mesmo alvo neste turno).' },
      { name: 'Reversal', requirement: 'É seu turno e você é o defensor num Grapple em que nenhum dos dois está em outro Grapple.', effect: 'Uma vez por turno, DV15: o Grapple acaba e você começa outro, agora como atacante.' },
    ],
  },

  // ---------------------------------------------------------------- Interface RED 5
  {
    name: 'Cyberbear', source: 'Interface RED 5', page: 78, innate: true,
    description: 'O jeito de lutar inato dos Cyberbears, efeito colateral da criação deles. Impossível de aprender ou imitar; funciona como uma arte marcial.',
    moves: [
      { name: 'Recovery', requirement: 'O urso está caído (Prone).', effect: 'Ao usar Get Up, DV13: levantar não gasta a Ação.' },
      { name: 'Cyberbear Three-Arm Strike', requirement: 'Acertou o mesmo alvo com dois ataques de Bear Claw neste turno.', effect: 'Uma vez por turno, DV15, escolhe um: agarrar ou soltar o último alvo; atacá-lo de novo com Bear Claw ou Combat Jaw; causar uma Lesão Crítica de corpo aleatória; ou reduzir em 4 o SP de toda a armadura dele. Se falhar, o tumor no cérebro causa 5 de dano direto no HP do urso.' },
    ],
  },
];
