/** A person's names, as Find a participant matches them: names only, never a date of birth or a status. */
export type PersonName = { givenName: string; familyName: string };

/**
 * Text as Find a participant compares it (search-filter.md *Behaviour*): accents taken off ("Zoë" is "zoe"), lower
 * case, apostrophes taken out ("O’Brien" is "obrien"), and every other run of characters that are not letters or
 * digits, such as a hyphen, a comma or the full stop that dictation adds, one space, with none at either end.
 */
function normalise(text: string): string {
  return text
    .normalize('NFKD')
    .replace(/\p{M}/gu, '')
    .toLocaleLowerCase('en-AU')
    .replace(/['‘’ʼ]/gu, '')
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();
}

/**
 * Whether a person matches what was typed in Find a participant: every typed word starts a word of their given and
 * family names, in any order, so "cit jane" finds Jane Citizen. Typed text that is nothing once normalised, such as
 * spaces or ".", matches everyone, so the whole list shows.
 */
export function matchesName({ givenName, familyName }: PersonName, typed: string): boolean {
  const query = normalise(typed);
  if (query === '') {
    return true;
  }

  const words = normalise(`${givenName} ${familyName}`).split(' ');
  return query.split(' ').every((part) => words.some((word) => word.startsWith(part)));
}
