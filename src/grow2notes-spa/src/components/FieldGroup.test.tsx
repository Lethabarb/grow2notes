import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FieldGroup } from './FieldGroup.tsx';

const nameError = "Enter the person's name";
const roleError = 'Choose Worker or Manager';

/** A text field, as a field component builds one: its control names the hint and the message, and is invalid. */
function NameField({ error }: { error?: string }) {
  return (
    <FieldGroup fieldId="invite-name" label="Name" hint="As they like to be called" error={error}>
      <input
        id="invite-name"
        type="text"
        aria-describedby={error === undefined ? 'invite-name-hint' : 'invite-name-hint invite-name-error'}
        aria-invalid={error === undefined ? undefined : true}
      />
    </FieldGroup>
  );
}

/** A set of radios, a group, whose fieldset `FieldGroup` describes itself. */
function RoleField({ error, hint }: { error?: string; hint?: string }) {
  return (
    <FieldGroup fieldId="invite-role" label="Role" group hint={hint} error={error}>
      <div>
        <input type="radio" id="invite-role-worker" name="role" />
        <label htmlFor="invite-role-worker">Worker</label>
      </div>
      <div>
        <input type="radio" id="invite-role-manager" name="role" />
        <label htmlFor="invite-role-manager">Manager</label>
      </div>
    </FieldGroup>
  );
}

/** The element `FieldGroup` renders around a field, which carries `data-invalid`. */
function groupOf(element: HTMLElement) {
  const group = element.closest('fieldset')?.parentElement ?? element.parentElement;
  if (group === null) {
    throw new Error('The element is in no group.');
  }

  return group;
}

describe('FieldGroup', () => {
  it("puts a field's label, hint, message and control in that order, with the hint's and message's ids", () => {
    render(<NameField error={nameError} />);
    const input = screen.getByRole('textbox', { name: 'Name' });
    const hint = screen.getByText('As they like to be called');
    const message = screen.getByText(nameError);

    expect(Array.from(groupOf(input).children)).toEqual([
      screen.getByText('Name', { selector: 'label' }),
      hint,
      message,
      input,
    ]);
    expect(hint).toHaveAttribute('id', 'invite-name-hint');
    expect(message).toHaveAttribute('id', 'invite-name-error');
    expect(input).toHaveAccessibleDescription(`As they like to be called Error: ${nameError}`);
  });

  it("puts a group's legend, hint, message and controls in that order in a fieldset that they describe", () => {
    render(<RoleField hint="What they can see and change" error={roleError} />);
    const fieldset = screen.getByRole('group', { name: 'Role' });
    const hint = screen.getByText('What they can see and change');
    const message = screen.getByText(roleError);
    const [worker, manager] = screen.getAllByRole('radio');

    expect(Array.from(fieldset.children)).toEqual([
      screen.getByText('Role', { selector: 'legend' }),
      hint,
      message,
      worker.parentElement,
      manager.parentElement,
    ]);
    expect(hint).toHaveAttribute('id', 'invite-role-hint');
    expect(message).toHaveAttribute('id', 'invite-role-error');
    expect(fieldset).toHaveAttribute('aria-describedby', 'invite-role-hint invite-role-error');
  });

  it("describes a group's fieldset by only the parts it has", () => {
    const { rerender } = render(<RoleField />);
    const fieldset = screen.getByRole('group', { name: 'Role' });
    expect(fieldset).not.toHaveAttribute('aria-describedby');

    rerender(<RoleField error={roleError} />);
    expect(fieldset).toHaveAttribute('aria-describedby', 'invite-role-error');
  });

  it('writes the message in bold, after a hidden "Error: "', () => {
    render(<NameField error={nameError} />);
    const message = screen.getByText(nameError);

    expect(message).toHaveTextContent(`Error: ${nameError}`, { normalizeWhitespace: false });
    expect(screen.getByText('Error:')).toHaveClass('visually-hidden');
    expect(message).toHaveStyle({ fontWeight: '700', color: 'var(--colour-error)' });
  });

  it('is marked invalid, and shows its message, only while it holds an error', () => {
    const { rerender } = render(
      <>
        <NameField />
        <RoleField />
      </>,
    );
    const nameGroup = groupOf(screen.getByRole('textbox'));
    const roleGroup = groupOf(screen.getByRole('group'));

    expect(nameGroup).not.toHaveAttribute('data-invalid');
    expect(roleGroup).not.toHaveAttribute('data-invalid');
    expect(screen.queryByText(/Error:/)).not.toBeInTheDocument();

    rerender(
      <>
        <NameField error={nameError} />
        <RoleField error={roleError} />
      </>,
    );
    expect(nameGroup).toHaveAttribute('data-invalid');
    expect(roleGroup).toHaveAttribute('data-invalid');
    expect(nameGroup).toHaveStyle({ borderInlineStart: '4px solid var(--colour-error)' });

    rerender(
      <>
        <NameField />
        <RoleField error={roleError} />
      </>,
    );
    expect(nameGroup).not.toHaveAttribute('data-invalid');
    expect(document.getElementById('invite-name-error')).toBeNull();
    expect(roleGroup).toHaveAttribute('data-invalid');
  });
});
