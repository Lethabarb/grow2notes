// @vitest-environment node
import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

// eslint.config.js's rule that keeps words out of JSX (microcopy.md §8). No JSX in src holds a word for it to refuse,
// so `npm run lint` would stay green with a selector that matches nothing; these samples, linted with the real config,
// are refused or passed by the rule itself.
const eslint = new ESLint();

/** The rule's messages on a component that returns `jsx`, linted as the file at `filePath`. */
async function refusals(jsx: string, filePath = 'src/App.tsx'): Promise<string[]> {
  const code = [
    "import { strings } from './copy/strings.ts';",
    'export function App({ busy }: { busy: boolean }) {',
    `  return ${jsx};`,
    '}',
    '',
  ].join('\n');
  // typescript-eslint's project service parses only a file that a tsconfig includes, so the path is a real one.
  const [result] = await eslint.lintText(code, { filePath });
  // A sample that is not parsed gives one message with no rule, which would pass every "is not refused" case below.
  expect(result.messages.filter((message) => message.fatal)).toEqual([]);
  return result.messages.filter((message) => message.ruleId === 'no-restricted-syntax').map(({ message }) => message);
}

// The first lint loads the config's plugins and builds the TypeScript program, which took over 2 s with the other
// test files running, near Vitest's 5 s default.
describe('the lint rule on words in JSX', { timeout: 30_000 }, () => {
  // No sample holds a capital S, which is all that a selector whose `\S` has lost its backslash still matches.
  it.each([
    ['typed text', '<p>Ready</p>', 1],
    ['typed text in a fragment', '<>Ready</>', 1],
    ['a string literal', "<p>{'Ready'}</p>", 1],
    ['a template literal', '<p>{`Ready`}</p>', 1],
    ['each branch of a conditional', "<p>{busy ? 'Busy' : 'Ready'}</p>", 2],
    ['a logical expression', "<p>{busy && 'Busy'}</p>", 1],
  ])('refuses %s as a JSX child, pointing to src/copy', async (_, jsx, count) => {
    const messages = await refusals(jsx);

    expect(messages).toHaveLength(count);
    expect(messages[0]).toContain('src/copy');
  });

  it.each([
    ['words from src/copy with a space between them', "<p>{strings.appName}{' '}{strings.loading}</p>"],
    ['whitespace between elements', '<div>\n    <p>{strings.appName}</p>\n  </div>'],
    ['an attribute value', '<p className="note">{strings.appName}</p>'],
    ['a conditional in a prop', "<p title={busy ? 'a' : 'b'}>{strings.appName}</p>"],
  ])('does not refuse %s', async (_, jsx) => {
    expect(await refusals(jsx)).toEqual([]);
  });

  it("does not refuse a test's made-up words", async () => {
    expect(await refusals('<p>Ready</p>', 'src/App.test.tsx')).toEqual([]);
  });
});
