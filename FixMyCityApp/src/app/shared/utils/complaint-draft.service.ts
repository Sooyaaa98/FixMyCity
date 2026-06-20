// src/app/shared/utils/complaint-draft.service.ts
//
// Phase 8 (§2) — auto-saves the submit-complaint form locally so a citizen
// who refreshes / navigates away / closes the tab doesn't lose what they were
// typing. We deliberately use `localStorage` (not `sessionStorage`) so the
// draft survives across tab closures, which is what users expect from
// "save my work" behaviour.
//
// Privacy: we explicitly DO NOT persist photos here — they're large, can be
// PII-laden, and a browser `localStorage` quota of 5 MB would fill up fast.
//
// Quota: every draft serialises to <1 KB so even a paranoid citizen
// flipping between dozens of partials won't hit the quota; nonetheless every
// write is wrapped in try/catch because Private-mode Safari throws on
// localStorage writes.

import { Injectable } from '@angular/core';

const STORAGE_KEY = 'fmc_complaint_draft_v1';

export interface IComplaintDraft {
  citizenUserId?: number;
  categoryId?:    number | null;
  title?:         string;
  description?:   string;
  localityId?:    number | null;
  address?:       string;
  criticality?:   string;
  latitude?:      number | null;
  longitude?:     number | null;
  // ISO timestamp — used to show the user how stale the draft is, and to
  // garbage-collect drafts older than 7 days.
  savedAt:        string;
}

@Injectable({ providedIn: 'root' })
export class ComplaintDraftService {

  private static readonly TTL_MS = 7 * 24 * 60 * 60 * 1000;   // 7 days

  /** Returns the currently-saved draft, or `null` if there is none / expired. */
  load(): IComplaintDraft | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const draft = JSON.parse(raw) as IComplaintDraft;
      if (!draft?.savedAt) return null;
      const age = Date.now() - new Date(draft.savedAt).getTime();
      if (!Number.isFinite(age) || age > ComplaintDraftService.TTL_MS) {
        this.clear();
        return null;
      }
      return draft;
    } catch {
      return null;
    }
  }

  /** Persists a partial draft (no photos, no AI hints). Safe in private mode. */
  save(draft: Omit<IComplaintDraft, 'savedAt'>): void {
    try {
      const payload: IComplaintDraft = { ...draft, savedAt: new Date().toISOString() };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
    } catch {
      // Quota / private-mode — ignore silently; draft simply won't restore.
    }
  }

  /** Removes the draft (called after a successful submit or user-initiated discard). */
  clear(): void {
    try { localStorage.removeItem(STORAGE_KEY); }
    catch { /* private mode */ }
  }

  /** True if a draft is present (used to gate a "Restore draft?" prompt). */
  has(): boolean {
    return this.load() !== null;
  }
}
