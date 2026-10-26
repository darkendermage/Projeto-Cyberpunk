
import { t } from '../../i18n';// Martial Arts Forms and Special Moves, summarized from the official books (page numbers are the printed ones).

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
    requirement: t('Nenhum. Todos os estilos podem usar.', 'None. Every form can use it.'),
    effect: t('Ao usar a Ação Get Up, tenta vencer DV13 com a Resolução de Special Move. Se passar, levantar não gasta a Ação.', 'When using the Get Up Action, try to beat DV13 with the Special Move Resolution. On a success, getting up doesn\'t take your Action.'),
  },
];

export const MARTIAL_ARTS: MartialArtForm[] = [
  // ---------------------------------------------------------------- Core Rulebook
  {
    name: 'Aikido', source: 'Core Rulebook', page: 179,
    description: t('Estilo suave de varreduras e travas para desarmar o oponente e voltar a força dele contra ele mesmo.', 'Soft form of sweeps and locks to disarm the opponent and turn their force against them.'),
    moves: [
      { name: 'Disarming Combination', requirement: t('Acertou o mesmo alvo com um ataque de Brawling e um de Martial Arts neste turno.', 'You hit the same target with a Brawling attack and a Martial Arts attack this turn.'), effect: t('Uma vez por turno, DV15: um objeto nas mãos desse alvo passa para as suas ou cai no chão.', 'Once per turn, DV15: an object in that target\'s hands goes into yours or falls to the ground.') },
      { name: 'Iron Grip', requirement: t('Tem um alvo agarrado (Grab) que ainda não está sob Iron Grip.', 'You have a grabbed target (Grab) not yet under Iron Grip.'), effect: t('Uma vez por turno, como Ação, DV15: o alvo tem -2 a mais para escapar do Grapple e não faz ataques à distância até o Grapple acabar.', 'Once per turn, as an Action, DV15: the target has an extra -2 to escape the Grapple and can\'t make ranged attacks until the Grapple ends.') },
    ],
  },
  {
    name: 'Judo', source: 'Core Rulebook', page: 179,
    description: t('Estilo suave de agarrões, arremessos e escapadas.', 'Soft form of grabs, throws and escapes.'),
    moves: [
      { name: 'Counter Throw', requirement: t('Esquivou de todos os ataques corpo a corpo feitos contra você desde seu último turno.', 'You dodged every melee attack made against you since your last turn.'), effect: t('Uma vez por turno, como Ação, DV15: usa Throw num alvo em alcance cujo ataque você esquivou, sem precisar agarrá-lo. O arremesso não pode ser evitado.', 'Once per turn, as an Action, DV15: use Throw on a target in range whose attack you dodged, without having to grab them. The throw can\'t be avoided.') },
      { name: 'Grab Escape', requirement: t('Acertou com 2 ataques corpo a corpo quem está te agarrando neste turno.', 'You hit whoever is grabbing you with 2 melee attacks this turn.'), effect: t('Uma vez por turno, DV15: você sai do Grapple e o alvo sofre Broken Arm (você escolhe o braço), se ainda não tiver.', 'Once per turn, DV15: you escape the Grapple and the target suffers a Broken Arm (you pick the arm), if they don\'t have one yet.') },
    ],
  },
  {
    name: 'Karate', source: 'Core Rulebook', page: 179,
    description: t('Estilo duro de golpes feitos para quebrar ossos e armaduras.', 'Hard form of strikes made to break bones and armor.'),
    moves: [
      { name: 'Armor Breaking Combination', requirement: t('Acertou o mesmo alvo com uma arma corpo a corpo e um ataque de Martial Arts neste turno.', 'You hit the same target with a melee weapon and a Martial Arts attack this turn.'), effect: t('Uma vez por turno, DV15: toda armadura usada pelo alvo é ablatada em 2 pontos a mais.', 'Once per turn, DV15: all armor worn by the target is ablated by 2 more points.') },
      { name: 'Bone Breaking Strike', requirement: t('WILL 8 ou mais.', 'WILL 8 or higher.'), effect: t('No lugar dos 2 ataques de Martial Arts, um golpe com a Resolução contra um alvo em alcance: se acertar, além do dano ele sofre Broken Ribs. Com -8, mira a cabeça como num Aimed Shot e causa Cracked Skull.', 'Instead of the 2 Martial Arts attacks, one strike with the Resolution against a target in range: on a hit, besides the damage they suffer Broken Ribs. At -8, aim at the head like an Aimed Shot and cause a Cracked Skull.') },
    ],
  },
  {
    name: 'Taekwondo', source: 'Core Rulebook', page: 180,
    description: t('Estilo duro de chutes altos e golpes precisos em pontos de pressão.', 'Hard form of high kicks and precise strikes to pressure points.'),
    moves: [
      { name: 'Pressure Point Strike', requirement: t('WILL 8 ou mais.', 'WILL 8 or higher.'), effect: t('No lugar dos 2 ataques, um golpe com a Resolução: se acertar, além do dano o alvo sofre Spinal Injury. Com -8, mira a cabeça como num Aimed Shot e causa Brain Injury.', 'Instead of the 2 attacks, one strike with the Resolution: on a hit, besides the damage the target suffers a Spinal Injury. At -8, aim at the head like an Aimed Shot and cause a Brain Injury.') },
      { name: 'Flying Kick', requirement: t('MOVE 8 ou mais e ter andado ao menos 4 m/yds neste turno.', 'MOVE 8 or higher and having moved at least 4 m/yds this turn.'), effect: t('No lugar dos 2 ataques, gasta a Ação e o resto do movimento voando em linha reta até um alvo a até 4 m/yds. Se acertar, causa o dano de Martial Arts no corpo, derruba o alvo (Prone) e o tira de moto ou veículo sem cabine fechada.', 'Instead of the 2 attacks, spend your Action and the rest of your movement flying in a straight line at a target up to 4 m/yds away. On a hit, deal Martial Arts damage to the body, knock the target Prone and off a bike or a vehicle without a closed cabin.') },
    ],
  },

  // ---------------------------------------------------------------- Interface RED 4 (Cyberfists of Fury)
  {
    name: 'Arasaka-te', source: 'Interface RED 4', page: 76,
    description: t('Criada pela Arasaka antes da 4ª Guerra Corporativa. Fácil de ensinar, virou franquia de dojôs em centros comerciais.', 'Created by Arasaka before the 4th Corporate War. Easy to teach, it became a franchise of dojos in shopping malls.'),
    moves: [
      { name: 'Counter Strike', requirement: t('É seu turno e você levou dano de Brawling, Martial Arts ou arma corpo a corpo desde seu último turno.', 'It\'s your turn and you took Brawling, Martial Arts or melee weapon damage since your last turn.'), effect: t('Uma vez por turno, DV15: no resto do turno, Aimed Shots com Martial Arts ou arma corpo a corpo contra quem te causou esse dano têm -5 em vez de -8.', 'Once per turn, DV15: for the rest of the turn, Aimed Shots with Martial Arts or a melee weapon against whoever dealt you that damage are at -5 instead of -8.') },
      { name: 'Escape Hold', requirement: t('É seu turno e você é o defensor num Grapple.', 'It\'s your turn and you\'re the defender in a Grapple.'), effect: t('Uma vez por turno, DV15: você sai do Grapple.', 'Once per turn, DV15: you escape the Grapple.') },
    ],
  },
  {
    name: 'Arnis', source: 'Interface RED 4', page: 76,
    description: t('Arte filipina centrada em armas, tida como uma das mais letais do mundo.', 'Weapon-focused Filipino art, considered one of the deadliest in the world.'),
    moves: [
      { name: 'Coordinated Combination', requirement: t('Acertou o mesmo alvo com dois ataques de Martial Arts (Arnis) ou dois ataques com armas corpo a corpo Leves ou Médias (ou armas em cyberware ou Battleglove) neste turno.', 'You hit the same target with two Martial Arts (Arnis) attacks or two attacks with Light or Medium melee weapons (or cyberware or Battleglove weapons) this turn.'), effect: t('Uma vez por turno, DV15: o alvo sofre Torn Muscle, sem o dano extra.', 'Once per turn, DV15: the target suffers a Torn Muscle, without the bonus damage.') },
      { name: 'Disarming Technique', requirement: t('É seu turno, você esquivou de ao menos um ataque com arma corpo a corpo e de todos eles desde seu último turno, e está empunhando uma arma corpo a corpo.', 'It\'s your turn, you dodged at least one melee weapon attack and all of them since your last turn, and you\'re wielding a melee weapon.'), effect: t('Uma vez por turno, DV15: uma arma corpo a corpo de um alvo em alcance que te atacou com ela passa para as suas mãos ou cai no chão.', 'Once per turn, DV15: a melee weapon of a target in range that attacked you with it goes into your hands or falls to the ground.') },
    ],
  },
  {
    name: 'Boxing', source: 'Interface RED 4', page: 76,
    description: t('Socos e defesas. Simples e versátil: desgasta o oponente ou acaba com ele numa saraivada de golpes.', 'Punches and blocks. Simple and versatile: wear the opponent down or finish them in a flurry of blows.'),
    moves: [
      { name: 'Knockout Punch', requirement: t('BODY 8 ou mais.', 'BODY 8 or higher.'), effect: t('No lugar dos 2 ataques, um soco com a Resolução a -5: se acertar, vale como Aimed Shot na cabeça e o alvo sofre Broken Jaw além do dano.', 'Instead of the 2 attacks, one punch with the Resolution at -5: on a hit, it counts as an Aimed Shot to the head and the target suffers a Broken Jaw besides the damage.') },
      { name: 'Punch Combination', requirement: t('Acertou o mesmo alvo com dois ataques de Brawling neste turno.', 'You hit the same target with two Brawling attacks this turn.'), effect: t('Uma vez por turno, DV15: mais um ataque de Brawling contra esse alvo.', 'Once per turn, DV15: one more Brawling attack against that target.') },
    ],
  },
  {
    name: 'Capoeira', source: 'Interface RED 4', page: 77,
    description: t('Possivelmente criada por pessoas escravizadas no Brasil. Movimentos fluidos como uma dança.', 'Possibly created by enslaved people in Brazil. Fluid, dance-like movements.'),
    moves: [
      { name: 'Rhythmic Recovery', requirement: t('Errou um ataque de Martial Arts (Capoeira) neste turno.', 'You missed a Martial Arts (Capoeira) attack this turn.'), effect: t('Uma vez por turno, DV15: um ataque de Brawling contra um alvo em alcance.', 'Once per turn, DV15: one Brawling attack against a target in range.') },
      { name: 'Slash Dance', requirement: t('Neste turno, causou Lesão Crítica com arma corpo a corpo Leve ou Média (ou em cyberware ou Battleglove), ou gastou mais de 3 de Luck num ataque com elas ou no Rhythmic Recovery.', 'This turn, you dealt a Critical Injury with a Light or Medium melee weapon (or cyberware or Battleglove weapon), or spent more than 3 Luck on an attack with them or on Rhythmic Recovery.'), effect: t('Uma vez por turno, DV15: um ataque de Martial Arts (Capoeira) contra um alvo em alcance.', 'Once per turn, DV15: one Martial Arts (Capoeira) attack against a target in range.') },
    ],
  },
  {
    name: 'Choy Li Fut', source: 'Interface RED 4', page: 77,
    description: t('Arte chinesa rápida e versátil, com golpes amplos que favorecem a luta contra vários oponentes.', 'Fast, versatile Chinese art with wide strikes that favor fighting several opponents.'),
    moves: [
      { name: 'Shaolin Step', requirement: t('MOVE 6 ou mais. É seu turno, você esquivou de ao menos um ataque e de todos desde seu último turno, e ainda não usou Run.', 'MOVE 6 or higher. It\'s your turn, you dodged at least one attack and every attack since your last turn, and you haven\'t used Run yet.'), effect: t('Uma vez por turno, DV15: usa a Ação Run sem gastar Ação (só uma vez no turno).', 'Once per turn, DV15: use the Run Action without spending an Action (only once per turn).') },
      { name: 'Sweeping Fist', requirement: t('MOVE 6 ou mais. Acertou o mesmo alvo com dois ataques de Martial Arts (Choy Li Fut) neste turno.', 'MOVE 6 or higher. You hit the same target with two Martial Arts (Choy Li Fut) attacks this turn.'), effect: t('Uma vez por turno, DV15: um ataque de Brawling contra outro alvo em alcance que você não acertou neste turno.', 'Once per turn, DV15: one Brawling attack against another target in range you haven\'t hit this turn.') },
    ],
  },
  {
    name: 'Drunken Fist', source: 'Interface RED 4', page: 77,
    description: t('Arte chinesa que imita um bêbado para atacar e se defender de forma inesperada.', 'Chinese art that imitates a drunk to attack and defend in unexpected ways.'),
    moves: [
      { name: 'Environmental Improvisation', requirement: t('LUCK 4 ou mais (o atributo, não o saldo atual).', 'LUCK 4 or higher (the STAT, not your current pool).'), effect: t('Uma vez por turno, DV13: escolhe um objeto ou cobertura em alcance. Se o Mestre concordar que doeria, seus ataques de Brawling perto dele ignoram metade da armadura neste turno (e o Mestre pode subir o dano até 4d6).', 'Once per turn, DV13: pick an object or cover in range. If the GM agrees it would hurt, your Brawling attacks near it ignore half armor this turn (and the GM can raise the damage up to 4d6).') },
      { name: 'Lucky Stumble', requirement: t('LUCK 4 ou mais. Você está sendo atacado sem perceber.', 'LUCK 4 or higher. You\'re being attacked without noticing.'), effect: t('DV15, mesmo inconsciente: pode esquivar normalmente, como se esquivasse de balas. Na primeira vez que der certo na sessão, recupera 2 de Luck.', 'DV15, even unconscious: you can dodge normally, as if dodging bullets. The first time it works in a session, regain 2 Luck.') },
    ],
  },
  {
    name: 'Gun Fu', source: 'Interface RED 4', page: 78,
    description: t('Metade luta corpo a corpo, metade tiro de pistola, tratando a arma como extensão do corpo. Letal em ambientes fechados.', 'Half melee, half pistol shooting, treating the gun as an extension of the body. Deadly in close quarters.'),
    moves: [
      { name: 'Combat Reload', requirement: t('Acertou um ataque de Martial Arts (Gun Fu) e ainda não recarregou neste turno.', 'You hit with a Martial Arts (Gun Fu) attack and haven\'t reloaded yet this turn.'), effect: t('Uma vez por turno, DV15: recarrega a arma empunhada sem gastar Ação.', 'Once per turn, DV15: reload the wielded weapon without spending an Action.') },
      { name: 'Woo Technique', requirement: t('Handgun 4 ou mais, ou Combat Awareness 1. É seu turno e você empunha uma arma de Handgun de uma mão, ROF 2, com ao menos 1d6 de dano.', 'Handgun 4 or higher, or Combat Awareness 1. It\'s your turn and you\'re wielding a one-handed Handgun weapon, ROF 2, with at least 1d6 damage.'), effect: t('Uma vez por turno, DV15: até o fim do turno, ataques de Martial Arts (Gun Fu) alcançam 25 m/yds (contam como corpo a corpo, ignoram metade da armadura e qualquer um pode esquivar), usam o dano da arma se for maior e os efeitos da munição gasta. O nível de Gun Fu cai ao de Handgun, se for maior. Sem munição, o ataque falha.', 'Once per turn, DV15: until the end of the turn, Martial Arts (Gun Fu) attacks reach 25 m/yds (they count as melee, ignore half armor and anyone can dodge), use the weapon\'s damage if higher and the effects of the ammo spent. Your Gun Fu level drops to your Handgun level, if higher. With no ammo, the attack fails.') },
    ],
  },
  {
    name: 'Jujutsu', source: 'Interface RED 4', page: 78,
    description: t('Arte japonesa que cede à força do oponente e a redireciona contra ele.', 'Japanese art that yields to the opponent\'s force and turns it against them.'),
    moves: [
      { name: 'Aiki', requirement: t('Acertou um ataque de Martial Arts que não seja Gun Fu e vai causar dano.', 'You hit with a Martial Arts attack other than Gun Fu and are about to deal damage.'), effect: t('Uma vez por turno, DV15: no resto do turno, usa o BODY do alvo em vez do seu para o dano de Brawling e Martial Arts contra ele.', 'Once per turn, DV15: for the rest of the turn, use the target\'s BODY instead of yours for Brawling and Martial Arts damage against them.') },
      { name: 'Throwing Technique', requirement: t('WILL 6 ou mais. Acertou o mesmo alvo com dois ataques de Martial Arts (Jujutsu) neste turno.', 'WILL 6 or higher. You hit the same target with two Martial Arts (Jujutsu) attacks this turn.'), effect: t('Uma vez por turno, DV15: usa Throw nesse alvo mesmo sem agarrá-lo, podendo usar o BODY dele para o dano do arremesso.', 'Once per turn, DV15: use Throw on that target even without grabbing them, and you can use their BODY for the throw\'s damage.') },
    ],
  },
  {
    name: 'Kendo', source: 'Interface RED 4', page: 79,
    description: t('O "Caminho da Espada" japonês moderno, com linhagem que remonta aos samurais.', 'The modern Japanese "Way of the Sword", with a lineage going back to the samurai.'),
    moves: [
      { name: 'Cut the Bullet', requirement: t('WILL 8 ou mais, Melee Weapon 6 ou mais e ciente do atacante. Empunhando arma corpo a corpo, você é alvo de um tiro único, de uma arma arremessada ou está na área de um explosivo.', 'WILL 8 or higher, Melee Weapon 6 or higher and aware of the attacker. Wielding a melee weapon, you\'re the target of a single shot, a thrown weapon or you\'re in an explosive\'s area.'), effect: t('Resolução contra o ataque: se vencer, o ataque é anulado; se perder, acerta (e o explosivo passa a ter você no centro). Pode sacar e guardar a arma sem Ação. Não conta como esquiva e não vale contra ataques não físicos.', 'Resolution against the attack: on a win, the attack is negated; on a loss, it hits (and the explosive is now centered on you). You can draw and stow the weapon without an Action. It doesn\'t count as a dodge and doesn\'t work against non-physical attacks.') },
      { name: 'Ki Ken Tai no Ichi', requirement: t('Você está em combate.', 'You\'re in combat.'), effect: t('Uma vez por turno, como Ação, DV15: no próximo turno, o primeiro Aimed Shot com arma corpo a corpo tem -2 em vez de -8.', 'Once per turn, as an Action, DV15: next turn, your first Aimed Shot with a melee weapon is at -2 instead of -8.') },
    ],
  },
  {
    name: 'Krav Maga', source: 'Interface RED 4', page: 79,
    description: t('Arte israelense que adapta Aikido, Boxe, Judô, Karatê e luta livre para a defesa pessoal real.', 'Israeli art that adapts Aikido, Boxing, Judo, Karate and wrestling for real self-defense.'),
    moves: [
      { name: 'Contact Combat', requirement: t('Neste turno, causou Lesão Crítica com ataque ou Special Move de Aikido, Boxing, Judo, Karate, Krav Maga ou Wrestling.', 'This turn, you dealt a Critical Injury with an Aikido, Boxing, Judo, Karate, Krav Maga or Wrestling attack or Special Move.'), effect: t('Uma vez por turno, DV15: um ataque de Brawling contra quem sofreu a Lesão Crítica.', 'Once per turn, DV15: one Brawling attack against whoever suffered the Critical Injury.') },
      { name: 'Punishing Blow', requirement: t('Esquivou de ao menos um ataque corpo a corpo e de todos os ataques desde seu último turno.', 'You dodged at least one melee attack and every attack since your last turn.'), effect: t('Uma vez por turno, DV15: um ataque de Brawling contra quem te atacou corpo a corpo.', 'Once per turn, DV15: one Brawling attack against whoever attacked you in melee.') },
    ],
  },
  {
    name: 'Kung Fu', source: 'Interface RED 4', page: 79,
    description: t('A arte marcial chinesa original, ancestral de muitos outros estilos.', 'The original Chinese martial art, ancestor of many other forms.'),
    moves: [
      { name: 'Five Forms', requirement: t('Kung Fu 4 ou mais.', 'Kung Fu 4 or higher.'), effect: t('No lugar dos 2 ataques, escolhe dois animais e faz a Resolução contra um alvo: se acertar, o primeiro vale agora e o segundo no próximo ataque corpo a corpo que acertar no combate. Grou: Damaged Eye sem dano extra. Dragão: +1 no próximo ataque contra o alvo. Leopardo: +2 MOVE até o fim do turno. Serpente: recupera 1 de Luck. Tigre: a armadura do alvo ablata 1 a mais.', 'Instead of the 2 attacks, pick two animals and make the Resolution against a target: on a hit, the first applies now and the second on your next melee hit in the fight. Crane: Damaged Eye without bonus damage. Dragon: +1 to your next attack against the target. Leopard: +2 MOVE until the end of the turn. Snake: regain 1 Luck. Tiger: the target\'s armor ablates 1 more.') },
      { name: 'Superior Stance', requirement: t('É seu turno e você acertou um ataque de Martial Arts.', 'It\'s your turn and you hit with a Martial Arts attack.'), effect: t('Uma vez por turno, DV17: você vai para o topo da fila de Iniciativa.', 'Once per turn, DV17: you go to the top of the Initiative Queue.') },
    ],
  },
  {
    name: 'Kyudo', source: 'Interface RED 4', page: 80,
    description: t('A arquearia japonesa, esporte e meditação. Hoje vem com golpes corpo a corpo que complementam o arco.', 'Japanese archery, both sport and meditation. Today it comes with melee strikes that complement the bow.'),
    moves: [
      { name: 'Hassetsu', requirement: t('Você não está Prone nem agarrado e empunha uma arma de Archery (pode sacá-la como parte do requisito).', 'You\'re not Prone or grabbed and you\'re wielding an Archery weapon (you can draw it as part of the requirement).'), effect: t('Uma vez por turno, como Ação, DV15: entra na Kyudo Stance, e o primeiro ataque ROF 1 de Archery de cada turno causa +2d6. A postura acaba ao andar mais de 6 m/yds, levar dano corpo a corpo, ser agarrado, cair, perder o último arco ou o combate terminar. Assumir a postura equivale a sacar uma arma.', 'Once per turn, as an Action, DV15: enter the Kyudo Stance, and the first ROF 1 Archery attack each turn deals +2d6. The stance ends when you move more than 6 m/yds, take melee damage, get grabbed, fall, lose your last bow or the fight ends. Taking the stance counts as drawing a weapon.') },
      { name: 'Zaiteki', requirement: t('Acertou um Aimed Shot com arma de Archery neste turno.', 'You landed an Aimed Shot with an Archery weapon this turn.'), effect: t('Uma vez por turno, DV15: recupera até 2 de Luck.', 'Once per turn, DV15: regain up to 2 Luck.') },
    ],
  },
  {
    name: 'Militech Commando Training', source: 'Interface RED 4', page: 80,
    description: t('Sistema criado pela Militech para ensinar soldados a usar facas de combate.', 'System created by Militech to teach soldiers to use combat knives.'),
    moves: [
      { name: 'Combat Knife Training', requirement: t('O primeiro ataque do turno é com arma corpo a corpo Leve ou Média.', 'The turn\'s first attack is with a Light or Medium melee weapon.'), effect: t('DV15: até o fim do turno, a arma causa 4d6, mas fica com ROF 1.', 'DV15: until the end of the turn, the weapon deals 4d6 but has ROF 1.') },
      { name: 'Commando Disarm', requirement: t('Você tem duas mãos livres.', 'You have two free hands.'), effect: t('Como Ação, Resolução contra o Brawling de um alvo em alcance que empunha uma arma: se vencer, pega uma das armas dele e pode atacá-lo com ela como se ainda não tivesse usado a Ação.', 'As an Action, Resolution against the Brawling of a target in range wielding a weapon: on a win, take one of their weapons and you can attack them with it as if you hadn\'t used your Action yet.') },
    ],
  },
  {
    name: 'Muay Thai', source: 'Interface RED 4', page: 80,
    description: t('Arte tailandesa de golpes, com condicionamento físico extremo (e, muitas vezes, cyberware igualmente extremo).', 'Thai striking art, with extreme physical conditioning (and, often, equally extreme cyberware).'),
    moves: [
      { name: 'Conditioned Ferocity', requirement: t('É seu turno e você causou dano com ataque de Martial Arts desde o início do seu último turno.', 'It\'s your turn and you dealt damage with a Martial Arts attack since the start of your last turn.'), effect: t('Uma vez por turno, DV15: até o fim do turno, Brawling causa +1d6 (até 4d6) e tem +1.', 'Once per turn, DV15: until the end of the turn, Brawling deals +1d6 (up to 4d6) and gets +1.') },
      { name: 'Conditioned Power', requirement: t('É seu turno e você causou dano com Brawling desde o início do seu último turno.', 'It\'s your turn and you dealt Brawling damage since the start of your last turn.'), effect: t('Uma vez por turno, DV15: o próximo ataque de Martial Arts no turno causa +1d6.', 'Once per turn, DV15: your next Martial Arts attack this turn deals +1d6.') },
    ],
  },
  {
    name: 'Multiarm Melee', source: 'Interface RED 4', page: 81,
    description: t('Criada no vale do Yukon, no Estado Livre do Alasca, para enfrentar os ursos aumentados da Biotechnica e lutar contra a Petrochem.', 'Created in the Yukon valley, in the Free State of Alaska, to face Biotechnica\'s augmented bears and fight Petrochem.'),
    moves: [
      { name: 'Armed & Dangerous', requirement: t('É seu turno e você tem mais braços que o alvo de um ataque de Brawling.', 'It\'s your turn and you have more arms than the target of a Brawling attack.'), effect: t('Antes de rolar, DV15 (DV17 se já usou no turno): rola o d10 duas vezes e escolhe; ou faz Aimed Shot de Brawling a -5 em vez de -8; ou, se for Solo, redistribui Combat Awareness sem Ação.', 'Before rolling, DV15 (DV17 if already used this turn): roll the d10 twice and pick one; or make a Brawling Aimed Shot at -5 instead of -8; or, if you\'re a Solo, reassign Combat Awareness without an Action.') },
      { name: 'Smack Together', requirement: t('BODY 6 ou mais. Você é o atacante em dois ou mais Grapples.', 'BODY 6 or higher. You\'re the attacker in two or more Grapples.'), effect: t('Uma vez por turno, como Ação, DV15: causa seu BODY direto no HP de todos que você está agarrando.', 'Once per turn, as an Action, DV15: deal your BODY straight to the HP of everyone you\'re grabbing.') },
    ],
  },
  {
    name: 'PanzerFaust', source: 'Interface RED 4', page: 81,
    description: t('Criada por FBCs para FBCs: soltar toda a força de um corpo de metal contra um de carne.', 'Made by FBCs for FBCs: unleashing all the strength of a metal body against one of flesh.'),
    moves: [
      { name: 'Borg Fist', requirement: t('BODY 10 ou mais.', 'BODY 10 or higher.'), effect: t('No lugar dos 2 ataques, Resolução contra um alvo em alcance com BODY menor que o seu: se vencer, um ataque de Martial Arts (PanzerFaust) que causa 5d6 (6d6 se sua Humanidade estiver abaixo de 0).', 'Instead of the 2 attacks, Resolution against a target in range with a lower BODY than yours: on a win, a Martial Arts (PanzerFaust) attack dealing 5d6 (6d6 if your Humanity is below 0).') },
      { name: 'Inner Chrome', requirement: t('Você é FBC ou tem Humanidade abaixo de 0, está em combate, e algum cyberware seu sofreu Lesão Crítica (exceto perda) ou foi danificado, desativado ou inutilizado (EMP, Quickhack...).', 'You\'re an FBC or have Humanity below 0, you\'re in combat, and some of your cyberware suffered a Critical Injury (other than loss) or was damaged, disabled or made useless (EMP, Quickhack...).'), effect: t('Uma vez por turno, DV11 +2 por cyberware afetado: até o fim do combate, todo o cyberware volta a funcionar e as Lesões que aceitam Quick Fix contam como tratadas (sem restaurar SP). Até seu próximo turno, o cyberware fica imune a esses efeitos; com Humanidade abaixo de 0 e DV 15+, até o fim do combate e também contra destruição.', 'Once per turn, DV11 +2 per affected cyberware: until the end of the fight, all your cyberware works again and Injuries that take a Quick Fix count as treated (without restoring SP). Until your next turn, the cyberware is immune to those effects; with Humanity below 0 and DV 15+, until the end of the fight and against destruction too.') },
    ],
  },
  {
    name: 'Silat', source: 'Interface RED 4', page: 81,
    description: t('Fusão de artes indígenas do Sudeste Asiático, com centenas de estilos diferentes.', 'A fusion of indigenous Southeast Asian arts, with hundreds of different styles.'),
    moves: [
      { name: 'Internal Power', requirement: t('WILL 6 ou mais. Acertou o mesmo alvo com dois ataques de Martial Arts (Silat) ou com armas corpo a corpo Leves ou Médias.', 'WILL 6 or higher. You hit the same target with two Martial Arts (Silat) attacks or with Light or Medium melee weapons.'), effect: t('Uma vez por turno, DV15: um ataque de Brawling contra um alvo em alcance.', 'Once per turn, DV15: one Brawling attack against a target in range.') },
      { name: 'Violent Leverage', requirement: t('É seu turno e, neste turno, você iniciou um Grapple com alguém que ainda não tinha agarrado no combate.', 'It\'s your turn and, this turn, you started a Grapple with someone you hadn\'t grabbed yet in this fight.'), effect: t('Uma vez por turno, DV15: um ataque com arma corpo a corpo contra um alvo em alcance.', 'Once per turn, DV15: one melee weapon attack against a target in range.') },
    ],
  },
  {
    name: 'Sov-System', source: 'Interface RED 4', page: 82,
    description: t('Sucessora do Command Sambo do Exército Vermelho, moldada pela SovOil para ser mais popular que a Arasaka-te.', 'Successor to the Red Army\'s Command Sambo, shaped by SovOil to be more popular than Arasaka-te.'),
    moves: [
      { name: 'Dirty Blow', requirement: t('BODY 4 ou mais.', 'BODY 4 or higher.'), effect: t('No lugar dos 2 ataques, um golpe com a Resolução: se acertar, além do dano o alvo sofre uma Lesão Crítica de corpo aleatória (Foreign Object é rolada de novo).', 'Instead of the 2 attacks, one strike with the Resolution: on a hit, besides the damage the target suffers a random body Critical Injury (Foreign Object is rerolled).') },
      { name: 'Revenge Strike', requirement: t('BODY 4 ou mais. É seu turno e você sofreu uma Lesão Crítica de ataque corpo a corpo ou Special Move desde seu último turno.', 'BODY 4 or higher. It\'s your turn and you\'ve suffered a Critical Injury from a melee attack or Special Move since your last turn.'), effect: t('Uma vez por turno, DV15: um ataque de Martial Arts (Sov-System) contra quem a causou. Se causar Lesão Crítica, recupera até 2 de Luck.', 'Once per turn, DV15: one Martial Arts (Sov-System) attack against whoever caused it. If it causes a Critical Injury, regain up to 2 Luck.') },
    ],
  },
  {
    name: 'Sumo', source: 'Interface RED 4', page: 82,
    description: t('Luta japonesa focada em mover o oponente com todo o peso do corpo.', 'Japanese wrestling focused on moving the opponent with your whole body weight.'),
    moves: [
      { name: 'Deashi', requirement: t('BODY 7 ou mais. Você está para ser agarrado ou arremessado (até pelo Counter Throw do Judô).', 'BODY 7 or higher. You\'re about to be grabbed or thrown (even by Judo\'s Counter Throw).'), effect: t('DV15: não é agarrado nem arremessado e move você e o oponente até 6 m/yds na direção que escolher.', 'DV15: you aren\'t grabbed or thrown, and you move yourself and the opponent up to 6 m/yds in a direction of your choice.') },
      { name: 'Niramiai', requirement: t('COOL 4 ou mais. Você está num Facedown.', 'COOL 4 or higher. You\'re in a Facedown.'), effect: t('DV15: +2 no Facedown.', 'DV15: +2 to the Facedown.') },
    ],
  },
  {
    name: 'Tai Chi', source: 'Interface RED 4', page: 82,
    description: t('Arte chinesa que desvia ou anula ataques mantendo o equilíbrio interior.', 'Chinese art that deflects or negates attacks while keeping inner balance.'),
    moves: [
      { name: 'Joint Manipulation', requirement: t('Alguém em alcance errou um ataque corpo a corpo em você.', 'Someone in range missed a melee attack against you.'), effect: t('DV15: escolhe uma mão do atacante, que fica inutilizada até o fim do próximo turno dele (sem soltar o que segura).', 'DV15: pick one of the attacker\'s hands, which is useless until the end of their next turn (without dropping what it holds).') },
      { name: 'Lu', requirement: t('WILL 8 ou mais. Alguém em alcance acabou de passar numa Resolução de Special Move.', 'WILL 8 or higher. Someone in range just passed a Special Move Resolution.'), effect: t('Resolução contra o resultado dele: se vencer, o Special Move falha, a menos que ele responda com Lu e supere o seu resultado.', 'Resolution against their result: on a win, the Special Move fails, unless they answer with Lu and beat your result.') },
    ],
  },
  {
    name: 'Thamoc', source: 'Interface RED 4', page: 83,
    description: t('A "Arte do Confronto Moderno", criada para treinar polícias em retenção de arma e grappling.', 'The "Art of Modern Confrontation", created to train police in weapon retention and grappling.'),
    moves: [
      { name: 'Advantaged Position', requirement: t('É seu turno e você é o atacante num Grapple.', 'It\'s your turn and you\'re the attacker in a Grapple.'), effect: t('Uma vez por turno, DV15: no resto do turno, Aimed Shots com Martial Arts ou arma corpo a corpo contra quem você agarra têm -5 em vez de -8.', 'Once per turn, DV15: for the rest of the turn, Aimed Shots with Martial Arts or a melee weapon against whoever you\'re grabbing are at -5 instead of -8.') },
      { name: 'Weapon Retention', requirement: t('Você está para ser desarmado, de qualquer forma.', 'You\'re about to be disarmed, by any means.'), effect: t('DV15: a arma continua nas suas mãos.', 'DV15: the weapon stays in your hands.') },
    ],
  },
  {
    name: 'Thrash Sambo', source: 'Interface RED 4', page: 83,
    description: t('Fusão recente de Command Sambo com Thrash Boxing, estilo nascido nas boates do Leste Europeu nos anos 2020.', 'A recent fusion of Command Sambo with Thrash Boxing, a style born in Eastern European clubs in the 2020s.'),
    moves: [
      { name: 'Grit', requirement: t('Você está para sofrer uma Lesão Crítica.', 'You\'re about to suffer a Critical Injury.'), effect: t('DV15: não sofre o dano extra da Lesão Crítica (o efeito continua).', 'DV15: you don\'t take the Critical Injury\'s bonus damage (the effect still applies).') },
      { name: 'Thick Skin', requirement: t('Você está levando dano de um ataque corpo a corpo.', 'You\'re taking damage from a melee attack.'), effect: t('DV15: o dano cai 2 (ou 4, se for ataque de Martial Arts), depois da armadura.', 'DV15: the damage drops by 2 (or 4, if it\'s a Martial Arts attack), after armor.') },
    ],
  },
  {
    name: 'Wrestling', source: 'Interface RED 4', page: 83,
    description: t('Uma das artes marciais mais antigas: agarrar e imobilizar o oponente.', 'One of the oldest martial arts: grab and pin the opponent.'),
    moves: [
      { name: 'Chokehold', requirement: t('É seu turno, você é o atacante num Grapple e ainda não usou Choke neste turno.', 'It\'s your turn, you\'re the attacker in a Grapple and you haven\'t used Choke yet this turn.'), effect: t('Uma vez por turno, DV15: usa Choke sem gastar Ação (não de novo no mesmo alvo neste turno).', 'Once per turn, DV15: use Choke without spending an Action (not again on the same target this turn).') },
      { name: 'Reversal', requirement: t('É seu turno e você é o defensor num Grapple em que nenhum dos dois está em outro Grapple.', 'It\'s your turn and you\'re the defender in a Grapple where neither of you is in another Grapple.'), effect: t('Uma vez por turno, DV15: o Grapple acaba e você começa outro, agora como atacante.', 'Once per turn, DV15: the Grapple ends and you start another, now as the attacker.') },
    ],
  },

  // ---------------------------------------------------------------- Interface RED 5
  {
    name: 'Cyberbear', source: 'Interface RED 5', page: 78, innate: true,
    description: t('O jeito de lutar inato dos Cyberbears, efeito colateral da criação deles. Impossível de aprender ou imitar; funciona como uma arte marcial.', 'The Cyberbears\' innate way of fighting, a side effect of how they were made. Impossible to learn or imitate; it works as a martial art.'),
    moves: [
      { name: 'Recovery', requirement: t('O urso está caído (Prone).', 'The bear is down (Prone).'), effect: t('Ao usar Get Up, DV13: levantar não gasta a Ação.', 'When using Get Up, DV13: getting up doesn\'t take your Action.') },
      { name: 'Cyberbear Three-Arm Strike', requirement: t('Acertou o mesmo alvo com dois ataques de Bear Claw neste turno.', 'You hit the same target with two Bear Claw attacks this turn.'), effect: t('Uma vez por turno, DV15, escolhe um: agarrar ou soltar o último alvo; atacá-lo de novo com Bear Claw ou Combat Jaw; causar uma Lesão Crítica de corpo aleatória; ou reduzir em 4 o SP de toda a armadura dele. Se falhar, o tumor no cérebro causa 5 de dano direto no HP do urso.', 'Once per turn, DV15, pick one: grab or release the last target; attack it again with Bear Claw or Combat Jaw; cause a random body Critical Injury; or reduce the SP of all its armor by 4. On a failure, the brain tumor deals 5 damage straight to the bear\'s HP.') },
    ],
  },
];
