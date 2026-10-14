import { describe, expect, it } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { userRefusal, validateUser, type UserValues } from './userRules.ts';

const alex: UserValues = { displayName: 'Alex Park', email: 'alex.park@example.org', role: 'Worker' };

const enterName = 'Enter the person’s name';
const nameTooLong = 'Name must be 100 characters or less';
const enterEmail = 'Enter an email address';
const emailFormat = 'Enter an email address in the correct format, like name@example.com';
const chooseRole = 'Choose Worker or Manager';

describe('validateUser', () => {
  it('passes a name and an address, with the spaces round them that are trimmed as they are sent', () => {
    expect(validateUser({ ...alex, displayName: '  Alex Park ', email: ' alex.park@example.org  ' })).toEqual({});
  });

  it.each([
    ['empty', '', enterName],
    ['spaces alone', '   ', enterName],
    ['101 characters', 'A'.repeat(101), nameTooLong],
  ])('refuses a name that is %s', (_, displayName, message) => {
    expect(validateUser({ ...alex, displayName })).toEqual({ displayName: message });
  });

  it('passes a name of 100 characters, counted once trimmed, in UTF-16 units as the server counts them', () => {
    expect(validateUser({ ...alex, displayName: ` ${'A'.repeat(100)} ` })).toEqual({});
    // Each emoji is two UTF-16 units, so 50 of them are 100.
    expect(validateUser({ ...alex, displayName: '😀'.repeat(50) })).toEqual({});
    expect(validateUser({ ...alex, displayName: `${'😀'.repeat(50)}A` })).toEqual({ displayName: nameTooLong });
  });

  it.each([
    ['empty', '', enterEmail],
    ['spaces alone', '  ', enterEmail],
    ['with no "@"', 'alex', emailFormat],
    ['with nothing before the "@"', '@example.org', emailFormat],
    ['with nothing after the "@"', 'alex@', emailFormat],
    ['with two "@"', 'alex@park@example.org', emailFormat],
  ])('refuses an address %s', (_, email, message) => {
    expect(validateUser({ ...alex, email })).toEqual({ email: message });
  });

  it('leaves the rest of an address to the server, as the loose check does', () => {
    expect(validateUser({ ...alex, email: 'zoë@example' })).toEqual({});
  });

  it('gives a message for each field at fault at once', () => {
    expect(validateUser({ displayName: '', email: '', role: 'Manager' })).toEqual({
      displayName: enterName,
      email: enterEmail,
    });
  });
});

describe('userRefusal', () => {
  it("gives a 409 user.email_in_use as Email's message", () => {
    expect(userRefusal(new ApiError(409, 'user.email_in_use'), alex)).toEqual({
      errors: {
        email:
          'Another account already uses this email address. Use a different one, or find the person in the Users '
          + 'list.',
      },
      unknownField: false,
    });
  });

  it("words each key of a 422's errors{} as the form does, never as the server does", () => {
    const refused = new ApiError(422, 'validation.failed', {
      displayName: ['The server’s words'],
      email: ['The server’s words'],
      role: ['The server’s words'],
    });

    expect(userRefusal(refused, alex)).toEqual({
      errors: { displayName: nameTooLong, email: emailFormat, role: chooseRole },
      unknownField: false,
    });
  });

  it("gives the name's message for what was sent", () => {
    const refused = new ApiError(422, 'validation.failed', { displayName: ['The server’s words'] });

    expect(userRefusal(refused, { ...alex, displayName: '' })?.errors).toEqual({ displayName: enterName });
  });

  it('says that the server named a field the form does not have', () => {
    const refused = new ApiError(422, 'validation.failed', { organisationId: ['x'], email: ['y'] });

    expect(userRefusal(refused, alex)).toEqual({ errors: { email: emailFormat }, unknownField: true });
  });

  it.each([
    ['a failure that is no ApiError', new TypeError('Failed to fetch')],
    ['no connection', new ApiError(0)],
    ['a 500', new ApiError(500)],
    ['a 409 with another code', new ApiError(409, 'user.last_manager')],
    ['a 422 with no field errors', new ApiError(422, 'validation.failed')],
    ['a 422 whose field errors are empty', new ApiError(422, 'validation.failed', {})],
  ])('gives nothing for %s, which names no field', (_, error) => {
    expect(userRefusal(error, alex)).toBeUndefined();
  });
});
