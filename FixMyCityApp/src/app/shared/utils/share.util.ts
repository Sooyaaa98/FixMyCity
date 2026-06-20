// src/app/shared/utils/share.util.ts
//
// Phase 8 (§4) — small wrapper around the Web Share API with a clipboard
// fallback so the "Share complaint" button works on every browser.
//
//   - On mobile Chrome / Safari with HTTPS, `navigator.share()` opens the OS
//     native share sheet (WhatsApp, X, mail, etc).
//   - On every other context (desktop Firefox, dev http://localhost, etc),
//     we fall back to copying the URL to clipboard.

export interface IShareResult {
  /** 'shared' = native share sheet was used. 'copied' = clipboard fallback. 'failed' = neither worked. */
  method: 'shared' | 'copied' | 'failed';
  url:    string;
}

/**
 * Try the Web Share API; fall back to clipboard.copy.
 * Returns a Promise that resolves with the method used (so the caller can
 * show "Link copied!" vs nothing).
 */
export async function shareOrCopy(
  url:   string,
  title: string,
  text?: string,
): Promise<IShareResult> {
  // navigator.share is only available on https or localhost.
  const nav: any = typeof navigator !== 'undefined' ? navigator : null;
  if (nav?.share) {
    try {
      await nav.share({ title, text: text ?? title, url });
      return { method: 'shared', url };
    } catch {
      // User cancelled the share sheet — fall through to clipboard.
    }
  }
  try {
    if (nav?.clipboard?.writeText) {
      await nav.clipboard.writeText(url);
      return { method: 'copied', url };
    }
  } catch { /* fall through */ }

  // Last-resort fallback: legacy execCommand('copy') via a hidden textarea.
  try {
    const ta = document.createElement('textarea');
    ta.value = url;
    ta.style.position = 'fixed';
    ta.style.opacity  = '0';
    document.body.appendChild(ta);
    ta.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(ta);
    return ok ? { method: 'copied', url } : { method: 'failed', url };
  } catch {
    return { method: 'failed', url };
  }
}
