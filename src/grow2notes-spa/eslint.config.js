import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';

// Words on screen come from src/copy (microcopy.md §8). ESLint's own rule stands in for eslint-plugin-react's
// jsx-no-literals, whose latest release does not allow ESLint 10. Like that rule at its defaults it reads no props, so
// a label passed in a prop, or a literal nested deeper in a child expression, is left to review.
const jsxChild = ':matches(JSXElement, JSXFragment) > JSXExpressionContainer';
// The backslash is doubled so that the selector gets `\S`; a single one would leave just `S`.
const words = ':matches(Literal[value=/\\S/], TemplateLiteral)';
const wordsInJsx = [
  'JSXText[value=/\\S/]',
  `${jsxChild} > ${words}`,
  `${jsxChild} > :matches(ConditionalExpression, LogicalExpression) > ${words}`,
].map((selector) => ({
  selector,
  message: 'Words on screen come from src/copy (microcopy.md §8): write them there, not in JSX.',
}));

export default defineConfig([
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      // The type-checked set rather than the strict one: typescript-eslint changes it only in a major version, so a
      // minor update cannot break the zero-warning lint step.
      tseslint.configs.recommendedTypeChecked,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
  {
    files: ['src/**/*.tsx'],
    // Not the tests, whose words are made up, or src/copy, where the words live.
    ignores: ['src/**/*.test.tsx', 'src/copy/**'],
    rules: {
      'no-restricted-syntax': ['error', ...wordsInJsx],
    },
  },
]);
