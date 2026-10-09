import { QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
// Before App, so that the build puts these first and a component's own stylesheet wins over them at equal specificity.
import './styles/tokens.css';
import './styles/base.css';
import { App } from './App.tsx';
import { createQueryClient } from './api/queryClient.ts';
import { PageStatus } from './components/PageStatus.tsx';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('index.html has no #root element.');
}

const queryClient = createQueryClient();

createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      {/* The placeholder home page is the one page, so its status region is here until the shell gives each page its
          own (app-shell.md component 3). */}
      <PageStatus>
        <App />
      </PageStatus>
    </QueryClientProvider>
  </StrictMode>,
);
