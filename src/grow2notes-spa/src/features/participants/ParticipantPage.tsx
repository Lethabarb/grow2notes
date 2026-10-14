import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useId, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { ApiError, apiWithHeaders, classify, type FieldErrors } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { DateOfBirthField } from '../../components/DateOfBirthField.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { dobParts, parseDob, type DobPart, type DobParts } from '../../components/parseDob.ts';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { SaveStatus, type SaveState } from '../../components/SaveStatus.tsx';
import textInput from '../../components/textInput.module.css';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import { PageNotFound } from '../../routes/PageNotFound.tsx';
import styles from './ParticipantPage.module.css';
import { fullName, participantsQuery } from './participantsQuery.ts';

const participantsPath = '/api/admin/participants';

// The fields' ids (text-and-date-inputs.md), which the error summary's links go to; the date's parts add `-day`,
// `-month` and `-year`.
const ids = {
  givenName: 'details-givenName',
  familyName: 'details-familyName',
  dateOfBirth: 'details-dateOfBirth',
} as const;

/** The Details as typed. */
type Typed = { givenName: string; familyName: string; dateOfBirth: DobParts };

/** The Details as the API takes them: the names with their ends trimmed, and the date, written `1987-03-07`. */
type Details = { givenName: string; familyName: string; dateOfBirth: string };

/** The message of each field in error, and, for the date, the parts that it marks invalid. */
type Errors = {
  givenName?: string;
  familyName?: string;
  dateOfBirth?: { message: string; parts: readonly DobPart[] };
};

/** The participant that the page has added, whose page it then is. */
type Added = { id: string; name: string };

const nothingTyped: Typed = { givenName: '', familyName: '', dateOfBirth: { day: '', month: '', year: '' } };

/**
 * A name's message, or nothing: none typed once its ends are trimmed, or more than `limits.participantName`
 * characters, counted in UTF-16 units, as the server counts them.
 */
function nameError(name: string, label: string, enter: string): string | undefined {
  const trimmed = name.trim();
  if (trimmed === '') {
    return enter;
  }

  return trimmed.length > limits.participantName ? msg.tooLong(label, limits.participantName) : undefined;
}

const givenNameError = (name: string) =>
  nameError(name, strings.participantDetails.givenName, strings.participantDetails.enterGivenName);

const familyNameError = (name: string) =>
  nameError(name, strings.participantDetails.familyName, strings.participantDetails.enterFamilyName);

function dateOfBirthError(parts: DobParts, today: string): Errors['dateOfBirth'] {
  const result = parseDob(parts, today);
  return result.ok ? undefined : { message: result.message, parts: result.parts };
}

/**
 * The Details' checks on Save, in page order (participants.md *Details validation messages*), each field's first that
 * fails, and the Details to send when none does.
 */
function check(typed: Typed, today: string): { errors: Errors; details?: Details } {
  const dateOfBirth = parseDob(typed.dateOfBirth, today);
  const errors: Errors = {
    givenName: givenNameError(typed.givenName),
    familyName: familyNameError(typed.familyName),
    dateOfBirth: dateOfBirth.ok ? undefined : { message: dateOfBirth.message, parts: dateOfBirth.parts },
  };
  if (!dateOfBirth.ok || errors.givenName !== undefined || errors.familyName !== undefined) {
    return { errors };
  }

  return {
    errors,
    details: { givenName: typed.givenName.trim(), familyName: typed.familyName.trim(), dateOfBirth: dateOfBirth.iso },
  };
}

/**
 * A `422`'s field errors on the Details' fields (form-validation.md *Server responses*), or `undefined` when one names
 * no field here, which is no fault of the person's. The server checks what Save checks, and Save sends only what
 * passed, the names trimmed already, so a name that the server refuses is one that its own trimming leaves empty, and
 * a date one that is no longer before today, as Melbourne's day has changed since the page read `me.today`.
 */
function serverErrors(fields: FieldErrors): Errors | undefined {
  const names = Object.keys(fields);
  if (names.length === 0) {
    return undefined;
  }

  const errors: Errors = {};
  for (const name of names) {
    if (name === 'givenName') {
      errors.givenName = strings.participantDetails.enterGivenName;
    } else if (name === 'familyName') {
      errors.familyName = strings.participantDetails.enterFamilyName;
    } else if (name === 'dateOfBirth') {
      errors.dateOfBirth = { message: strings.dateOfBirth.inThePast, parts: dobParts };
    } else {
      return undefined;
    }
  }

  return errors;
}

/** The error summary's items, in page order, the date's linked to its first part marked invalid. */
function summaryItems({ givenName, familyName, dateOfBirth }: Errors): FormError[] {
  const items: FormError[] = [];
  if (givenName !== undefined) {
    items.push({ message: givenName, fieldId: ids.givenName });
  }

  if (familyName !== undefined) {
    items.push({ message: familyName, fieldId: ids.familyName });
  }

  if (dateOfBirth !== undefined) {
    items.push({ message: dateOfBirth.message, fieldId: `${ids.dateOfBirth}-${dateOfBirth.parts[0]}` });
  }

  return items;
}

/**
 * The one route component of `/manage/participants/new` and `/manage/participants/{participantId}` (participants.md
 * component B), so that the `201` of Add participant, which replaces the address with the new participant's, turns
 * this page into theirs without a remount, its fields as typed and focus on Save.
 */
export function ParticipantPage() {
  const { participantId } = useParams();
  const [added, setAdded] = useState<Added>();

  // A participant's page opened from the list or an address is S01.02.03's; until then, only the participant that
  // this page has just added has one.
  if (participantId !== undefined && participantId !== added?.id) {
    return <PageNotFound />;
  }

  return <DetailsPage added={added} onAdded={setAdded} />;
}

type DetailsPageProps = {
  /** The participant once Save has added them, whose name is then the page's `<h1>`. */
  added: Added | undefined;
  onAdded: (added: Added) => void;
};

/**
 * Add participant (participants.md B1, B2, B4, B5, B8): the back link to the list, the error summary after a failed
 * Save, the `<h1>`, then Details, a form of the given and family names and the date of birth, with no `name` on any
 * field, so that nothing it holds can reach an address, and Save, with the alert above it for a request that failed and
 * the save status beside it. Nothing is checked before Save, which checks every field in page order and sends nothing
 * while one fails; once a field shows a message, each edit checks it again. A `201` shows "Saved 4:12 pm" from its
 * `Date`, makes the name the `<h1>` and replaces the address with the participant's, in the screen.
 */
function DetailsPage({ added, onAdded }: DetailsPageProps) {
  const me = useMe();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const headingId = useId();
  // As typed, in this page's memory only (D22).
  const [typed, setTyped] = useState<Typed>(nothingTyped);
  const [errors, setErrors] = useState<Errors>({});
  // The presses that found a field in error, so that each moves focus to the summary, even with the same messages.
  const [attempt, setAttempt] = useState(0);
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const [saved, setSaved] = useState<SaveState>('none');
  const add = useMutation({
    mutationFn: (details: Details) =>
      apiWithHeaders<{ id: string }>(participantsPath, { method: 'POST', body: details }),
    // The add has no idempotency key (design.md §6.6), so one sent again after its answer was lost could add the
    // participant twice: the person presses Save again instead.
    retry: 0,
    // Even once the page has gone, so that the list shows them.
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: participantsQuery.queryKey });
    },
  });

  // Each edit empties the save status, so that it never says "Saved" over changes that are not, and checks its own
  // field again once that shows a message, which goes as soon as the field passes (form-validation.md, rule 2).
  function edited(next: Typed, recheck: (shown: Errors) => Errors) {
    setTyped(next);
    setSaved('none');
    setErrors(recheck);
  }

  function editGivenName(givenName: string) {
    edited({ ...typed, givenName }, (shown) =>
      shown.givenName === undefined ? shown : { ...shown, givenName: givenNameError(givenName) },
    );
  }

  function editFamilyName(familyName: string) {
    edited({ ...typed, familyName }, (shown) =>
      shown.familyName === undefined ? shown : { ...shown, familyName: familyNameError(familyName) },
    );
  }

  function editDateOfBirth(dateOfBirth: DobParts) {
    edited({ ...typed, dateOfBirth }, (shown) =>
      shown.dateOfBirth === undefined ? shown : { ...shown, dateOfBirth: dateOfBirthError(dateOfBirth, me.today) },
    );
  }

  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied at each press, so that what the press brings is written into empty regions and announced.
    setFailure(undefined);
    setSaved('none');
    const checked = check(typed, me.today);
    setErrors(checked.errors);
    if (checked.details === undefined) {
      setAttempt((presses) => presses + 1);
      return;
    }

    // Saving the Details of a participant who exists is S01.02.03's, a PUT with If-Match.
    if (added !== undefined) {
      return;
    }

    const details = checked.details;
    add.mutate(details, {
      onSuccess: ({ body, dateUtc }) => {
        onAdded({ id: body.id, name: fullName(details) });
        setSaved({ savedAtUtc: dateUtc });
        // Replaced, so that Back goes to the list, not to an empty form, and in the screen, so that the shell moves
        // neither focus nor scroll (app-shell.md *The route-change rule (one owner)*).
        void navigate(`/manage/participants/${body.id}`, { replace: true, state: { inScreen: true } });
      },
      onError: (error) => {
        const fields =
          error instanceof ApiError && error.status === 422 && error.errors !== undefined
            ? serverErrors(error.errors)
            : undefined;
        if (fields !== undefined) {
          setErrors(fields);
          setAttempt((presses) => presses + 1);
          return;
        }

        // A 401 has the session flow, which `api()` started, and shows nothing here.
        const cause = classify(error);
        if (cause !== 'signedOut') {
          setFailure(msg.notSaved(cause));
        }
      },
    });
  }

  return (
    <PageStatus>
      <PageBar>
        <BackLink to="/manage/participants">{pageName.participants}</BackLink>
      </PageBar>
      <ErrorSummary errors={summaryItems(errors)} attempt={attempt} />
      <PageHeading>{added === undefined ? pageName.addParticipant : added.name}</PageHeading>
      <section aria-labelledby={headingId} className={styles.details}>
        <h2 id={headingId}>{strings.participantDetails.heading}</h2>
        <form noValidate onSubmit={save}>
          <div className={styles.fields}>
            <NameField
              id={ids.givenName}
              label={strings.participantDetails.givenName}
              error={errors.givenName}
              value={typed.givenName}
              onChange={editGivenName}
            />
            <NameField
              id={ids.familyName}
              label={strings.participantDetails.familyName}
              error={errors.familyName}
              value={typed.familyName}
              onChange={editFamilyName}
            />
            <DateOfBirthField
              id={ids.dateOfBirth}
              value={typed.dateOfBirth}
              onChange={editDateOfBirth}
              error={errors.dateOfBirth?.message}
              invalid={errors.dateOfBirth?.parts ?? []}
            />
          </div>
          <RequestAlert>{failure}</RequestAlert>
          <div className={styles.save}>
            <Button type="submit" variant="primary" busy={add.isPending} busyLabel={strings.saving}>
              {strings.save}
            </Button>
            <SaveStatus state={saved} />
          </div>
        </form>
      </section>
    </PageStatus>
  );
}

type NameFieldProps = {
  id: string;
  label: string;
  error: string | undefined;
  value: string;
  onChange: (value: string) => void;
};

/**
 * A name's field (text-and-date-inputs.md): a text field that takes every character and changes none, with no
 * `name`, `maxLength` or `pattern`. As someone else's name, the browser offers nothing from its autofill and keeps
 * nothing (D21, D22), and corrects and marks nothing as misspelt; a phone's keyboard starts each word with a capital.
 */
function NameField({ id, label, error, value, onChange }: NameFieldProps) {
  return (
    <FieldGroup fieldId={id} label={label} error={error}>
      <input
        id={id}
        type="text"
        className={`${textInput.input} ${styles.name}`}
        autoComplete="off"
        autoCorrect="off"
        spellCheck={false}
        autoCapitalize="words"
        aria-describedby={error === undefined ? undefined : `${id}-error`}
        aria-invalid={error === undefined ? undefined : true}
        value={value}
        onChange={(event) => onChange(event.currentTarget.value)}
      />
    </FieldGroup>
  );
}
