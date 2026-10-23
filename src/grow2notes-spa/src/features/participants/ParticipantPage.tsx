import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useId, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, ApiError, apiWithHeaders, classify, type FieldErrors, type WithHeaders } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { buttonClass } from '../../components/buttonClass.ts';
import { ButtonGroup } from '../../components/ButtonGroup.tsx';
import { DateOfBirthField } from '../../components/DateOfBirthField.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { focusPageHeading } from '../../components/focusPageHeading.ts';
import { ListEditor } from '../../components/ListEditor.tsx';
import { ListConflict, ListFrame } from '../../components/ListFrame.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { Notice } from '../../components/Notice.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { dobParts, dobPartsOf, parseDob, type DobPart, type DobParts } from '../../components/parseDob.ts';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { SaveStatus, type SaveState } from '../../components/SaveStatus.tsx';
import { SummaryList } from '../../components/SummaryList.tsx';
import { SummaryRow } from '../../components/SummaryRow.tsx';
import textInput from '../../components/textInput.module.css';
import { datePlain, parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import { PageNotFound } from '../../routes/PageNotFound.tsx';
import { invalidateToday } from '../today/todayQuery.ts';
import { goalsApi, goalsQuery } from './goalsQuery.ts';
import styles from './ParticipantPage.module.css';
import { fullName, participantsQuery, type AdminParticipant, type ParticipantStatus } from './participantsQuery.ts';

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
function typedOf({ givenName, familyName, dateOfBirth }: Details): Typed {
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
 * The participant as the server last confirmed them: their ID; their details, whose name the `<h1>` shows, so that it
 * changes only once a save is done, never as the name is typed (text-and-date-inputs.md), and which an archived
 * participant's Details show, and Restore puts back in the form; their status, which decides whether the page is the
 * form or read-only; and their version, which the next change sends as `If-Match`.
 */
type Known = {
  id: string;
  details: Details;
  status: ParticipantStatus;
  /**
   * The version that the page loaded them at, then the one that the list gives after each change and each refusal as
   * stale, never one that a refetch brings in between, which would let a change overwrite one that the page has not
   * shown; `undefined` while the page has read none, for a participant that it has just added, or when its read after
   * a change did not load, so that the next change reads the list first.
   */
  version: string | undefined;
};

/** The participant as the page knows them, from the list. */
function knownOf({ id, givenName, familyName, dateOfBirth, status, rowVersion }: AdminParticipant): Known {
  return { id, details: { givenName, familyName, dateOfBirth }, status, version: rowVersion };
}

/** A correction of a participant's Details, and the version it is sent with. */
type Correction = { id: string; details: Details; version: string | undefined };

/** An archive or a restore of a participant, and the version it is sent with. */
type StatusChange = { id: string; action: 'archive' | 'restore'; version: string | undefined };

/**
 * What a change to a participant who exists came to: done, with its answer, or refused as stale (`412`); and the
 * participant as the list gives them after it, read afresh, or `undefined` when that read did not load.
 */
type Outcome<T> = ({ done: true; answer: T } | { done: false }) & { now: AdminParticipant | undefined };

/**
 * The participant as the list gives them now (design.md §6.6: a change answers with no version), from a read sent now:
 * a read already under way began before the change's answer, so it is called off first, rather than joined. It fails
 * as the list's request fails, and is `undefined` when the list does not hold them.
 */
async function listed(queryClient: QueryClient, id: string): Promise<AdminParticipant | undefined> {
  await queryClient.cancelQueries({ queryKey: participantsQuery.queryKey });
  const participants = await queryClient.fetchQuery(participantsQuery);
  return participants.find((participant) => participant.id === id);
}

/**
 * Sends a change to a participant who exists with `version` as `If-Match`, or, with none, the one that the list gives
 * first, then reads the list again for what it now holds of them, as no change answers with their next version, and
 * for the list screen: inside the change, so that its button stays busy until the next version is known. A `412` is
 * an outcome, as the page shows it; any other failure, the first read's included, fails the change.
 */
async function changeAndReread<T>(
  queryClient: QueryClient,
  id: string,
  version: string | undefined,
  send: (ifMatch: string | undefined) => Promise<T>,
): Promise<Outcome<T>> {
  const ifMatch = version ?? (await listed(queryClient, id))?.rowVersion;
  const reread = () => listed(queryClient, id).catch(() => undefined);
  try {
    const answer = await send(ifMatch);
    return { done: true, answer, now: await reread() };
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 412)) {
      throw error;
    }

    return { done: false, now: await reread() };
  }
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
      <PageStatus>
        <Participant onAdded={setAddedId} />
      </PageStatus>
    );
  }

  return <ListedParticipant key={participantId} participantId={participantId} />;
}

/**
 * The back link to the list, in the shell's bar (participants.md B1): to the Archived view for an archived
 * participant, so that the manager returns to the view the person is in, and else to the list's Active view.
 */
function BackToParticipants({ archived }: { archived: boolean }) {
  return (
    <PageBar>
      <BackLink to={archived ? '/manage/participants?view=archived' : '/manage/participants'}>
        {pageName.participants}
      </BackLink>
    </PageBar>
  );
}

/**
 * A participant's page opened at their address (participants.md B9): the back link alone while the list and their
 * goals load, together, as the `<h1>` is their name, then "Loading participant…" after 1 s; then their page, from the
 * list, whose `<h1>` takes the focus left pending for it; or the fallback `<h1>` "Participant" with the failure
 * sentence and Try again, when either did not load; or, for an ID not on the list, or goals refused as not found, Page
 * not found in place of the whole page.
 */
function ListedParticipant({ participantId }: { participantId: string }) {
  const participants = useQuery(participantsQuery);
  const goals = useQuery(goalsQuery(participantId));
  // Found in the list rather than picked out by a `select`, whose `undefined` for an ID not on it the region would
  // take for a list not loaded yet.
  const participant = participants.data?.find(({ id }) => id === participantId);

  if (participants.data !== undefined && participant === undefined) {
    return <PageNotFound />;
  }

  return (
    <PageStatus>
      {/* Until the region shows the page, whose own back link follows their status from then on. */}
      {(participant === undefined || goals.data === undefined) && <BackToParticipants archived={false} />}
      <LoadRegion
        queries={[participants, goals]}
        loading={strings.participants.participantLoading}
        what={strings.participants.thisParticipant}
        fallbackHeading={pageName.participant}
      >
        {/* Called only once both queries have data, the list then holding the participant. */}
        {() => <Participant participant={participant!} />}
      </LoadRegion>
    </PageStatus>
  );
}

/**
 * A participant's page, with the participant as the list gave them as it opened, whom it shows from then on as it
 * knows them; or Add participant, told the new participant's ID once Save has added them.
 */
type ParticipantProps =
  | { participant: AdminParticipant; onAdded?: never }
  | { participant?: never; onAdded: (id: string) => void };

/**
 * Add participant or a participant's page (participants.md B1 to B7): the back link, the error summary after a failed
 * Save, the `<h1>`, then Details, a form of the given and family names and the date of birth, with no `name` on any
 * field, so that nothing it holds can reach an address, and Save, with the alert above it for a request that failed and
 * the save status beside it; then, once the participant exists, their Goals, and Actions, which holds Past notes, a
 * link styled as a secondary button, then Write past-day note, which an archived participant has not, then, after a
 * rule, Archive participant; T21 adds Export record after them. Nothing is checked before Save, which checks every
 * field in page order and sends nothing while one fails; once a field shows a message, each edit checks it again. A
 * `201` shows "Saved 4:12 pm" from its `Date`, makes the name the `<h1>`, puts an empty list in the new participant's
 * goals query, as they have none, rather than fetching it, and replaces the address with the participant's, in the
 * screen. A save of a participant who exists is a `PUT` with their version as `If-Match`, whose `200` shows
 * "Saved 4:12 pm" and the saved name as the `<h1>`, and whose `412` says that someone else changed the details, the
 * fields kept as typed. Focus stays on Save.
 *
 * An archived participant's page is read-only (participants.md B3, *Archive and Restore*): the archived notice under
 * the `<h1>`, with Restore, then Details as a summary list, their goals as text alone, and Actions with Past notes
 * alone, as an archived participant's notes are kept (design.md §3.7). Archive participant, with no dialog, turns the
 * page read-only, dropping what was typed (participants.md open question 3), and focuses the notice, as its button has
 * gone; Restore turns it back into the form, filled in afresh, and focuses the `<h1>`. A press refused as stale reads
 * the list again: when that finds the status changed already, by someone else, the page simply shows it, as that
 * action's success does; otherwise the alert above the button says so.
 */
function Participant({ participant, onAdded }: ParticipantProps) {
  const me = useMe();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const detailsHeadingId = useId();
  const goalsHeadingId = useId();
  const goalsHelpId = useId();
  const actionsHeadingId = useId();
  const [known, setKnown] = useState<Known | undefined>(() =>
    participant === undefined ? undefined : knownOf(participant),
  );
  // As typed, in this page's memory only (D22), copied once from the participant, so that no refetch overwrites them.
  const [typed, setTyped] = useState<Typed>(() => (participant === undefined ? nothingTyped : typedOf(participant)));
  const [errors, setErrors] = useState<Errors>({});
  // The presses that found a field in error, so that each moves focus to the summary, even with the same messages.
  const [attempt, setAttempt] = useState(0);
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const [saved, setSaved] = useState<SaveState>('none');
  // The words of the alert above Archive participant, or above Restore, while it holds that press's failure.
  const [statusFailure, setStatusFailure] = useState<string>();
  // Whether the archived notice appeared because of a press, whose button has gone, so that it takes focus.
  const [noticeTakesFocus, setNoticeTakesFocus] = useState(false);
  const add = useMutation({
    mutationFn: (details: Details) =>
      apiWithHeaders<{ id: string }>(participantsPath, { method: 'POST', body: details }),
    // The add has no idempotency key (design.md §6.6), so one sent again after its answer was lost could add the
    // participant twice: the person presses Save again instead.
    retry: 0,
    // Even once the page has gone, so that the list and Today show them.
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: participantsQuery.queryKey });
      void invalidateToday(queryClient);
    },
  });
  // Each sent once, as the query client's mutations are: one sent again after a lost answer would meet its own change
  // as a `412`. Each reads the list again itself, and leaves Today to fetch its list again too, as it shows the
  // participant's name, and only while they are active (participants.md *Interactions*).
  const correct = useMutation({
    mutationFn: ({ id, details, version }: Correction): Promise<Outcome<WithHeaders<undefined>>> =>
      changeAndReread(queryClient, id, version, (ifMatch) =>
        apiWithHeaders<undefined>(`${participantsPath}/${id}`, { method: 'PUT', body: details, ifMatch }),
      ),
    onSuccess: () => {
      void invalidateToday(queryClient);
    },
  });
  const statusChange = useMutation({
    mutationFn: ({ id, action, version }: StatusChange): Promise<Outcome<undefined>> =>
      changeAndReread(queryClient, id, version, (ifMatch) =>
        api<undefined>(`${participantsPath}/${id}/${action}`, { method: 'POST', ifMatch }),
      ),
    onSuccess: () => {
      void invalidateToday(queryClient);
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

  /**
   * The participant in the other status, as a press found them: the page turns read-only, dropping what was typed, or
   * back into the form, filled in afresh with their details; and focus goes where that action's success sends it, as
   * the button pressed has gone: to the archived notice as it appears, or to the `<h1>`, which stays in the page.
   */
  function turn(next: Known) {
    setKnown(next);
    setTyped(typedOf(next.details));
    setErrors({});
    setFailure(undefined);
    setSaved('none');
    setStatusFailure(undefined);
    setNoticeTakesFocus(next.status === 'Archived');
    if (next.status === 'Active') {
      focusPageHeading();
    }
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
          onSuccess: (outcome) => {
            // Archived by someone else meanwhile, which the read shows (participants.md *Interactions*).
            if (outcome.now !== undefined && outcome.now.status !== known.status) {
              turn(knownOf(outcome.now));
              return;
            }

            if (!outcome.done) {
              setKnown({ ...known, version: outcome.now?.rowVersion });
              setFailure(strings.participantDetails.changedMeanwhile);
              return;
            }

            setKnown({ ...known, details, version: outcome.now?.rowVersion });
            setSaved({ savedAtUtc: outcome.answer.dateUtc });
          },
          onError: refused,
        },
      );
      return;
    }

    add.mutate(details, {
      onSuccess: ({ body, dateUtc }) => {
        // Before the Goals section appears, so that it shows them at once, with nothing to wait for.
        queryClient.setQueryData(goalsQuery(body.id).queryKey, []);
        setKnown({ id: body.id, details, status: 'Active', version: undefined });
        onAdded?.(body.id);
        setSaved({ savedAtUtc: dateUtc });
        // Replaced, so that Back goes to the list, not to an empty form, and in the screen, so that the shell moves
        // neither focus nor scroll (app-shell.md *The route-change rule (one owner)*).
        void navigate(`/manage/participants/${body.id}`, { replace: true, state: { inScreen: true } });
      },
      onError: refused,
    });
  }

  /**
   * Archives the participant, to `'Archived'`, or restores them, to `'Active'`: done, the page shows them in that
   * status, as the list now gives them or, when that read did not load, as it knew them, with no version; refused as
   * stale, the same when the list shows them in that status already, and otherwise the alert says that someone else
   * changed them, and only their version changes. A failure says "Not archived: …" or "Not restored: …" in the
   * alert, focus staying on the button.
   */
  function changeStatusTo(current: Known, target: ParticipantStatus) {
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setStatusFailure(undefined);
    statusChange.mutate(
      { id: current.id, action: target === 'Archived' ? 'archive' : 'restore', version: current.version },
      {
        onSuccess: ({ done, now }) => {
          if (done || now?.status === target) {
            const shown = now === undefined ? { ...current, version: undefined } : knownOf(now);
            turn({ ...shown, status: target });
            return;
          }

          setKnown({ ...current, version: now?.rowVersion });
          setStatusFailure(strings.participantActions.changedMeanwhile);
        },
        onError: (error) => {
          const cause = classify(error);
          if (cause !== 'signedOut') {
            setStatusFailure(target === 'Archived' ? msg.notArchived(cause) : msg.notRestored(cause));
          }
        },
      },
    );
  }

  const archived = known?.status === 'Archived';

  return (
    <>
      <BackToParticipants archived={archived} />
      <ErrorSummary errors={summaryItems(errors)} attempt={attempt} />
      <PageHeading>{known === undefined ? pageName.addParticipant : fullName(known.details)}</PageHeading>
      {known !== undefined && archived && (
        <Notice
          takesFocus={noticeTakesFocus}
          action={
            <>
              <RequestAlert>{statusFailure}</RequestAlert>
              <Button
                busy={statusChange.isPending}
                busyLabel={strings.restoring}
                onClick={() => changeStatusTo(known, 'Active')}
              >
                {strings.restore}
              </Button>
            </>
          }
        >
          {msg.participantArchived(fullName(known.details))}
        </Notice>
      )}
      <section aria-labelledby={detailsHeadingId} className={styles.details}>
        <h2 id={detailsHeadingId}>{strings.participantDetails.heading}</h2>
        {known !== undefined && archived ? (
          <SummaryList>
            <SummaryRow term={strings.participantDetails.givenName}>{known.details.givenName}</SummaryRow>
            <SummaryRow term={strings.participantDetails.familyName}>{known.details.familyName}</SummaryRow>
            <SummaryRow term={strings.dateOfBirth.legend}>
              {datePlain(parseDateOnly(known.details.dateOfBirth))}
            </SummaryRow>
          </SummaryList>
        ) : (
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
        )}
      </section>
      {known !== undefined && (
        <section aria-labelledby={goalsHeadingId} className={styles.goals}>
          <h2 id={goalsHeadingId}>{strings.participantGoals.heading}</h2>
          <ListFrame helpId={goalsHelpId} help={archived ? undefined : strings.listEditor.goal.help}>
            <ListConflict />
            <ListEditor kind="goal" api={goalsApi(known.id)} readOnly={archived} />
          </ListFrame>
        </section>
      )}
      {known !== undefined && (
        <section aria-labelledby={actionsHeadingId} className={styles.actions}>
          <h2 id={actionsHeadingId}>{strings.participantActions.heading}</h2>
          <ButtonGroup>
            <Link to={`/participants/${known.id}/notes`} className={buttonClass()}>
              {pageName.pastNotes}
            </Link>
            {!archived && (
              <Link to={`/manage/participants/${known.id}/past-day-note`} className={buttonClass()}>
                {pageName.writePastDayNote}
              </Link>
            )}
          </ButtonGroup>
          {!archived && (
            <div className={styles.archive}>
              <RequestAlert>{statusFailure}</RequestAlert>
              <Button
                busy={statusChange.isPending}
                busyLabel={strings.archiving}
                onClick={() => changeStatusTo(known, 'Archived')}
              >
                {strings.participantActions.archive}
              </Button>
            </div>
          )}
        </section>
      )}
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
