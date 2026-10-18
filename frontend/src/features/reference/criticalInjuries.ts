// Critical Injury tables of the Cyberpunk RED Core Rulebook (p.187 body, p.188 head). Injury names stay in
// English (as printed); effects are translated.

export interface CriticalInjury {
  roll: number;
  name: string;
  effect: string;
  quickFix: string;
  treatment: string;
  /** Raises the Base Death Save Penalty by 1. */
  deathSave: boolean;
}

export const CRITICAL_RULES = [
  'Ocorre quando dois ou mais dados de dano de um ataque corpo a corpo ou à distância saem 6.',
  'Role 2d6 na tabela adequada até sair uma lesão que o alvo ainda não tem. Sem Aimed Shot na cabeça, use a tabela do corpo.',
  'Toda Critical Injury causa 5 de dano bônus direto nos Hit Points: não desgasta armadura e não é multiplicado pelo local.',
  'A lesão e o dano bônus acontecem mesmo que o dano do ataque não passe do SP do alvo.',
];

export const BODY_INJURIES: CriticalInjury[] = [
  { roll: 2, name: 'Dismembered Arm', effect: 'O braço se foi. Você larga na hora o que estiver naquela mão.', quickFix: '—', treatment: 'Surgery DV17', deathSave: true },
  { roll: 3, name: 'Dismembered Hand', effect: 'A mão se foi. Você larga na hora o que estiver nela.', quickFix: '—', treatment: 'Surgery DV17', deathSave: true },
  { roll: 4, name: 'Collapsed Lung', effect: '-2 em MOVE (mínimo 1).', quickFix: 'Paramedic DV15', treatment: 'Surgery DV15', deathSave: true },
  { roll: 5, name: 'Broken Ribs', effect: 'No fim de todo turno em que se mover mais de 4 m/yds a pé, sofre de novo o dano bônus desta lesão direto nos HP.', quickFix: 'Paramedic DV13', treatment: 'Paramedic DV15 ou Surgery DV13', deathSave: false },
  { roll: 6, name: 'Broken Arm', effect: 'O braço não pode ser usado. Você larga na hora o que estiver naquela mão.', quickFix: 'Paramedic DV13', treatment: 'Paramedic DV15 ou Surgery DV13', deathSave: false },
  { roll: 7, name: 'Foreign Object', effect: 'No fim de todo turno em que se mover mais de 4 m/yds a pé, sofre de novo o dano bônus desta lesão direto nos HP.', quickFix: 'First Aid ou Paramedic DV13', treatment: 'O Quick Fix remove o efeito de vez', deathSave: false },
  { roll: 8, name: 'Broken Leg', effect: '-4 em MOVE (mínimo 1).', quickFix: 'Paramedic DV13', treatment: 'Paramedic DV15 ou Surgery DV13', deathSave: false },
  { roll: 9, name: 'Torn Muscle', effect: '-2 em ataques corpo a corpo.', quickFix: 'First Aid ou Paramedic DV13', treatment: 'O Quick Fix remove o efeito de vez', deathSave: false },
  { roll: 10, name: 'Spinal Injury', effect: 'No próximo turno não pode fazer uma Ação, mas ainda pode fazer uma Ação de Movimento.', quickFix: 'Paramedic DV15', treatment: 'Surgery DV15', deathSave: true },
  { roll: 11, name: 'Crushed Fingers', effect: '-4 em todas as ações com aquela mão.', quickFix: 'Paramedic DV13', treatment: 'Surgery DV15', deathSave: false },
  { roll: 12, name: 'Dismembered Leg', effect: 'A perna se foi. -6 em MOVE (mínimo 1) e não pode se esquivar de ataques.', quickFix: '—', treatment: 'Surgery DV17', deathSave: true },
];

export const HEAD_INJURIES: CriticalInjury[] = [
  { roll: 2, name: 'Lost Eye', effect: 'O olho se foi. -4 em ataques à distância e testes de Perception que dependam da visão.', quickFix: '—', treatment: 'Surgery DV17', deathSave: true },
  { roll: 3, name: 'Brain Injury', effect: '-2 em todas as ações.', quickFix: '—', treatment: 'Surgery DV17', deathSave: true },
  { roll: 4, name: 'Damaged Eye', effect: '-2 em ataques à distância e testes de Perception que dependam da visão.', quickFix: 'Paramedic DV15', treatment: 'Surgery DV13', deathSave: false },
  { roll: 5, name: 'Concussion', effect: '-2 em todas as ações.', quickFix: 'First Aid ou Paramedic DV13', treatment: 'O Quick Fix remove o efeito de vez', deathSave: false },
  { roll: 6, name: 'Broken Jaw', effect: '-4 em todas as ações que envolvam fala.', quickFix: 'Paramedic DV13', treatment: 'Paramedic ou Surgery DV13', deathSave: false },
  { roll: 7, name: 'Foreign Object', effect: 'No fim de todo turno em que se mover mais de 4 m/yds a pé, sofre de novo o dano bônus desta lesão direto nos HP.', quickFix: 'First Aid ou Paramedic DV13', treatment: 'O Quick Fix remove o efeito de vez', deathSave: false },
  { roll: 8, name: 'Whiplash', effect: 'Só aumenta a penalidade base de Death Save.', quickFix: 'Paramedic DV13', treatment: 'Paramedic ou Surgery DV13', deathSave: true },
  { roll: 9, name: 'Cracked Skull', effect: 'Aimed Shots na sua cabeça multiplicam por 3 (em vez de 2) o dano que passa do SP.', quickFix: 'Paramedic DV15', treatment: 'Paramedic ou Surgery DV15', deathSave: true },
  { roll: 10, name: 'Damaged Ear', effect: 'Se mover mais de 4 m/yds a pé num turno, não pode fazer Ação de Movimento no turno seguinte. -2 em Perception que dependa da audição.', quickFix: 'Paramedic DV13', treatment: 'Surgery DV13', deathSave: false },
  { roll: 11, name: 'Crushed Windpipe', effect: 'Você não consegue falar.', quickFix: '—', treatment: 'Surgery DV15', deathSave: true },
  { roll: 12, name: 'Lost Ear', effect: 'A orelha se foi. Se mover mais de 4 m/yds a pé num turno, não pode fazer Ação de Movimento no turno seguinte. -4 em Perception que dependa da audição.', quickFix: '—', treatment: 'Surgery DV17', deathSave: true },
];
