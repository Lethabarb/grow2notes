import { Outlet } from 'react-router';
import { useMe } from '../api/me.ts';
import { PageNotFound } from './PageNotFound.tsx';

/**
 * The layout route around the manager screens, which shows a worker "Page not found" in their place, so that the app
 * never shows that a manager screen exists, as the API answers a worker `404`, not `403` (design.md §9.2;
 * app-shell-nav.md *Implementation notes*).
 */
export function ManagerOnly() {
  return useMe().role === 'Manager' ? <Outlet /> : <PageNotFound />;
}
