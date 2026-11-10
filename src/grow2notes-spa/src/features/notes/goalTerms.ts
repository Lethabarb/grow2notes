/**
 * Goals split by term, each term's goals in the order given (A150): the Short term goals, and the Medium or
 * Long term goals. The note form and the read view show them under their own headings, and leave a heading out when its
 * term has no goal, as the report files do (D104).
 */
export function goalsByTerm<T extends { isMediumOrLongTerm: boolean }>(
  goals: readonly T[],
): { shortTerm: T[]; mediumOrLongTerm: T[] } {
  return {
    shortTerm: goals.filter((goal) => !goal.isMediumOrLongTerm),
    mediumOrLongTerm: goals.filter((goal) => goal.isMediumOrLongTerm),
  };
}
