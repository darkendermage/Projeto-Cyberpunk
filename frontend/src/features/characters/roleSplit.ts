import type { RoleSplitRule } from '../../api/characterTypes';

/** The total a split may use at this Rank: the Rank, or twice it for the Tech's specialties (CP:R p.147). */
export const splitTotal = (rule: RoleSplitRule, rank: number) => (rule.max === '2xrank' ? rank * 2 : rank);
