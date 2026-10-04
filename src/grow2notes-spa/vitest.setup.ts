import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Testing Library unmounts rendered trees by itself only when Vitest's globals are on, and they are off here.
afterEach(cleanup);
