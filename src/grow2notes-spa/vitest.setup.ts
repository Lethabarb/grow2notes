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
