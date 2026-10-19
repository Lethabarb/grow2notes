import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { describe, expect, it } from 'vitest';
import type { StartedBy } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { DiscardDialog } from './DiscardDialog.tsx';

// The dialog's body for someone else's draft (confirm-dialog.md's dialog 2, *Body, manager discarding someone else's
// draft*), which a manager's read-only draft opens; the note form's own Discard is in NotePage.test.tsx.

const nbsp = ' ';

// Jordan Ellis, a manager, signed in; today in Melbourne is Thursday 1 October 2026 (A35's made-up people).
const manager: Me = {
  userId: '9c4d2b7e-3a18-4f65-8d90-7e1b5a3c6f24',
  displayName: 'Jordan Ellis',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

/** Renders the dialog for Jane Citizen's draft dated `noteDate`, started by `startedBy`, as the manager. */
function renderDialog(noteDate: string, startedBy: StartedBy) {
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, manager);
  const router = createMemoryRouter([
    {
      path: '/',
      element: (
        <DiscardDialog
          participantId="3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95"
          noteDate={noteDate}
          name="Jane Citizen"
          startedBy={startedBy}
          onClose={() => {}}
        />
      ),
    },
  ]);
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
}

describe("The Discard dialog for someone else's draft", () => {
  it('says who started it and at what time, before the line that always ends it', () => {
    // 9:14 am in Melbourne on 1 October 2026.
    renderDialog('2026-10-01', { authorDisplayName: 'Alex P.', startedAtUtc: '2026-09-30T23:14:00Z' });

    const dialog = screen.getByRole('alertdialog', { name: 'Discard the draft note for Jane Citizen?' });
    expect(dialog).toHaveAccessibleDescription(
      `Alex P. started it at 9:14${nbsp}am. Nobody will be able to open this draft again.`,
    );
  });

  // A manager's past-day note, started on a later day than its note date (design.md §3.8, A64).
  it("says an earlier day's date first, then the start's date and time when it was another day", () => {
    renderDialog('2026-09-28', { authorDisplayName: 'Alex P.', startedAtUtc: '2026-09-30T23:14:00Z' });

    expect(screen.getByRole('alertdialog')).toHaveAccessibleDescription(
      `This draft is for Monday 28${nbsp}September 2026. Alex P. started it at Thu 1${nbsp}Oct 2026, 9:14${nbsp}am. `
        + 'Nobody will be able to open this draft again.',
    );
  });
});
