import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useLayoutEffect, useRef, useState, type FormEvent } from 'react';
import { ApiError, apiWithHeaders, classify } from '../../api/api.ts';
import { refreshMe } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { FieldError } from '../../components/FieldError.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { SaveStatus, type SaveState } from '../../components/SaveStatus.tsx';
import textBox from '../../components/TextBox.module.css';
import { useDelayed } from '../../components/useDelayed.ts';
import { useErrorTitle } from '../../components/useErrorTitle.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import { unloadGuardArmed } from '../../routes/leaveGuard.ts';
import { fieldIds } from './fieldIds.ts';
import styles from './GuidePromptsForm.module.css';
import { guidePromptsQuery, type GuidePrompts } from './guidePromptsQuery.ts';

const promptsPath = '/api/admin/guide-prompts';
// The count shows from 900 characters, 90% of the limit, GOV.UK's threshold (guide-prompts.md 6c).
const countFrom = 900;
// Its twin speaks once typing has stopped for 1 s, never at each keystroke.
const countPause = 1000;
// When Save's busy label shows (Button), at which the save status holds its hidden "Saving…" too (6f).
const savingDelay = 400;

/**
 * The guide prompts editor (guide-prompts.md component 6), mounted once the prompts have loaded: an uncontrolled
 * `<textarea>` in the shared text box, holding the loaded text exactly, line breaks included, and described by the
 * page's help text and limit hint, with the count under it from 900 characters; then Save, which sends the text
 * trimmed, or, over 1,000 characters, shows the error and sends nothing, with the alert above it for a request that
 * failed and the save status beside it. The field is never focused by itself, as on a phone the keyboard would open
 * over the help text, and has no placeholder, which could be taken for saved prompts. Nothing is sent while typing,
 * as every save reaches every worker's empty Guided notes box at once.
 */
export function GuidePromptsForm({ loaded }: { loaded: GuidePrompts }) {
  const queryClient = useQueryClient();
  // Read once: a later refetch of the query must never change what the field holds (settings-textarea.md *Behaviour*
  // 2), as `defaultValue` would when it changed.
  const [text] = useState(loaded.text);
  // The text that the server holds, as far as the page knows, and its ETag's token, which the next save sends as
  // `If-Match`: from the load, then from each save's `200` and each `412`'s refetch alone.
  const [base, setBase] = useState(loaded);
  // The field's text, trimmed, as its last input left it, which the count counts and the base is compared with.
  const [trimmed, setTrimmed] = useState(() => loaded.text.trim());
  const [saving, setSaving] = useState(false);
  const slow = useDelayed(saving, savingDelay);
  // The status once a save is done, emptied by the next press and the next keystroke.
  const [saved, setSaved] = useState<Exclude<SaveState, 'saving'>>('none');
  // The alert's words while it holds a failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  // The presses that found the text too long, so that each moves focus to the field, even with the error showing;
  // `0` while it shows no error.
  const [tooLong, setTooLong] = useState(0);
  // The latest input's count, a new object at each, from which the twin speaks once typing has stopped.
  const [typed, setTyped] = useState<{ count: string }>();
  const [spoken, setSpoken] = useState('');
  const field = useRef<HTMLTextAreaElement>(null);
  const grow = useRef<HTMLDivElement>(null);
  const length = trimmed.length;
  const over = length > limits.guidePrompts;
  // The field holds text that the server does not, so that leaving would lose it.
  const dirty = trimmed !== base.text;
  useErrorTitle(tooLong > 0 || failure !== undefined);

  useLayoutEffect(() => {
    // Leaves out the browser's own completions, in text that every worker sees on every note; React has no prop for
    // it yet.
    field.current?.setAttribute('writingsuggestions', 'false');
  }, []);

  // After the render that names the error in the field's description, so that a screen reader reads it with focus.
  useEffect(() => {
    if (tooLong > 0) {
      field.current?.focus();
    }
  }, [tooLong]);

  useEffect(() => {
    if (typed === undefined) {
      return;
    }

    const timer = window.setTimeout(() => {
      setSpoken(typed.count);
    }, countPause);
    return () => window.clearTimeout(timer);
  }, [typed]);

  // The browser's own warning before a reload, a closed tab or a typed address, added only while the text is not
  // saved, as a page with the listener loses the browser's back-forward cache (settings-textarea.md *Implementation
  // notes*).
  useEffect(() => {
    if (!dirty) {
      return;
    }

    const warn = (event: BeforeUnloadEvent) => {
      if (unloadGuardArmed()) {
        event.preventDefault();
        // For Chromium before 119, which asks only when this is set.
        event.returnValue = true;
      }
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  function onInput(event: FormEvent<HTMLTextAreaElement>) {
    const { value } = event.currentTarget;
    grow.current?.setAttribute('data-value', value);
    const next = value.trim();
    setTrimmed(next);
    // So that the status never says "Saved" over changes that are not.
    setSaved('none');
    // Silently, once the text is short enough (form-validation.md *Fixing*).
    if (next.length <= limits.guidePrompts) {
      setTooLong(0);
    }

    setTyped({ count: next.length >= countFrom ? msg.characterCount(next.length, limits.guidePrompts) : '' });
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied at each press, so that what the press brings is written into empty regions and announced, the same
    // words included.
    setFailure(undefined);
    setSaved('none');
    // From the field itself, as it is now, its line breaks as LF. The field is in this form, so it is there.
    const sent = field.current!.value.trim();
    if (sent.length > limits.guidePrompts) {
      setTooLong((presses) => presses + 1);
      return;
    }

    // In the press's own render, so that a second quick press meets Save busy and sends nothing.
    setSaving(true);
    try {
      // Sent once for each press, never again by itself: a PUT sent again after its answer was lost would meet the
      // ETag it moved on, and say that another manager saved (settings-textarea.md *Behaviour* 5).
      const { etag, dateUtc } = await apiWithHeaders<undefined>(promptsPath, {
        method: 'PUT',
        body: { text: sent },
        ifMatch: base.etag,
      });
      // Whatever was typed while it was out stays not saved.
      setBase({ text: sent, etag });
      setSaved({ savedAtUtc: dateUtc });
    } catch (error) {
      await answerFailure(error);
    } finally {
      setSaving(false);
    }
  }

  async function answerFailure(error: unknown) {
    if (error instanceof ApiError && error.status === 412) {
      // Another manager saved first: theirs becomes the base, while the field keeps this manager's text, which the
      // next Save puts in its place, both kept in the audit log (settings-textarea.md *Behaviour* 8). Save stays busy
      // until the refetch has answered, so that the next press sends the new ETag. A refetch that fails leaves the
      // base as it was, and that press meets the 412 again.
      const fresh = await queryClient.fetchQuery({ ...guidePromptsQuery, staleTime: 0 }).catch(() => undefined);
      if (fresh !== undefined) {
        setBase(fresh);
      }

      setFailure(strings.guidePrompts.conflict);
    } else if (error instanceof ApiError && error.status === 422 && error.errors?.text !== undefined) {
      // The server counted more than 1,000 characters, which the field shows as Save would.
      setTooLong((presses) => presses + 1);
    } else if (error instanceof ApiError && error.status === 403) {
      // No longer a manager: the shell's role guard shows Page not found once `/me` says so (guide-prompts.md
      // *States*). A refresh that fails leaves the page as it was.
      refreshMe(queryClient).catch(() => {});
    } else {
      // A 401 has the session flow, which `api()` started, and shows nothing here.
      const cause = classify(error);
      if (cause !== 'signedOut') {
        setFailure(msg.notSaved(cause));
      }
    }
  }

  const description = [fieldIds.help, fieldIds.limit, tooLong > 0 ? fieldIds.error : undefined].filter(
    (id) => id !== undefined,
  );

  return (
    <form noValidate onSubmit={(event) => void save(event)}>
      <div className={styles.group} data-invalid={tooLong > 0 || undefined}>
        {tooLong > 0 && (
          <FieldError fieldId={fieldIds.prefix}>{msg.tooLong(pageName.guidePrompts, limits.guidePrompts)}</FieldError>
        )}
        {/* The copy behind the box, from data-value, sizes it where there is no field-sizing (TextBox.module.css). */}
        <div ref={grow} className={textBox.grow} data-value={text}>
          <textarea
            ref={field}
            id={fieldIds.text}
            name="text"
            className={textBox.box}
            defaultValue={text}
            aria-describedby={description.join(' ')}
            aria-invalid={tooLong > 0 || undefined}
            data-over-limit={over || undefined}
            spellCheck
            autoCapitalize="sentences"
            autoComplete="off"
            onInput={onInput}
          />
        </div>
        {/* Hidden from screen readers, which hear its twin instead, once typing has stopped. */}
        {length >= countFrom && (
          <p aria-hidden="true" className={styles.count} data-over-limit={over || undefined}>
            {msg.characterCount(length, limits.guidePrompts)}
          </p>
        )}
        <p aria-live="polite" className="visually-hidden">
          {spoken}
        </p>
      </div>
      <div className={styles.actions}>
        <RequestAlert>{failure}</RequestAlert>
        <div className={styles.saveRow}>
          {/* Not announced by the page: the save status says "Saving…" itself (guide-prompts.md 6e). */}
          <Button type="submit" variant="primary" busy={saving} busyLabel={strings.saving} announceBusy={false}>
            {strings.save}
          </Button>
          <SaveStatus state={saving ? (slow ? 'saving' : 'none') : saved} />
        </div>
      </div>
    </form>
  );
}
