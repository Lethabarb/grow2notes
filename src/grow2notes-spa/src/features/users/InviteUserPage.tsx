import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { api, classify } from '../../api/api.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import textInput from '../../components/textInput.module.css';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import styles from './InviteUserPage.module.css';
import { firstRoleRadioId, RoleField } from './RoleField.tsx';
import { userRefusal, validateUser, type UserErrors, type UserValues } from './userRules.ts';
import { usersQuery } from './usersQuery.ts';

// Each field's id is its key in the API's JSON, so that a 422's errors{} name it (users.md component B).
const nameId = 'displayName';
const emailId = 'email';

/** A field's `aria-describedby`: its hint, then its message while it shows one (form-validation.md *Anatomy*). */
function describedBy(fieldId: string, error: string | undefined): string {
  return error === undefined ? `${fieldId}-hint` : `${fieldId}-hint ${fieldId}-error`;
}

/**
 * The summary's items: a field the server named that the form does not have, with no link, then each field's message,
 * linked to its field, in the fields' order, the role's to its first radio.
 */
function summaryItems(errors: UserErrors, unknownField: boolean): FormError[] {
  const items: FormError[] = unknownField ? [{ message: msg.notSent('server') }] : [];
  for (const [message, fieldId] of [
    [errors.displayName, nameId],
    [errors.email, emailId],
    [errors.role, firstRoleRadioId],
  ] as const) {
    if (message !== undefined) {
      items.push({ message, fieldId });
    }
  }

  return items;
}

/**
 * Invite user at `/manage/users/new` (users.md component B, *States* › Invite user and *Interactions*): the back link
 * to Users, then the error summary, the `<h1>`, Name, Email address, the Role radios with Worker chosen (D77), and Send
 * invite under its alert line. Send invite checks the fields first and sends nothing while one is at fault; then it
 * sends them, trimmed, once, however often it is pressed while busy. The server's refusal of a field shows as the
 * form's own checks do, and a failed request in the alert line, with focus left on the button. The typed values are
 * never cleared, and are kept in this page's memory alone, never in the address, the history's state or storage
 * (AC3).
 */
export function InviteUserPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [values, setValues] = useState<UserValues>({ displayName: '', email: '', role: 'Worker' });
  const [errors, setErrors] = useState<UserErrors>({});
  const [unknownField, setUnknownField] = useState(false);
  // The number of failed presses, so that the summary takes focus on each, even with the same words.
  const [attempt, setAttempt] = useState(0);
  // Set in the press itself, rather than read from the mutation, whose state TanStack hands on only after a task, by
  // when a second tap of a double tap would have sent the invite again.
  const [sending, setSending] = useState(false);
  const [failure, setFailure] = useState<string>();

  const invite = useMutation({
    mutationFn: async (body: UserValues) => {
      await api('/api/admin/users', { method: 'POST', body });
      // Awaited, so that the list arrives holding the new row (users.md *E. Data and helpers*, Mutations).
      await queryClient.refetchQueries({ queryKey: usersQuery.queryKey });
    },
  });

  function change<K extends keyof UserValues>(field: K, value: UserValues[K]) {
    const next = { ...values, [field]: value };
    setValues(next);
    // Once a field shows a message, each change checks that field again, and the message goes as soon as it passes
    // (form-validation.md *Fixing*); a field with no message is checked on the next press only.
    if (errors[field] !== undefined) {
      setErrors((shown) => ({ ...shown, [field]: validateUser(next)[field] }));
    }
  }

  // Send invite, pressed or reached by Enter in a field. While busy, the button ignores both.
  function send(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied, so that a failure that repeats is written into an empty region and announced again.
    setFailure(undefined);
    setUnknownField(false);
    const found = validateUser(values);
    setErrors(found);
    if (Object.keys(found).length > 0) {
      setAttempt((count) => count + 1);
      return;
    }

    const body: UserValues = { displayName: values.displayName.trim(), email: values.email.trim(), role: values.role };
    setSending(true);
    // TanStack calls these only while the page is still shown, so that an invite that ends after the manager has
    // gone elsewhere takes them nowhere.
    invite.mutate(body, {
      // The list in the form's place, so that Back does not return to the form (users.md *Interactions*), whose <h1>
      // the shell's route-change rule focuses, as on every new screen.
      onSuccess: () => void navigate('/manage/users', { replace: true }),
      onError: (error) => {
        setSending(false);
        const refusal = userRefusal(error, body);
        if (refusal !== undefined) {
          setErrors(refusal.errors);
          setUnknownField(refusal.unknownField);
          setAttempt((count) => count + 1);
          return;
        }

        // Nothing for a 401, which the session flow has.
        const cause = classify(error);
        if (cause !== 'signedOut') {
          setFailure(msg.notSent(cause));
        }
      },
    });
  }

  return (
    <PageStatus>
      <PageBar>
        <BackLink to="/manage/users">{pageName.users}</BackLink>
      </PageBar>
      <div className={styles.view}>
        <ErrorSummary errors={summaryItems(errors, unknownField)} attempt={attempt} />
        <PageHeading>{pageName.inviteUser}</PageHeading>
        <form noValidate onSubmit={send}>
          <div className={styles.fields}>
            {/* No maxLength, which would cut a longer name pasted in without a word; the press checks it. */}
            <FieldGroup fieldId={nameId} label={strings.name} hint={strings.users.nameHint} error={errors.displayName}>
              <input
                id={nameId}
                type="text"
                className={textInput.input}
                // Off on both fields, which describe another person, so that the manager's own details are not
                // offered (SC 1.3.5 covers information about the person filling the form only).
                autoComplete="off"
                spellCheck={false}
                autoCapitalize="words"
                aria-describedby={describedBy(nameId, errors.displayName)}
                aria-invalid={errors.displayName === undefined ? undefined : true}
                value={values.displayName}
                onChange={(event) => change('displayName', event.currentTarget.value)}
              />
            </FieldGroup>
            <FieldGroup
              fieldId={emailId}
              label={strings.emailAddress}
              hint={strings.users.inviteEmailHint}
              error={errors.email}
            >
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
                onChange={(event) => change('email', event.currentTarget.value)}
              />
            </FieldGroup>
            <RoleField value={values.role} error={errors.role} onChange={(role) => change('role', role)} />
          </div>
          <RequestAlert>{failure}</RequestAlert>
          <Button type="submit" variant="primary" busy={sending} busyLabel={strings.sending}>
            {strings.users.sendInvite}
          </Button>
        </form>
      </div>
    </PageStatus>
  );
}
