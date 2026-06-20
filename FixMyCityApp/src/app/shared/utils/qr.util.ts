// src/app/shared/utils/qr.util.ts
//
// Phase 8 (§8) — return a QR code image URL for any payload (a complaint
// detail page URL, a certificate verification code, etc).
//
// We use the QRServer API (https://api.qrserver.com) which is:
//   - free, no signup
//   - returns a PNG directly (we can `<img [src]>` it)
//   - serves the image with CORS headers so canvas + downloading works
//
// If you later add an offline-first PWA build, swap this to an inline
// `qrcode` npm module — the call sites only depend on `qrUrl(text)` so the
// migration is one file.

const ENDPOINT = 'https://api.qrserver.com/v1/create-qr-code/';

/**
 * Build a QR-code image URL.
 *
 * @param text  Anything ≤ 900 chars. Almost always a URL.
 * @param size  Pixel dimension (square). Default 200.
 * @param ecc   Error-correction level. 'L'/'M'/'Q'/'H' — H is the densest
 *              but tolerates the most damage / overlays. Default 'M'.
 */
export function qrUrl(text: string, size: number = 200, ecc: 'L'|'M'|'Q'|'H' = 'M'): string {
  const safeSize = Math.max(64, Math.min(1024, size));
  const params = new URLSearchParams({
    size:           `${safeSize}x${safeSize}`,
    data:           text,
    'ecc':          ecc,
    margin:         '0',
  });
  return `${ENDPOINT}?${params.toString()}`;
}

/**
 * Trigger a QR-code download as PNG. Fetches the image through the same API
 * with a `download=1` parameter and a tweaked filename.
 */
export function downloadQr(text: string, filename: string = 'qr.png'): void {
  const url = qrUrl(text, 512, 'H');   // bigger + denser for printing
  // We can't set Content-Disposition cross-origin, so we re-route through a
  // hidden anchor with `download=`.
  const a = document.createElement('a');
  a.href     = url;
  a.download = filename;
  a.target   = '_blank';
  a.rel      = 'noopener';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
}
