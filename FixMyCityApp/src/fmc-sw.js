// FixMyCity — minimal service worker (PWA support, §19)
// 2026-05-20
//
// Strategy:
//   • Precache the shell so the user gets an "app launched" feel offline.
//   • Network-first for everything else. If offline, fall back to the cache
//     and otherwise return a hand-built /offline.html so the user knows what
//     happened instead of staring at the browser's stock error page.
//
// We DELIBERATELY do not cache /api/* responses. The complaints data must
// always be live — caching it would create stale state that's worse than no
// state at all.

const VERSION = 'fmc-v1';
const SHELL = [
  '/',
  '/index.html',
  '/manifest.webmanifest',
  '/favicon.ico',
];

self.addEventListener('install', (ev) => {
  self.skipWaiting();
  ev.waitUntil(
    caches.open(VERSION).then(c => c.addAll(SHELL).catch(() => {}))
  );
});

self.addEventListener('activate', (ev) => {
  ev.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== VERSION).map(k => caches.delete(k))))
  );
  self.clients.claim();
});

self.addEventListener('fetch', (ev) => {
  const req = ev.request;
  // Only GET requests are cacheable.
  if (req.method !== 'GET') return;

  // Never touch API calls — they must always reach the network.
  const url = new URL(req.url);
  if (url.pathname.startsWith('/api/')) return;

  ev.respondWith(
    fetch(req)
      .then(resp => {
        // Stash a copy in the cache for offline next time.
        const copy = resp.clone();
        caches.open(VERSION).then(c => c.put(req, copy).catch(() => {}));
        return resp;
      })
      .catch(() =>
        caches.match(req).then(cached => cached || caches.match('/index.html')))
  );
});
