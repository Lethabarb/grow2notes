import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type FormEvent, type Ref } from 'react';
import { api, ApiError, classify } from '../../api/api.ts';
import { refreshMe, useMe } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { ButtonGroup } from '../../components/ButtonGroup.tsx';
import { CharacterCount } from '../../components/CharacterCount.tsx';
import { FieldError } from '../../components/FieldError.tsx';
import fieldGroup from '../../components/FieldGroup.module.css';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import textBox from '../../components/TextBox.module.css';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { dateTime } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import { noteBase, noteQuery } from './noteQueries.ts';
import { invalidatePastNotes } from './pastNotesQuery.ts';
import styles from './ReviewPanel.module.css';

const commentId = 'review-comment';

// The comments typed and not sent, by `${participantId}/${noteDate}`, in memory only: never in storage, the URL, router
// state or the title (design.md D22), so that Edit, Save changes and Cancel, which open the panel afresh, bring back
// what was typed, and a reload or a sign-out takes it.
const unsentComments = new Map<string, string>();

/** A review as the panel shows it: who made it, when, in UTC, and its comment. */
type Shown = { displayName: string; atUtc: string; comment: string | null };

/** What replaces the form: the manager's own review, or one that another manager made first, with the unsent text. */
type Outcome = { kind: 'reviewed'; review: Shown } | { kind: 'other'; review: Shown; unsent: string };

/** What a press sends. */
type Sent = { versionNumber: number; comment: string | null };

type ReviewPanelProps = {
  participantId: string;
  noteDate: string;
  /** The version on the screen, which a review names so that the manager has seen what is current. */
  versionNumber: number;
};

/**
 * The Review panel (review-panel.md, participant-notes.md component 8), the last block of a manager's read view of a
 * note that was To review as the page opened: the heading, the optional comment with its hint and count, and Mark
 * reviewed. A press sends `POST {base}/reviews` with the version on the screen and the comment trimmed, `null` when
 * blank; the form then gives way to the record, focused. The button is never disabled; while the request is out it is
 * `aria-disabled` and the comment `readOnly`.
 *
 * A `409` means the note is no longer To review at that version, and the note is read again to say which: reviewed by
 * this manager at that version, a press whose answer was lost, which is the success it was; reviewed by another, whose
 * record replaces the form with the comment that was not saved; or edited since, whose newer version the read view now
 * shows, with the comment kept and the next press naming that version. Any other failure keeps the comment and the
 * button, and sends nothing again.
 */
export function ReviewPanel({ participantId, noteDate, versionNumber }: ReviewPanelProps) {
  const me = useMe();
  const queryClient = useQueryClient();
  const key = `${participantId}/${noteDate}`;
  const [text, setText] = useState(() => unsentComments.get(key) ?? '');
  // The text's inputs, after which the count's twin speaks once typing has stopped.
  const [inputs, setInputs] = useState(0);
  const [error, setError] = useState<string>();
  const [alert, setAlert] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [outcome, setOutcome] = useState<Outcome>();
  const field = useRef<HTMLTextAreaElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  // The line that focus moves to once the form has gone.
  const focusTarget = useRef<HTMLParagraphElement>(null);
  const { mutate } = useMutation({
    mutationFn: (sent: Sent) =>
      api<{ reviewedAtUtc: string }>(`${noteBase(participantId, noteDate)}/reviews`, { method: 'POST', body: sent }),
  });

  useEffect(() => {
    if (outcome !== undefined) {
      focusTarget.current?.focus();
    }
  }, [outcome]);

  function change(value: string) {
    setText(value);
    setInputs((count) => count + 1);
    unsentComments.set(key, value);
    // Rewards the fix at once: the message goes the moment the text fits.
    if (error !== undefined && value.length <= limits.reviewComment) {
      setError(undefined);
    }
  }

  function press(event: FormEvent) {
    event.preventDefault();
    // The raw text, as the count shows it, where the server counts the comment as sent.
    if (text.length > limits.reviewComment) {
      setError(msg.tooLong(strings.review.comment, limits.reviewComment));
      field.current?.focus();
      return;
    }

    setError(undefined);
    setAlert(undefined);
    setBusy(true);
    const trimmed = text.trim();
    const sent = { versionNumber, comment: trimmed === '' ? null : trimmed };
    mutate(sent, {
      onSuccess: ({ reviewedAtUtc }) => {
        reviewed({ displayName: me.displayName, atUtc: reviewedAtUtc, comment: sent.comment });
      },
      onError: (failure) => {
        void failed(failure, sent);
      },
    });
  }

  function reviewed(review: Shown) {
    unsentComments.delete(key);
    setOutcome({ kind: 'reviewed', review });
    void queryClient.invalidateQueries({ queryKey: ['notes', participantId, noteDate] });
    void queryClient.invalidateQueries({ queryKey: ['reviews'] });
    void invalidatePastNotes(queryClient, participantId);
    // A failure leaves `me` as it was, as the shell's refreshes do.
    refreshMe(queryClient).catch(() => {});
  }

  function stay(words: string | undefined) {
    setAlert(words);
    setBusy(false);
    button.current?.focus();
  }

  async function failed(failure: unknown, sent: Sent) {
    if (failure instanceof ApiError && failure.status === 409 && failure.code === 'review.not_current') {
      await conflict(sent);
      return;
    }

    const refusal = failure instanceof ApiError && failure.status === 422 ? failure.errors?.comment?.[0] : undefined;
    if (refusal !== undefined) {
      setError(refusal);
      setBusy(false);
      field.current?.focus();
      return;
    }

    if (failure instanceof ApiError && failure.status === 403) {
      // No longer a manager: the shell's role guard shows Page not found once `/me` says so.
      refreshMe(queryClient).catch(() => {});
    }

    // A 401 has the session flow, which `api()` started, and shows nothing here: the comment stays, and the press is
    // never sent again after signing in.
    const cause = classify(failure);
    stay(cause === 'signedOut' ? undefined : msg.notMarkedReviewed(cause));
  }

  async function conflict(sent: Sent) {
    const fresh = await queryClient
      .fetchQuery({ ...noteQuery(participantId, noteDate), staleTime: 0 })
      .catch(() => undefined);
    const now = fresh?.status === 'Submitted' ? fresh : undefined;
    const latest = now?.flagStatus === 'Reviewed' ? now.reviews?.[0] : undefined;
    if (latest !== undefined) {
      const review = {
        displayName: latest.reviewedBy.displayName,
        atUtc: latest.reviewedAtUtc,
        comment: latest.comment,
      };
      // This manager's own press, whose answer was lost, found the note reviewed already: the success it was.
      if (latest.reviewedBy.id === me.userId && latest.versionNumber === sent.versionNumber) {
        reviewed(review);
      } else {
        unsentComments.delete(key);
        setOutcome({ kind: 'other', review, unsent: text });
      }
    } else if (now?.flagStatus === 'ToReview') {
      stay(strings.review.editedSinceOpened);
    } else {
      stay(apiErrorCopy['review.not_current']);
    }
  }

  if (outcome?.kind === 'reviewed') {
    return (
      <section>
        <h2>{strings.review.heading}</h2>
        <ReviewRecord review={outcome.review} ref={focusTarget} />
      </section>
    );
  }

  if (outcome?.kind === 'other') {
    return (
      <section>
        <h2>{strings.review.heading}</h2>
        <div className={styles.record}>
          <p tabIndex={-1} ref={focusTarget}>
            {strings.review.reviewedWhileOpen}
          </p>
        </div>
        <ReviewRecord review={outcome.review} />
        {outcome.unsent.trim() !== '' && (
          <div className={styles.record}>
            <p>{strings.review.commentNotSaved}</p>
            <p className={styles.written}>{outcome.unsent}</p>
          </div>
        )}
      </section>
    );
  }

  const describedBy = [error === undefined ? undefined : `${commentId}-error`, `${commentId}-hint`, `${commentId}-limit`];
  return (
    <section>
      <h2>{strings.review.heading}</h2>
      <form noValidate className={styles.form} onSubmit={press}>
        <div className={fieldGroup.group} data-invalid={error !== undefined || undefined}>
          <label htmlFor={commentId} className={fieldGroup.label}>
            {strings.review.commentLabel}
          </label>
          <p id={`${commentId}-hint`} className={fieldGroup.hint}>
            {strings.review.commentHint}
          </p>
          <p id={`${commentId}-limit`} className="visually-hidden">
            {msg.characterLimitHint(limits.reviewComment)}
          </p>
          {error !== undefined && <FieldError fieldId={commentId}>{error}</FieldError>}
          <textarea
            ref={field}
            id={commentId}
            name="comment"
            rows={4}
            className={`${textBox.box} ${styles.field}`}
            value={text}
            readOnly={busy}
            aria-invalid={error !== undefined || undefined}
            aria-describedby={describedBy.filter((id) => id !== undefined).join(' ')}
            onChange={(event) => {
              change(event.currentTarget.value);
            }}
          />
          <CharacterCount length={text.length} limit={limits.reviewComment} inputs={inputs} />
        </div>
        <RequestAlert>{alert}</RequestAlert>
        <ButtonGroup>
          <Button ref={button} variant="primary" type="submit" busy={busy} busyLabel={strings.review.markingReviewed}>
            {strings.review.markReviewed}
          </Button>
        </ButtonGroup>
      </form>
    </section>
  );
}

type ReviewRecordProps = {
  review: Shown;
  /** Set on the record that focus moves to. */
  ref?: Ref<HTMLParagraphElement>;
};

/**
 * A review as plain lines (design.md A74): "Reviewed by Jo Smith on Fri 2 Oct 2026, 9:30 am", then its comment, if it
 * has one. The first line takes focus where a record replaces the form.
 */
function ReviewRecord({ review, ref }: ReviewRecordProps) {
  return (
    <div className={styles.record}>
      <p tabIndex={ref === undefined ? undefined : -1} ref={ref}>
        {strings.tag.reviewed} {msg.reviewedBy(review.displayName)}{' '}
        <time dateTime={review.atUtc}>{dateTime(review.atUtc)}</time>
      </p>
      {review.comment !== null && <p className={styles.written}>{msg.reviewComment(review.comment)}</p>}
    </div>
  );
}
