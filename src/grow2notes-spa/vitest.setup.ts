import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Testing Library unmounts rendered trees by itself only when Vitest's globals are on, and they are off here.
afterEach(cleanup);

// jsdom has neither showModal() nor close() (S00.04.07's Notes), so these stand in for them, as far as the open
// attribute, the return value that close() is given and the `close` event, which the browser fires in a task of its
// own. What else the browser does, such as moving focus in and back out, is checked end to end. Not in a test file
// that runs in Node, which has no DOM.
if ('HTMLDialogElement' in globalThis) {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement, returnValue?: string) {
    if (!this.open) {
      return;
    }

    if (returnValue !== undefined) {
      this.returnValue = returnValue;
    }

    this.open = false;
    setTimeout(() => {
      this.dispatchEvent(new Event('close'));
    });
  };
}

// jsdom's scrollTo() only reports that it is not implemented, as jsdom lays nothing out, so this stands in for it, as
// far as the position that scrollY then reads, which it takes whatever the page's length, with no scroll event. Where
// a page is on screen is checked end to end.
if ('window' in globalThis) {
  window.scrollTo = function scrollTo(xOrOptions?: number | ScrollToOptions, y?: number) {
    const top = typeof xOrOptions === 'object' ? xOrOptions.top : y;
    if (top !== undefined) {
      Object.defineProperty(window, 'scrollY', { value: top, configurable: true, writable: true });
    }
  };
}

// jsdom has no <search> element, which every browser the app supports has had since 2023, so React warns in tests
// alone that Find a participant's landmark is unrecognised. Only that warning is let go: every other one still shows.
const consoleError = console.error;
console.error = (...args: unknown[]) => {
  const unrecognisedSearch =
    typeof args[0] === 'string' && args[0].startsWith('The tag <%s> is unrecognized') && args[1] === 'search';
  if (!unrecognisedSearch) {
    consoleError(...args);
  }
};
