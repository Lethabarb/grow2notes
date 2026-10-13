import { Pause } from './Pause.tsx';

/**
 * The separator " · " between two parts of one line (microcopy.md §1 and §7). Screen readers skip the middle dot and
 * read a comma in its place, so "Today · Thursday 1 October" is read "Today, Thursday 1 October".
 */
export function Sep() {
  // The space after the dot is shown and read, outside both spans: a name computed from the text, as Testing Library
  // computes it, drops a space at the end of the hidden comma's span, and would read "Today,Thursday".
  return (
    <>
      <span aria-hidden="true"> ·</span>
      <Pause />{' '}
    </>
  );
}
