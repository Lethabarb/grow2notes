export function App() {
  // Throwaway: never reassigned, so ESLint's prefer-const rejects the let, which tsc accepts.
  let heading = 'Grow2Notes';
  return <h1>{heading}</h1>;
}
