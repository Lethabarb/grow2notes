import { afterEach, describe, expect, it, vi } from 'vitest';
import { confirmLeave, disarmUnloadGuard, rearmUnloadGuard, setLeaveGuard, unloadGuardArmed } from './leaveGuard.ts';

afterEach(() => {
  setLeaveGuard(null);
  rearmUnloadGuard();
});

/** Whether `promise` has settled once the microtasks queued so far have run, with no timer let through. */
async function settledAtOnce(promise: Promise<unknown>) {
  let settled = false;
  void promise.then(() => {
    settled = true;
  });
  await Promise.resolve();
  return settled;
}

describe('confirmLeave', () => {
  // S01.04.02's own criterion: no screen but Guide prompts registers a guard, so no other screen warns.
  it('resolves true at once when no page has registered a guard', async () => {
    const leave = confirmLeave();

    expect(await settledAtOnce(leave)).toBe(true);
    await expect(leave).resolves.toBe(true);
  });

  it.each([true, false])('asks the guard registered, once, and resolves with its answer, %s', async (answer) => {
    const guard = vi.fn(() => Promise.resolve(answer));
    setLeaveGuard(guard);

    await expect(confirmLeave()).resolves.toBe(answer);
    expect(guard).toHaveBeenCalledOnce();
  });

  it('asks only the guard registered last', async () => {
    const replaced = vi.fn(() => Promise.resolve(false));
    const current = vi.fn(() => Promise.resolve(true));
    setLeaveGuard(replaced);
    setLeaveGuard(current);

    await expect(confirmLeave()).resolves.toBe(true);
    expect(replaced).not.toHaveBeenCalled();
    expect(current).toHaveBeenCalledOnce();
  });

  it('resolves true at once again once the guard has been removed, without asking it', async () => {
    const guard = vi.fn(() => Promise.resolve(false));
    setLeaveGuard(guard);
    setLeaveGuard(null);

    const leave = confirmLeave();

    expect(await settledAtOnce(leave)).toBe(true);
    await expect(leave).resolves.toBe(true);
    expect(guard).not.toHaveBeenCalled();
  });
});

describe('the unload guard', () => {
  it('is armed until it is disarmed, and again once it is rearmed', () => {
    expect(unloadGuardArmed()).toBe(true);

    disarmUnloadGuard();
    expect(unloadGuardArmed()).toBe(false);

    rearmUnloadGuard();
    expect(unloadGuardArmed()).toBe(true);
  });

  it('is left as it was by registering or removing a leave guard', () => {
    disarmUnloadGuard();
    setLeaveGuard(() => Promise.resolve(true));
    setLeaveGuard(null);

    expect(unloadGuardArmed()).toBe(false);
  });
});
