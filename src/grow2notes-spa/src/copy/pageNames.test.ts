import { describe, expect, it } from 'vitest';
import type { AppRoute } from '../routes/routes.tsx';
import { msg } from './messages.ts';
import { pageName } from './pageNames.ts';

describe('msg.pageTitle', () => {
  it('puts "Grow2Notes", an en dash and the page name together, with a space either side of the dash', () => {
    expect(msg.pageTitle(pageName.today)).toBe('Grow2Notes – Today');
  });

  // npm run typecheck fails if either line compiles.
  it('takes a page name from the list only, and so does a route', () => {
    // @ts-expect-error A name outside the list.
    expect(msg.pageTitle('Home')).toBe('Grow2Notes – Home');
    // @ts-expect-error A route that declares a name outside the list.
    const route: AppRoute = { path: 'home', handle: { pageName: 'Home' } };
    expect(route.handle).toEqual({ pageName: 'Home' });
  });
});
