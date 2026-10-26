
import { t } from '../../i18n';// How Critical Injuries happen (CP:R p.187). The tables themselves live in the server rules file
// (character-rules.json) and reach the Tables page and the character sheet through the API.

export const CRITICAL_RULES = [
  t('Ocorre quando dois ou mais dados de dano de um ataque corpo a corpo ou à distância saem 6.', 'Happens when two or more damage dice of a melee or ranged attack roll a 6.'),
  t('Role 2d6 na tabela adequada até sair uma lesão que o alvo ainda não tem. Sem Aimed Shot na cabeça, use a tabela do corpo.', 'Roll 2d6 on the appropriate table until you get an injury the target doesn\'t have yet. Without an Aimed Shot to the head, use the body table.'),
  t('Toda Critical Injury causa 5 de dano bônus direto nos Hit Points: não desgasta armadura e não é multiplicado pelo local.', 'Every Critical Injury deals 5 bonus damage straight to Hit Points: it doesn\'t ablate armor and isn\'t multiplied by location.'),
  t('A lesão e o dano bônus acontecem mesmo que o dano do ataque não passe do SP do alvo.', 'The injury and the bonus damage happen even if the attack\'s damage doesn\'t get past the target\'s SP.'),
];
