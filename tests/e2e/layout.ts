import type { Locator } from '@playwright/test';

/** Where the element shows on the page. */
export async function boxOf(locator: Locator) {
  const box = await locator.boundingBox();
  if (box === null) {
    throw new Error(`${locator.toString()} is not shown.`);
  }

  return box;
}
