import { createContext } from 'react';

/** The shell's `#page-bar` slot, which `PageBar` renders into, or `null` until the slot is in the DOM. */
export const PageBarContext = createContext<HTMLElement | null>(null);
