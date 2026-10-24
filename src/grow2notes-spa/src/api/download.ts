import { apiFile } from './api.ts';

// 30 seconds in place of the default 10: the server builds the whole file before it sends a byte, which M4 allows up to
// 10 seconds for 20 notes (empty-loading-error.md *Timing*).
const downloadTimeoutMs = 30_000;

// Long enough for the browser to have started reading the file, which it does as the click is handled.
const revokeAfterMs = 60_000;

/**
 * Fetches a file and saves it through the browser (design.md A78, file-download.md *Save step*): the bytes as a `blob:`
 * URL of this origin, and a hidden `<a download>` with no `target`, which WebKit would turn into a blank tab
 * (daily-report.md component 6), clicked and removed. The name is the one in the answer's `Content-Disposition`,
 * `fallbackName` when it has none; the promise gives that name. Fails as `apiFile()` does; a `signal` abort is
 * rethrown as it came.
 */
export async function download(path: string, fallbackName: string, signal: AbortSignal): Promise<string> {
  const { body, headers } = await apiFile(path, { timeoutMs: downloadTimeoutMs, signal });
  const url = URL.createObjectURL(body);
  const link = document.createElement('a');
  link.href = url;
  const name = nameFrom(headers.get('Content-Disposition')) ?? fallbackName;
  link.download = name;
  link.rel = 'noopener';
  link.hidden = true;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => {
    URL.revokeObjectURL(url);
  }, revokeAfterMs);
  return name;
}

// The file name in a `Content-Disposition` (RFC 6266): `filename*`, percent-encoded UTF-8 (design.md A80), before
// `filename`, quoted or bare. A `filename*` that does not decode is passed over.
function nameFrom(header: string | null): string | undefined {
  const encoded = header?.match(/filename\*\s*=\s*UTF-8''([^;\s]+)/i)?.[1];
  if (encoded !== undefined) {
    try {
      return decodeURIComponent(encoded);
    } catch {
      // Falls through to `filename`.
    }
  }

  const plain = header?.match(/filename\s*=\s*(?:"([^"]*)"|([^;\s]+))/i);
  const name = plain?.[1] ?? plain?.[2];
  return name === '' ? undefined : name;
}
