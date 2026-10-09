import { fireEvent, render, screen, within } from '@testing-library/react';
import { useState, type FormEvent } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Button } from './Button.tsx';
import { ErrorSummary, type FormError } from './ErrorSummary.tsx';
import { FieldGroup } from './FieldGroup.tsx';
import { PageHeading } from './PageHeading.tsx';

const nameId = 'invite-name';
const roleId = 'invite-role';
const roles = ['worker', 'manager'] as const;
type Role = (typeof roles)[number];

const nameError = "Enter the person's name";
const roleError = 'Choose Worker or Manager';
const pageTitle = 'Grow2Notes – Invite user';

type Values = { name: string; role?: Role };
type Errors = { name?: string; role?: string };

function check({ name, role }: Values): Errors {
  return { name: name.trim() === '' ? nameError : undefined, role: role === undefined ? roleError : undefined };
}

/**
 * A form of a text field and a set of radios that checks itself as form-validation.md has it: on submit, and then, as
 * the person types or chooses, each field that shows an error, whose error goes once the field is fixed.
 */
function InviteForm() {
  const [values, setValues] = useState<Values>({ name: '' });
  const [errors, setErrors] = useState<Errors>({});
  const [attempt, setAttempt] = useState(0);

  function change(next: Values) {
    setValues(next);
    const found = check(next);
    setErrors((shown) => ({
      name: shown.name === undefined ? undefined : found.name,
      role: shown.role === undefined ? undefined : found.role,
    }));
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    setErrors(check(values));
    setAttempt((count) => count + 1);
  }

  // In page order, a group's error linked to its first control.
  const summary: FormError[] = [];
  if (errors.name !== undefined) {
    summary.push({ fieldId: nameId, message: errors.name });
  }

  if (errors.role !== undefined) {
    summary.push({ fieldId: `${roleId}-${roles[0]}`, message: errors.role });
  }

  return (
    <main>
      <ErrorSummary errors={summary} attempt={attempt} />
      <PageHeading>Invite user</PageHeading>
      <form noValidate onSubmit={submit}>
        <FieldGroup fieldId={nameId} label="Name" hint="As they like to be called" error={errors.name}>
          <input
            id={nameId}
            type="text"
            value={values.name}
            aria-describedby={errors.name === undefined ? `${nameId}-hint` : `${nameId}-hint ${nameId}-error`}
            aria-invalid={errors.name === undefined ? undefined : true}
            onChange={(event) => change({ ...values, name: event.currentTarget.value })}
          />
        </FieldGroup>
        <FieldGroup fieldId={roleId} label="Role" group error={errors.role}>
          {roles.map((role) => (
            <div key={role}>
              <input
                type="radio"
                id={`${roleId}-${role}`}
                name="role"
                checked={values.role === role}
                onChange={() => change({ ...values, role })}
              />
              <label htmlFor={`${roleId}-${role}`}>{role === 'worker' ? 'Worker' : 'Manager'}</label>
            </div>
          ))}
        </FieldGroup>
        <Button type="submit" variant="primary">Send invite</Button>
      </form>
    </main>
  );
}

/** Presses Send invite as a person does, which moves focus to the button. */
function pressSend() {
  const send = screen.getByRole('button', { name: 'Send invite' });
  send.focus();
  fireEvent.click(send);
}

/** The error summary, by its heading. */
function summary() {
  const container = screen.getByRole('heading', { level: 2, name: 'There is a problem' }).parentElement;
  if (container === null) {
    throw new Error('The heading is in no summary.');
  }

  return container;
}

/** jsdom follows a link to a part of the page in a task of its own, so its effect shows only after this. */
async function afterNavigation() {
  await new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
}

// jsdom has no scrollIntoView. The spy records the element each call scrolled.
const scrollIntoView = vi.fn<(this: Element, options?: boolean | ScrollIntoViewOptions) => void>();

beforeEach(() => {
  document.title = pageTitle;
  Element.prototype.scrollIntoView = scrollIntoView;
});

afterEach(() => {
  Reflect.deleteProperty(Element.prototype, 'scrollIntoView');
  scrollIntoView.mockReset();
});

describe('ErrorSummary', () => {
  it('renders nothing with no errors', () => {
    const { container } = render(<ErrorSummary errors={[]} attempt={1} />);

    expect(container).toBeEmptyDOMElement();
    expect(document.title).toBe(pageTitle);
  });

  it('shows nothing, and leaves focus on the button, after a submit with no errors', () => {
    render(<InviteForm />);
    fireEvent.change(screen.getByRole('textbox', { name: 'Name' }), { target: { value: 'Sam Lee' } });
    fireEvent.click(screen.getByRole('radio', { name: 'Worker' }));

    pressSend();

    expect(screen.queryByRole('heading', { name: 'There is a problem' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send invite' })).toHaveFocus();
    expect(document.title).toBe(pageTitle);
  });

  it('takes focus on a failed submit, and again on a second with the same errors', () => {
    render(<InviteForm />);

    pressSend();
    expect(summary()).toHaveFocus();
    expect(summary()).toHaveAttribute('tabindex', '-1');

    pressSend();
    expect(summary()).toHaveFocus();
  });

  it("lists the errors in the order given, each a link in the words of its field's message", () => {
    render(<InviteForm />);

    pressSend();

    const links = within(summary()).getAllByRole('link');
    expect(links.map((link) => link.textContent)).toEqual([nameError, roleError]);
    expect(document.getElementById(`${nameId}-error`)).toHaveTextContent(`Error: ${nameError}`);
    expect(document.getElementById(`${roleId}-error`)).toHaveTextContent(`Error: ${roleError}`);
    expect(within(summary()).getByRole('list')).toBeInTheDocument();
    expect(links[0]).toHaveStyle({ display: 'block', minBlockSize: 'var(--target-min)' });
  });

  it('follows a link to a text field: scrolls its label into view, focuses it, and changes no URL', async () => {
    render(<InviteForm />);
    pressSend();
    const input = screen.getByRole('textbox', { name: 'Name' });
    const focus = vi.spyOn(input, 'focus');
    const { hash, href } = location;
    const entries = history.length;

    fireEvent.click(within(summary()).getByRole('link', { name: nameError }));
    await afterNavigation();

    expect(input).toHaveFocus();
    expect(focus).toHaveBeenCalledWith({ preventScroll: true });
    expect(scrollIntoView).toHaveBeenCalledOnce();
    expect(scrollIntoView.mock.contexts[0]).toBe(screen.getByText('Name', { selector: 'label' }));
    expect(scrollIntoView).toHaveBeenCalledWith({ block: 'start' });
    expect(location.hash).toBe(hash);
    expect(location.href).toBe(href);
    expect(history.length).toBe(entries);
  });

  it('follows a link to a group: scrolls its legend into view and focuses its first control', async () => {
    render(<InviteForm />);
    pressSend();
    const worker = screen.getByRole('radio', { name: 'Worker' });
    const focus = vi.spyOn(worker, 'focus');
    const { hash, href } = location;
    const entries = history.length;

    fireEvent.click(within(summary()).getByRole('link', { name: roleError }));
    await afterNavigation();

    expect(worker).toHaveFocus();
    expect(focus).toHaveBeenCalledWith({ preventScroll: true });
    expect(scrollIntoView).toHaveBeenCalledOnce();
    expect(scrollIntoView.mock.contexts[0]).toBe(screen.getByText('Role', { selector: 'legend' }));
    expect(location.hash).toBe(hash);
    expect(location.href).toBe(href);
    expect(history.length).toBe(entries);
  });

  it('lists an error that is in no field as its words with no link, among linked ones', () => {
    const signInFailed =
      'Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.';
    render(
      <ErrorSummary
        errors={[
          { fieldId: 'signin-email', message: 'Enter your email address' },
          { message: signInFailed },
          { fieldId: 'signin-password', message: 'Enter your password' },
        ]}
        attempt={1}
      />,
    );

    const items = within(summary()).getAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual([
      'Enter your email address',
      signInFailed,
      'Enter your password',
    ]);
    expect(within(items[0]).getByRole('link')).toHaveAttribute('href', '#signin-email');
    expect(within(items[1]).queryByRole('link')).not.toBeInTheDocument();
    expect(within(items[2]).getByRole('link')).toHaveAttribute('href', '#signin-password');
  });

  it('moves no focus when the errors change with no new attempt, as the person fixes them', () => {
    render(<InviteForm />);
    pressSend();
    const input = screen.getByRole('textbox', { name: 'Name' });
    input.focus();

    fireEvent.change(input, { target: { value: 'Sam Lee' } });
    expect(within(summary()).getAllByRole('link').map((link) => link.textContent)).toEqual([roleError]);
    expect(input).toHaveFocus();

    const worker = screen.getByRole('radio', { name: 'Worker' });
    worker.focus();
    fireEvent.click(worker);
    expect(screen.queryByRole('heading', { name: 'There is a problem' })).not.toBeInTheDocument();
    expect(worker).toHaveFocus();
  });

  it('moves no focus when errors that had cleared come back with no new attempt', () => {
    const errors = [{ fieldId: nameId, message: nameError }];
    const { rerender } = render(<ErrorSummary errors={errors} attempt={1} />);
    expect(summary()).toHaveFocus();
    rerender(<ErrorSummary errors={[]} attempt={1} />);

    rerender(<ErrorSummary errors={errors} attempt={1} />);
    expect(summary()).not.toHaveFocus();

    rerender(<ErrorSummary errors={errors} attempt={2} />);
    expect(summary()).toHaveFocus();
  });

  it('puts "Error: " in front of the page title while it shows errors, and takes it off when they clear', () => {
    render(<InviteForm />);

    pressSend();
    expect(document.title).toBe(`Error: ${pageTitle}`);

    pressSend();
    fireEvent.change(screen.getByRole('textbox', { name: 'Name' }), { target: { value: 'Sam Lee' } });
    expect(document.title).toBe(`Error: ${pageTitle}`);

    fireEvent.click(screen.getByRole('radio', { name: 'Manager' }));
    expect(document.title).toBe(pageTitle);
  });

  it('takes "Error: " off the page title when the form goes', () => {
    const { unmount } = render(<InviteForm />);
    pressSend();
    expect(document.title).toBe(`Error: ${pageTitle}`);

    unmount();

    expect(document.title).toBe(pageTitle);
  });
});
