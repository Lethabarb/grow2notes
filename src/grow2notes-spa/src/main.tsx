import { QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
// Before the routes, so that the build puts these first and a component's own stylesheet wins over them at equal
// specificity.
import './styles/tokens.css';
import './styles/base.css';
import { createQueryClient } from './api/queryClient.ts';
import { routes } from './routes/routes.tsx';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('index.html has no #root element.');
}

const queryClient = createQueryClient();
const router = createBrowserRouter(routes);

createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
);
