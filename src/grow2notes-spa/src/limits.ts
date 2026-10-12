/**
 * The length limits the SPA checks, each named after the server's `Limits` constant it mirrors, in camelCase
 * (design.md §5.1, D71), so that a form refuses what the API would. `SpaLimitsTests` in tests/Grow2Notes.Tests checks
 * every value here against its constant. Each limit is added by the story that first checks it (form-validation.md).
 */
export const limits = {
  passwordMinLength: 12,
} as const;
