import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import textInput from '../../components/textInput.module.css';
import { strings } from '../../copy/strings.ts';
import { firstRoleRadioId, RoleField } from './RoleField.tsx';
import styles from './UserFields.module.css';
import type { UserErrors, UserValues } from './userRules.ts';

// Each field's id is its key in the API's JSON, so that a 422's errors{} name it (users.md component B).
const nameId = 'displayName';
const emailId = 'email';

/** A field's `aria-describedby`: its hint, then its message while it shows one (form-validation.md *Anatomy*). */
function describedBy(fieldId: string, error: string | undefined): string {
  return error === undefined ? `${fieldId}-hint` : `${fieldId}-hint ${fieldId}-error`;
}

type UserErrorSummaryProps = {
  errors: UserErrors;
  /** What the summary lists first, with no link, when the server named a field that the form does not have. */
  unknownField?: string;
  /** The number of failed presses, so that the summary takes focus on each, even with the same words. */
  attempt: number;
};

/**
 * The error summary of Invite user and Edit user (users.md components B and D item 2): a field the server named that
 * the form does not have, with no link, then each field's message, linked to its field, in the fields' order, the
 * role's to its first radio.
 */
export function UserErrorSummary({ errors, unknownField, attempt }: UserErrorSummaryProps) {
  const items: FormError[] = unknownField === undefined ? [] : [{ message: unknownField }];
  for (const [message, fieldId] of [
    [errors.displayName, nameId],
    [errors.email, emailId],
    [errors.role, firstRoleRadioId],
  ] as const) {
    if (message !== undefined) {
      items.push({ message, fieldId });
    }
  }

  return <ErrorSummary errors={items} attempt={attempt} />;
}

type UserFieldsProps = {
  values: UserValues;
  errors: UserErrors;
  /** Email address's hint, which says what a new address brings: a setup link, and on Edit user a reset. */
  emailHint: string;
  /** Called with the values as a change of one field leaves them, and that field. */
  onChange: (next: UserValues, field: keyof UserValues) => void;
};

/**
 * The fields of Invite user and Edit user (users.md component B items 4 to 6, and component D item 5): Name, Email
 * address and the Role radios, each with its hint, and its message above it while it shows one. Neither text field
 * has a `maxLength`, which would cut a longer name pasted in without a word, or a `name`, so that nothing it holds can
 * reach an address; the press checks them.
 */
export function UserFields({ values, errors, emailHint, onChange }: UserFieldsProps) {
  return (
    <div className={styles.fields}>
      <FieldGroup fieldId={nameId} label={strings.name} hint={strings.users.nameHint} error={errors.displayName}>
        <input
          id={nameId}
          type="text"
          className={textInput.input}
          // Off on both fields, which describe another person, so that the manager's own details are not offered (SC
          // 1.3.5 covers information about the person filling the form only).
          autoComplete="off"
          spellCheck={false}
          autoCapitalize="words"
          aria-describedby={describedBy(nameId, errors.displayName)}
          aria-invalid={errors.displayName === undefined ? undefined : true}
          value={values.displayName}
          onChange={(event) => onChange({ ...values, displayName: event.currentTarget.value }, 'displayName')}
        />
      </FieldGroup>
      <FieldGroup fieldId={emailId} label={strings.emailAddress} hint={emailHint} error={errors.email}>
        <input
          id={emailId}
          type="email"
          className={textInput.input}
          autoComplete="off"
          spellCheck={false}
          autoCapitalize="none"
          autoCorrect="off"
          aria-describedby={describedBy(emailId, errors.email)}
          aria-invalid={errors.email === undefined ? undefined : true}
          value={values.email}
          onChange={(event) => onChange({ ...values, email: event.currentTarget.value }, 'email')}
        />
      </FieldGroup>
      <RoleField value={values.role} error={errors.role} onChange={(role) => onChange({ ...values, role }, 'role')} />
    </div>
  );
}
