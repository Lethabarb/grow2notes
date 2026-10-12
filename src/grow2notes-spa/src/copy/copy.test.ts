import { describe, expect, it } from 'vitest';

// Every module in src/copy but its tests, whose samples hold the very words checked for, by its path from the SPA's
// root. Read whole, comments included, so that the check stays a plain search.
const modules = import.meta.glob<string>(['/src/copy/**/*.{ts,tsx}', '!**/*.test.{ts,tsx}'], {
  query: '?raw',
  import: 'default',
  eager: true,
});

/** A pattern the copy must not hold, and the strings, if any, in which it may. */
type Check = { pattern: RegExp; allowed?: RegExp };

// microcopy.md §8's patterns, with \w* after a word's start, so that a failure names the whole word.
const checks: readonly Check[] = [
  // The banned words.
  { pattern: /\b(?:log ?in|login|log out|click|invalid|oops|sorry)\b/gi },
  { pattern: /\bdelete\w*/gi },
  // design.md's "Please check your ticks." stays word for word (microcopy.md §1).
  { pattern: /\bplease\b/gi, allowed: /Please check your ticks\./g },
  // The US spellings.
  { pattern: /\b(?:organiz\w*|authoriz\w*|recogniz\w*|color\b|center\b|canceled\w*)/gi },
  // A negative contraction, with a straight or a typographic apostrophe, typed or escaped (\' or \u2019), but in
  // design.md's two "can't" strings, which stay word for word (microcopy.md §1, Contractions) with either one:
  // design.md types the straight apostrophe, and src/copy the typographic (§1, Quotes and apostrophes).
  {
    pattern: /\b[a-z]+n(?:\\?['’]|\\u2019)t\b/gi,
    allowed: /this note can(?:\\?['’]|\\u2019)t be started|and can(?:\\?['’]|\\u2019)t sign in/g,
  },
];

/** What breaks the checks in a set of modules, as each file and the word it holds. */
function wordingFailures(files: Readonly<Record<string, string>>): string[] {
  const failures: string[] = [];
  for (const [file, text] of Object.entries(files)) {
    for (const { pattern, allowed } of checks) {
      const searched = allowed === undefined ? text : text.replaceAll(allowed, ' ');
      for (const [word] of searched.matchAll(pattern)) {
        failures.push(`${file}: ${word}`);
      }
    }
  }

  return failures;
}

describe('the modules in src/copy', () => {
  it('are read, strings.ts, messages.ts and format.ts among them', () => {
    // Without them, the check below would pass on no files.
    expect(modules).toHaveProperty(['/src/copy/strings.ts'], expect.stringContaining('export const strings'));
    expect(modules).toHaveProperty(['/src/copy/messages.ts'], expect.stringContaining('export const msg'));
    expect(modules).toHaveProperty(['/src/copy/format.ts'], expect.stringContaining('export function stamp'));
  });

  it('have no banned word, no US spelling and no negative contraction', () => {
    expect(wordingFailures(modules)).toEqual([]);
  });
});

describe('the wording check', () => {
  it("fails on each of microcopy.md §8's banned words, naming the file and the word", () => {
    const sample = {
      '/src/copy/a.ts': `export const a = [
        'Log in', 'login', 'Log out', 'Click Save', 'Invalid code', 'Oops', 'Sorry',
      ];`,
      '/src/copy/b.ts': `// Deleted when it is discarded.
        export const b = 'Delete draft';`,
    };

    expect(wordingFailures(sample)).toEqual([
      '/src/copy/a.ts: Log in',
      '/src/copy/a.ts: login',
      '/src/copy/a.ts: Log out',
      '/src/copy/a.ts: Click',
      '/src/copy/a.ts: Invalid',
      '/src/copy/a.ts: Oops',
      '/src/copy/a.ts: Sorry',
      '/src/copy/b.ts: Deleted',
      '/src/copy/b.ts: Delete',
    ]);
  });

  it('fails on please, but in "Please check your ticks."', () => {
    const sample = {
      '/src/copy/a.ts': `export const a = [
        'The goal or common-item list was just changed. Please check your ticks.',
        'Please try again.',
        'Enter the code, please.',
      ];`,
    };

    expect(wordingFailures(sample)).toEqual(['/src/copy/a.ts: Please', '/src/copy/a.ts: please']);
  });

  it("fails on each of microcopy.md §8's US spellings", () => {
    const sample = {
      '/src/copy/a.ts': "export const a = ['organization', 'Authorize', 'recognized', 'color', 'center', 'canceled'];",
    };

    expect(wordingFailures(sample)).toEqual([
      '/src/copy/a.ts: organization',
      '/src/copy/a.ts: Authorize',
      '/src/copy/a.ts: recognized',
      '/src/copy/a.ts: color',
      '/src/copy/a.ts: center',
      '/src/copy/a.ts: canceled',
    ]);
  });

  it(`fails on a negative contraction, with either apostrophe, but in design.md's two "can't" strings`, () => {
    const sample = {
      '/src/copy/a.ts': `export const a = [
        'It’s now after midnight, so this note can’t be started for yesterday. Ask a manager to record it.',
        "Jane Citizen will be signed out everywhere now and can't sign in. Their notes stay.",
        "this note can't be started", 'this note can\\'t be started', 'this note can\\u2019t be started',
        'and can’t sign in', 'and can\\'t sign in', 'and can\\u2019t sign in',
        "This note can't be submitted.",
        'You can’t sign in.',
        'You can\\'t sign in.',
        "It isn't saved.",
        'It didn’t load.',
        'It didn\\u2019t load.',
        // Won't change.
      ];`,
    };

    expect(wordingFailures(sample)).toEqual([
      "/src/copy/a.ts: can't",
      '/src/copy/a.ts: can’t',
      "/src/copy/a.ts: can\\'t",
      "/src/copy/a.ts: isn't",
      '/src/copy/a.ts: didn’t',
      '/src/copy/a.ts: didn\\u2019t',
      "/src/copy/a.ts: Won't",
    ]);
  });

  it('passes the words that the app uses in their place', () => {
    const sample = {
      '/src/copy/a.ts': `// The organisation's colours, centred.
        export const a = [
          'Sign in', 'Signed in as', 'Sign out', 'Sign-in failed', 'Discard draft', 'Archive', 'Try again',
          'organisation', 'Authorise', 'recognised', 'colour', 'centre', 'cancelled',
          'It’s saved.', 'You’ll get an email.', 'You cannot sign in.', 'It did not load.',
        ];`,
    };

    expect(wordingFailures(sample)).toEqual([]);
  });
});
