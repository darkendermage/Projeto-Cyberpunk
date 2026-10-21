// How Critical Injuries happen (CP:R p.187). The tables themselves live in the server rules file
// (character-rules.json) and reach the Tables page and the character sheet through the API.

export const CRITICAL_RULES = [
  'Ocorre quando dois ou mais dados de dano de um ataque corpo a corpo ou à distância saem 6.',
  'Role 2d6 na tabela adequada até sair uma lesão que o alvo ainda não tem. Sem Aimed Shot na cabeça, use a tabela do corpo.',
  'Toda Critical Injury causa 5 de dano bônus direto nos Hit Points: não desgasta armadura e não é multiplicado pelo local.',
  'A lesão e o dano bônus acontecem mesmo que o dano do ataque não passe do SP do alvo.',
];
