import type { Role } from '../../api/me.ts';
import { ChoiceRow } from '../../components/ChoiceRow.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { strings } from '../../copy/strings.ts';

// In the order they show: the least access first (users.md component B item 6).
const roles: readonly Role[] = ['Worker', 'Manager'];

// The group's id, which its radios' ids, its message's and the API's key for the field start with.
const fieldId = 'role';

const radioId = (role: Role) => `${fieldId}-${role.toLowerCase()}`;

/** The radio that the error summary's link for the role goes to: the first, Worker's, as GOV.UK's summary links. */
export const firstRoleRadioId = `${fieldId}-worker`;

type RoleFieldProps = {
  value: Role;
  onChange: (role: Role) => void;
  /** The role's message once the server has refused it; never the form's own, as a role is always chosen. */
  error?: string;
};

/**
 * The Role radios of Invite user and Edit user (users.md component B item 6): a `<fieldset>` whose legend is "Role",
 * with its message under the legend, and Worker then Manager, each with the hint that says what the role can do. One
 * Tab stop, in which the arrow keys move and choose, as the browser's own radios do.
 */
export function RoleField({ value, onChange, error }: RoleFieldProps) {
  return (
    <FieldGroup fieldId={fieldId} label={strings.users.role} group error={error}>
      {roles.map((role) => (
        <ChoiceRow
          key={role}
          id={radioId(role)}
          name={fieldId}
          label={strings.role[role]}
          hint={strings.users.roleHint[role]}
          checked={value === role}
          onChange={() => onChange(role)}
        />
      ))}
    </FieldGroup>
  );
}
