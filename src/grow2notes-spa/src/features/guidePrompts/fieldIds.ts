// What every id below starts with, from which `FieldError` makes its message's id, `error`.
const prefix = 'guidePrompts';

/**
 * The ids that tie the page's `<h1>` label, help text and limit hint, which show at once, and the error message, after
 * a Save of text that is too long, to the field, which mounts with the data (guide-prompts.md components 2 to 6).
 */
export const fieldIds = {
  prefix,
  text: `${prefix}-text`,
  help: `${prefix}-help`,
  limit: `${prefix}-limit`,
  error: `${prefix}-error`,
} as const;
