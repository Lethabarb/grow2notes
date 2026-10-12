import { describe, expect, it } from 'vitest';
import type { Me } from './me.ts';

// A made-up manager (A35).
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

describe('Me', () => {
  // npm run typecheck fails if the comparison compiles.
  it("types role as the API's role names, so that a comparison with 'manager' does not compile", () => {
    // @ts-expect-error The API gives 'Manager', with a capital, so this comparison would never match.
    expect(manager.role === 'manager').toBe(false);
  });
});
