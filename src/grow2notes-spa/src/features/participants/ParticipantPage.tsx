import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useId, useState, type FormEvent, type ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router';
import { ApiError, apiWithHeaders, classify, type FieldErrors } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { DateOfBirthField } from '../../components/DateOfBirthField.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { dobParts, dobPartsOf, parseDob, type DobPart, type DobParts } from '../../components/parseDob.ts';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { SaveStatus, type SaveState } from '../../components/SaveStatus.tsx';
import textInput from '../../components/textInput.module.css';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import { PageNotFound } from '../../routes/PageNotFound.tsx';
import styles from './ParticipantPage.module.css';
import { fullName, participantsQuery, type AdminParticipant } from './participantsQuery.ts';

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

const nothingTyped: Typed = { givenName: '', familyName: '', dateOfBirth: { day: '', month: '', year: '' } };

/** A participant's details as the fields show them, the date of birth as a person types it. */
function typedOf({ givenName, familyName, dateOfBirth }: AdminParticipant): Typed {
  return { givenName, familyName, dateOfBirth: dobPartsOf(dateOfBirth) };
}

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
 * The participant as the server last confirmed them: their ID, their name as saved, which the `<h1>` shows, so that it
 * changes only once a save is done, never as the name is typed (text-and-date-inputs.md), and their version, which the
 * next save sends as `If-Match`.
 */
type Known = {
  id: string;
  name: string;
  /**
   * The version that the page loaded them at, then the one that the list gives after each save and each refusal as
   * stale, never one that a refetch brings in between, which would let a save overwrite a change that the page has not
   * shown; `undefined` while the page has read none, for a participant that it has just added, or when its read after
   * a save did not load, so that the next save reads the list first.
   */
  version: string | undefined;
};

/** A correction of a participant's Details, and the version it is sent with. */
type Correction = { id: string; details: Details; version: string | undefined };

/**
 * What a correction came to: saved, at its answer's `Date`, or refused as stale (`412`); and the participant's version,
 * read afresh from the list, or `undefined` when that read did not load.
 */
type Corrected = { stale: boolean; savedAtUtc: string | undefined; version: string | undefined };

/**
 * The participant's version as the list gives it now (design.md §6.6: a save answers with none), from a read sent now:
 * a read already under way began before the save's answer, so it is called off first, rather than joined. It fails as
 * the list's request fails, and is `undefined` when the list does not hold them.
 */
async function listedVersion(queryClient: QueryClient, id: string): Promise<string | undefined> {
  await queryClient.cancelQueries({ queryKey: participantsQuery.queryKey });
  const participants = await queryClient.fetchQuery(participantsQuery);
  return participants.find((participant) => participant.id === id)?.rowVersion;
}

/**
 * The one route component of `/manage/participants/new` and `/manage/participants/{participantId}` (participants.md
 * component B): Add participant, which the `201`, as it replaces the address with the new participant's, turns into
 * their page without a remount, its fields as typed and focus on Save; or the page of a participant opened from the
 * list or an address, which loads them.
 */
export function ParticipantPage() {
  const { participantId } = useParams();
  // The participant that this page has added, at whose address it stays the same page.
  const [addedId, setAddedId] = useState<string>();

  if (participantId === undefined || participantId === addedId) {
    return (
      <ParticipantFrame>
        <Details onAdded={setAddedId} />
      </ParticipantFrame>
    );
  }

  return <ListedParticipant key={participantId} participantId={participantId} />;
}

/** The page around the Details: the back link to the list, in the shell's bar, and the page's status region. */
function ParticipantFrame({ children }: { children: ReactNode }) {
  return (
    <PageStatus>
      <PageBar>
        <BackLink to="/manage/participants">{pageName.participants}</BackLink>
      </PageBar>
      {children}
    </PageStatus>
  );
}

/**
 * A participant's page opened at their address (participants.md B9): the back link alone while the list loads, as the
 * `<h1>` is their name, then "Loading participant…" after 1 s; then their Details, copied from the list, whose `<h1>`
 * takes the focus left pending for it; or the fallback `<h1>` "Participant" with the failure sentence and Try again;
 * or, for an ID not on the list, Page not found in place of the whole page.
 */
function ListedParticipant({ participantId }: { participantId: string }) {
  const participants = useQuery(participantsQuery);
  // Found in the list rather than picked out by a `select`, whose `undefined` for an ID not on it the region would
  // take for a list not loaded yet.
  const participant = participants.data?.find(({ id }) => id === participantId);

  if (participants.data !== undefined && participant === undefined) {
    return <PageNotFound />;
  }

  return (
    <ParticipantFrame>
      <LoadRegion
        queries={[participants]}
        loading={strings.participants.participantLoading}
        what={strings.participants.thisParticipant}
        fallbackHeading={pageName.participant}
      >
        {/* Called only once the query has data, which then holds the participant. */}
        {() => <Details participant={participant!} />}
      </LoadRegion>
    </ParticipantFrame>
  );
}

/**
 * A participant's page, with the participant as the list gave them as it opened, whose details the fields start
 * with, copied once; or Add participant, told the new participant's ID once Save has added them.
 */
type DetailsProps =
  | { participant: AdminParticipant; onAdded?: never }
  | { participant?: never; onAdded: (id: string) => void };

/**
 * Add participant or a participant's Details (participants.md B2, B4, B5): the error summary after a failed Save, the
 * `<h1>`, then Details, a form of the given and family names and the date of birth, with no `name` on any field, so
 * that nothing it holds can reach an address, and Save, with the alert above it for a request that failed and the save
 * status beside it. Nothing is checked before Save, which checks every field in page order and sends nothing while one
 * fails; once a field shows a message, each edit checks it again. A `201` shows "Saved 4:12 pm" from its `Date`, makes
 * the name the `<h1>` and replaces the address with the participant's, in the screen. A save of a participant who
 * exists is a `PUT` with their version as `If-Match`, whose `200` shows "Saved 4:12 pm" and the saved name as the
 * `<h1>`, and whose `412` says that someone else changed the details, the fields kept as typed. Focus stays on Save.
 */
function Details({ participant, onAdded }: DetailsProps) {
  const me = useMe();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const headingId = useId();
  // As typed, in this page's memory only (D22), copied once from the participant, so that no refetch overwrites them.
  const [typed, setTyped] = useState<Typed>(() => (participant === undefined ? nothingTyped : typedOf(participant)));
  const [known, setKnown] = useState<Known | undefined>(() =>
    participant === undefined
      ? undefined
      : { id: participant.id, name: fullName(participant), version: participant.rowVersion },
  );
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
  // Sent once, as the query client's mutations are: one sent again after a lost answer would meet its own change as a
  // `412`. The list is read again after the answer, for the next version, as no save answers with one, and for the
  // list screen; inside the save, so that Save stays busy until the next version is known.
  const correct = useMutation({
    mutationFn: async ({ id, details, version }: Correction): Promise<Corrected> => {
      const ifMatch = version ?? (await listedVersion(queryClient, id));
      const reread = () => listedVersion(queryClient, id).catch(() => undefined);
      try {
        const { dateUtc } = await apiWithHeaders<undefined>(`${participantsPath}/${id}`, {
          method: 'PUT',
          body: details,
          ifMatch,
        });
        return { stale: false, savedAtUtc: dateUtc, version: await reread() };
      } catch (error) {
        if (!(error instanceof ApiError && error.status === 412)) {
          throw error;
        }

        return { stale: true, savedAtUtc: undefined, version: await reread() };
      }
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

  // A request's failure: a `422`'s fields on the fields, as Save's own checks show them; nothing for a `401`, which
  // the session flow has, as `api()` started it; else "Not saved: …" in the alert, focus staying on Save.
  function refused(error: Error) {
    const fields =
      error instanceof ApiError && error.status === 422 && error.errors !== undefined
        ? serverErrors(error.errors)
        : undefined;
    if (fields !== undefined) {
      setErrors(fields);
      setAttempt((presses) => presses + 1);
      return;
    }

    const cause = classify(error);
    if (cause !== 'signedOut') {
      setFailure(msg.notSaved(cause));
    }
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

    const details = checked.details;
    if (known !== undefined) {
      correct.mutate(
        { id: known.id, details, version: known.version },
        {
          onSuccess: ({ stale, savedAtUtc, version }) => {
            if (stale) {
              setKnown({ ...known, version });
              setFailure(strings.participantDetails.changedMeanwhile);
              return;
            }

            setKnown({ id: known.id, name: fullName(details), version });
            setSaved({ savedAtUtc });
          },
          onError: refused,
        },
      );
      return;
    }

    add.mutate(details, {
      onSuccess: ({ body, dateUtc }) => {
        setKnown({ id: body.id, name: fullName(details), version: undefined });
        onAdded?.(body.id);
        setSaved({ savedAtUtc: dateUtc });
        // Replaced, so that Back goes to the list, not to an empty form, and in the screen, so that the shell moves
        // neither focus nor scroll (app-shell.md *The route-change rule (one owner)*).
        void navigate(`/manage/participants/${body.id}`, { replace: true, state: { inScreen: true } });
      },
      onError: refused,
    });
  }

  return (
    <>
      <ErrorSummary errors={summaryItems(errors)} attempt={attempt} />
      <PageHeading>{known === undefined ? pageName.addParticipant : known.name}</PageHeading>
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
            <Button
              type="submit"
              variant="primary"
              busy={add.isPending || correct.isPending}
              busyLabel={strings.saving}
            >
              {strings.save}
            </Button>
            <SaveStatus state={saved} />
          </div>
        </form>
      </section>
    </>
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
