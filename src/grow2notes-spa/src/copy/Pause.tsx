/**
 * A comma that only screen readers get, so that they pause between two parts of a text that the screen sets apart some
 * other way, by a middle dot (`Sep`) or a new line (microcopy.md §7). Hidden by base.css's `.visually-hidden`.
 */
export function Pause() {
  return <span className="visually-hidden">,</span>;
}
