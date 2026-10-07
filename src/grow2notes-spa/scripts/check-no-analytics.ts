// Fails if the SPA could send telemetry from the browser: design.md §9.5 ("No browser telemetry") and A34 allow no
// Application Insights JavaScript SDK and no third-party analytics. It reads the build, so CI runs it after the build.
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, relative } from 'node:path';

// A package ending in * stands for every name that starts with the rest. The text is what those scripts, or the hosts
// they load from and send to, leave in a build; it is matched ignoring case.
const denied = {
  packages: [
    // The Application Insights JavaScript SDK in all its parts, and its old name.
    '@microsoft/applicationinsights-*', 'applicationinsights*',
    // Azure Monitor and OpenTelemetry in the browser.
    '@azure/monitor-opentelemetry*', '@opentelemetry/sdk-trace-web', '@opentelemetry/auto-instrumentations-web',
    '@opentelemetry/instrumentation-*', '@opentelemetry/exporter-*',
    // Third-party analytics, error tracking and session recording.
    'react-ga', 'react-ga4', 'ga-4-react', 'gtag', 'analytics', '@analytics/*', 'mixpanel-browser', '@segment/*',
    '@amplitude/*', 'amplitude-js', 'posthog-js', '@sentry/*', 'logrocket', '@hotjar/*', 'react-hotjar', '@fullstory/*',
    'plausible-tracker', '@vercel/analytics', 'newrelic', '@newrelic/*', '@datadog/browser-*', '@microsoft/clarity',
    '@bugsnag/*', 'rollbar', '@rudderstack/*', '@snowplow/*', 'react-facebook-pixel',
  ],
  text: [
    // The Application Insights SDK's own name, and the hosts it sends to and loads from.
    'applicationinsights', 'dc.services.visualstudio.com', 'js.monitor.azure.com', 'az416426.vo.msecnd.net',
    // The third parties' names and hosts.
    'googletagmanager', 'google-analytics.com', 'gtag(', 'segment.com', 'segment.io', 'mixpanel', 'amplitude.com',
    'posthog', 'sentry.io', 'logrocket', 'hotjar', 'clarity.ms', 'fullstory', 'heapanalytics', 'plausible.io',
    'newrelic', 'nr-data.net', 'datadoghq', 'connect.facebook.net', 'matomo', 'bugsnag', 'rollbar', 'rudderstack',
    'snowplow',
  ],
};

const spaRoot = join(import.meta.dirname, '..');
// vite.config.ts's build.outDir.
const outDir = join(spaRoot, '..', 'Grow2Notes.Web', 'wwwroot');

if (!existsSync(join(outDir, 'index.html'))) {
  console.error(`${outDir} has no build to check. Run npm run build first.`);
  process.exit(1);
}

const problems: string[] = [];

// Every package npm ci installs, development ones included, because Vite bundles whatever the code imports from either
// list. A key is an install path such as node_modules/a/node_modules/@scope/b; "" is the SPA itself.
const lockfile = JSON.parse(readFileSync(join(spaRoot, 'package-lock.json'), 'utf8')) as {
  packages: Record<string, { name?: string }>;
};
const installed = new Set<string>();
for (const [path, { name }] of Object.entries(lockfile.packages)) {
  if (path === '') continue;
  installed.add(path.replace(/^.*node_modules\//, ''));
  // An alias (npm:) is installed under its own name, and the lockfile records the real one.
  if (name !== undefined) installed.add(name);
}
for (const name of installed) {
  const isDenied = denied.packages.some((entry) =>
    entry.endsWith('*') ? name.startsWith(entry.slice(0, -1)) : name === entry,
  );
  if (isDenied) problems.push(`package-lock.json installs ${name}`);
}

const files = readdirSync(outDir, { recursive: true, withFileTypes: true })
  .filter((entry) => entry.isFile())
  .map((entry) => join(entry.parentPath, entry.name));
for (const file of files) {
  const content = readFileSync(file, 'utf8').toLowerCase();
  for (const text of denied.text) {
    if (content.includes(text)) problems.push(`${relative(outDir, file)} contains "${text}"`);
  }
}

// The build's own scripts load from same-origin paths, so a script URL with a scheme or host is a third party's.
const indexHtml = readFileSync(join(outDir, 'index.html'), 'utf8');
for (const [, src] of indexHtml.matchAll(/<script\b[^>]*\ssrc\s*=\s*["']?([^"'\s>]+)/gi)) {
  if (/^([a-z][\w+.-]*:|\/\/)/i.test(src)) problems.push(`index.html loads a script from ${src}`);
}

if (problems.length > 0) {
  console.error(`Browser telemetry is not allowed (design.md §9.5, A34):\n  ${problems.join('\n  ')}`);
  process.exitCode = 1;
} else {
  console.log(`No browser telemetry in the ${installed.size} installed packages or the ${files.length} built files.`);
}
