import { describe, expect, it } from 'vitest';
import tokens from './tokens.css?raw';

/** A foreground colour, the ratio it needs, and the backgrounds it is used on; names without `--colour-`. */
type Pair = readonly [foreground: string, minimum: number, backgrounds: readonly string[]];

// The pairs in S00.01.03's Notes, from foundations.md's colour table.
const pairs: readonly Pair[] = [
  // Text at 7:1 (WCAG 1.4.6).
  ['text', 7, ['page', 'surface', 'surface-muted', 'hover', 'pressed', 'action-tint', 'error-tint', 'success-tint',
    'attention-tint']],
  ['text-secondary', 7, ['page', 'surface', 'surface-muted', 'hover', 'pressed']],
  ['action', 7, ['page', 'surface', 'surface-muted', 'hover']],
  ['action-hover', 7, ['page']],
  ['on-action', 7, ['action', 'action-hover', 'action-pressed', 'error', 'error-hover', 'error-pressed']],
  ['error', 7, ['page', 'surface', 'error-tint']],
  ['attention-text', 7, ['attention-tint-strong']],
  ['neutral-text', 7, ['neutral-tint']],
  ['on-badge', 7, ['badge']],

  // The two lower ratios foundations.md sets, at A32's 4.5:1: the placeholder (design.md §4.10), and the secondary
  // button's label while the button is held down.
  ['placeholder', 4.5, ['surface', 'surface-muted']],
  ['action', 4.5, ['action-tint-pressed']],

  // Non-text colours at 3:1 (WCAG 1.4.11): control borders, the focus ring, the button fills, banner edges and the
  // tick. The ring is not paired with the dark fills, where it would fail: its offset puts page colour between them.
  ['border-control', 3, ['page']],
  ['focus', 3, ['page', 'surface', 'surface-muted', 'hover', 'pressed', 'action-tint', 'action-tint-pressed',
    'error-tint', 'success-tint', 'attention-tint', 'attention-tint-strong', 'neutral-tint']],
  ['action', 3, ['page']],
  ['error', 3, ['page']],
  ['attention-edge', 3, ['page', 'attention-tint', 'attention-tint-strong']],
  ['success', 3, ['page', 'success-tint']],
];

// The divider is decorative, as spacing does the grouping, and the backdrop is a see-through layer behind a dialog,
// whose contrast depends on the page under it.
const unpaired = ['divider', 'backdrop'];

/**
 * What breaks the contrast rules in a stylesheet's `--colour-` tokens: each pair below its ratio, and each token in no
 * pair and not in `exempt`, so that a new colour cannot skip the check. Throws on a paired token that is missing or is
 * not a #rrggbb colour, and on a token declared twice, of whose values the check would read only one.
 */
function contrastFailures(css: string, checked: readonly Pair[], exempt: readonly string[]): string[] {
  // Without its comments, so that a token named in one is not taken for a declaration.
  const declarations = css.replaceAll(/\/\*[\s\S]*?\*\//g, '').matchAll(/--colour-([\w-]+)\s*:\s*([^;}]+)/g);
  const colours = new Map<string, string>();
  for (const [, name, value] of declarations) {
    if (colours.has(name)) {
      throw new Error(`--colour-${name} is declared more than once.`);
    }

    colours.set(name, value.trim());
  }

  const failures: string[] = [];
  for (const [foreground, minimum, backgrounds] of checked) {
    for (const background of backgrounds) {
      const ratio = contrast(hex(colours, foreground), hex(colours, background));
      if (ratio < minimum) {
        // Rounded down, so that a ratio just below its minimum never shows as reaching it.
        const shown = (Math.floor(ratio * 100) / 100).toFixed(2);
        failures.push(`--colour-${foreground} on --colour-${background} is ${shown}:1, below ${minimum}:1`);
      }
    }
  }

  const named = new Set([...exempt, ...checked.flatMap(([foreground, , backgrounds]) => [foreground, ...backgrounds])]);
  for (const name of colours.keys()) {
    if (!named.has(name)) {
      failures.push(`--colour-${name} is in no pair`);
    }
  }

  return failures;
}

function hex(colours: ReadonlyMap<string, string>, name: string): string {
  const value = colours.get(name);
  if (value === undefined) {
    throw new Error(`--colour-${name} is not declared.`);
  }

  if (!/^#[0-9a-f]{6}$/i.test(value)) {
    throw new Error(`--colour-${name} is ${value}, not a #rrggbb colour.`);
  }

  return value;
}

// WCAG 2's contrast ratio, from each colour's relative luminance.
function contrast(first: string, second: string): number {
  const [a, b] = [luminance(first), luminance(second)];
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

function luminance(colour: string): number {
  const [r, g, b] = [1, 3, 5].map((start) => {
    const channel = parseInt(colour.slice(start, start + 2), 16) / 255;
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

describe('tokens.css', () => {
  it('reaches the contrast ratio of each pair, and has no colour token in no pair', () => {
    expect(contrastFailures(tokens, pairs, unpaired)).toEqual([]);
  });
});

describe('the contrast check', () => {
  const samplePairs: readonly Pair[] = [['text', 7, ['page']]];

  it('fails on a text colour below 7:1', () => {
    const sample = ':root { --colour-text: #767676; --colour-page: #ffffff; }';

    expect(contrastFailures(sample, samplePairs, [])).toEqual(['--colour-text on --colour-page is 4.54:1, below 7:1']);
  });

  it('fails on a colour token in no pair, other than one it is told to leave out', () => {
    const sample = `:root {
      --colour-text: #0b0c0c; --colour-page: #ffffff; --colour-new: #00558b; --colour-rule: #b1b4b6;
    }`;

    expect(contrastFailures(sample, samplePairs, ['rule'])).toEqual(['--colour-new is in no pair']);
  });
});
