import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
// Before App, so that the build puts these first and a component's own stylesheet wins over them at equal specificity.
import './styles/tokens.css';
import './styles/base.css';
import { App } from './App.tsx';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('index.html has no #root element.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
