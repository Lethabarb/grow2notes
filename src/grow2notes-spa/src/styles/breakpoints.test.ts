import { describe, expect, it } from 'vitest';

// Every stylesheet under src/, components' CSS Modules included, by its path from the SPA's root.
const stylesheets = import.meta.glob<string>('/src/**/*.css', { query: '?raw', import: 'default', eager: true });

// The one breakpoint, and the note form's query for short screens (foundations.md, Breakpoint).
const allowed = ['(min-width: 40rem)', '(max-height: 30rem)'];

/**
 * What breaks foundations.md's one breakpoint in a set of stylesheets, as each file and rule: an `@media` rule with a
 * condition on width or height other than the allowed two, range syntax included, and any `@container` rule. Other
 * media features, such as `hover`, and `min-width` as a property are not breakpoints.
 */
function breakpointFailures(files: Readonly<Record<string, string>>): string[] {
  const failures: string[] = [];
  for (const [file, css] of Object.entries(files)) {
    // Without its comments, so that a query named in one is not taken for a rule.
    const rules = css.replaceAll(/\/\*[\s\S]*?\*\//g, '').matchAll(/@(media|container)(?![\w-])([^{]*)\{/gi);
    for (const [, name, prelude] of rules) {
      if (name.toLowerCase() === 'container' || hasOtherSizeCondition(prelude)) {
        failures.push(`${file}: @${name} ${prelude.trim().replaceAll(/\s+/g, ' ')}`);
      }
    }
  }

  return failures;
}

function hasOtherSizeCondition(prelude: string): boolean {
  // Spacing and letter case do not change a condition, so (min-width:40rem) is the allowed one too.
  const conditions = allowed.reduce(
    (rest, condition) => rest.replaceAll(condition, ''),
    prelude.toLowerCase().replaceAll(/\s*([():])\s*/g, '$1').replaceAll(':', ': '),
  );

  // Any width or height left, as a feature (max-width, min-device-height) or in range syntax (width >= 40rem).
  return /\b(?:width|height)\b/.test(conditions);
}

describe('the stylesheets under src/', () => {
  it('are read as they are written', () => {
    // Without Vitest's css option, ?raw gives an empty string, on which the check below would pass.
    expect(stylesheets['/src/styles/tokens.css']).toContain('@media (min-width: 40rem)');
    expect(stylesheets['/src/styles/base.css']).toContain('@media (forced-colors: active)');
  });

  it('have no size media query but (min-width: 40rem) and (max-height: 30rem), and no @container rule', () => {
    expect(breakpointFailures(stylesheets)).toEqual([]);
  });
});

describe('the breakpoint check', () => {
  it('passes the two allowed conditions, other media features, and min-width as a property', () => {
    const sample = {
      '/src/a.css': `
        /* Not @media (min-width: 48rem): a comment is no rule. */
        .row { min-width: 8rem; max-height: 20rem; width: 100%; }
        @media (min-width: 40rem) { .row { min-width: 12rem; } }
        @media (max-height: 30rem) { .bar { position: static; } }
        @media screen and ( MIN-WIDTH:40rem ) and (hover: hover) { .row { inline-size: auto; } }
      `,
      '/src/b.module.css': `
        @media (hover: hover) and (pointer: fine) { .button:hover { background: var(--colour-hover); } }
        @media (forced-colors: active) { .button { border-color: ButtonText; } }
        @media (prefers-reduced-motion: reduce) { .button { transition: none; } }
      `,
    };

    expect(breakpointFailures(sample)).toEqual([]);
  });

  it('fails on a condition on another width or height, naming the file and the rule', () => {
    const sample = {
      '/src/a.css': `
        @media (min-width: 48rem) { .a { margin: 0; } }
        @media (min-width: 640px) { .a { margin: 0; } }
        @media (max-width: 39.99rem) { .a { margin: 0; } }
      `,
      '/src/b.module.css': `
        @media (max-height: 20rem) { .b { margin: 0; } }
        @media (min-height: 30rem) { .b { margin: 0; } }
        @media (min-device-width: 40rem) { .b { margin: 0; } }
        @media (min-width: 40rem) and
          (max-width: 60rem) { .b { margin: 0; } }
      `,
    };

    expect(breakpointFailures(sample)).toEqual([
      '/src/a.css: @media (min-width: 48rem)',
      '/src/a.css: @media (min-width: 640px)',
      '/src/a.css: @media (max-width: 39.99rem)',
      '/src/b.module.css: @media (max-height: 20rem)',
      '/src/b.module.css: @media (min-height: 30rem)',
      '/src/b.module.css: @media (min-device-width: 40rem)',
      '/src/b.module.css: @media (min-width: 40rem) and (max-width: 60rem)',
    ]);
  });

  it('fails on range syntax, even at the allowed sizes', () => {
    const sample = {
      '/src/a.css': `
        @media (width >= 40rem) { .a { margin: 0; } }
        @media (40rem <= width) { .a { margin: 0; } }
        @media (height <= 30rem) { .a { margin: 0; } }
        @media (hover: hover) and (20rem < width < 40rem) { .a { margin: 0; } }
      `,
    };

    expect(breakpointFailures(sample)).toEqual([
      '/src/a.css: @media (width >= 40rem)',
      '/src/a.css: @media (40rem <= width)',
      '/src/a.css: @media (height <= 30rem)',
      '/src/a.css: @media (hover: hover) and (20rem < width < 40rem)',
    ]);
  });

  it('fails on any @container rule', () => {
    const sample = {
      '/src/a.module.css': `
        .slot { container-type: inline-size; }
        @container (min-width: 40rem) { .name { display: none; } }
        @container slot (inline-size < 20rem) { .name { display: none; } }
      `,
    };

    expect(breakpointFailures(sample)).toEqual([
      '/src/a.module.css: @container (min-width: 40rem)',
      '/src/a.module.css: @container slot (inline-size < 20rem)',
    ]);
  });
});
