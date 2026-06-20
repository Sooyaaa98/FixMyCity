// src/app/shared/utils/export.util.ts
//
// Phase 8 (§10) — small standalone CSV exporter and clipboard / file helpers.
// No third-party dep needed; just a quoted-field serialiser + Blob download.
//
// Quirks worth knowing:
//   - We always emit a UTF-8 BOM. Excel on Windows interprets unprefixed UTF-8
//     CSV as the legacy "ANSI" code page and mangles non-ASCII characters
//     (commonly seen with Indian-language locality names and addresses).
//   - Cells that contain commas, double quotes or line breaks are quoted,
//     and any inner double quotes are doubled. This matches RFC 4180.

/**
 * Convert a flat array of records into RFC-4180 CSV text.
 *
 * @param rows       Array of objects. Keys of the FIRST row determine column order.
 * @param columns    Optional explicit column order + nicer header labels.
 *                   `{ key: 'complaintId', label: 'Complaint ID' }`
 * @param withBom    Prefix the BOM (default true — keeps Excel happy).
 */
export function toCsv<T extends Record<string, any>>(
  rows:    T[],
  columns?: Array<{ key: keyof T & string; label?: string }>,
  withBom: boolean = true,
): string {
  if (!rows || rows.length === 0) {
    return withBom ? '﻿' : '';
  }
  // Falling back to inferred columns: build it as the same widened type so
  // .label access doesn't narrow to never on the default branch.
  const cols: Array<{ key: string; label?: string }> =
    columns
      ? (columns as Array<{ key: string; label?: string }>)
      : Object.keys(rows[0]).map(k => ({ key: k, label: undefined }));
  const headers = cols.map(c => escapeCell(c.label ?? c.key));
  const lines: string[] = [headers.join(',')];
  for (const r of rows) {
    lines.push(cols.map(c => escapeCell(format(r[c.key]))).join(','));
  }
  return (withBom ? '﻿' : '') + lines.join('\r\n');
}

/**
 * Trigger a browser file download with the given filename and content.
 * Caller is responsible for picking the right MIME type ('text/csv' for CSV).
 */
export function downloadFile(
  filename: string,
  content:  string | Blob,
  mime:     string = 'text/csv;charset=utf-8',
): void {
  const blob = typeof content === 'string' ? new Blob([content], { type: mime }) : content;
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = filename;
  // Append → click → remove. The append is required on Firefox.
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  // Give the browser a tick to finish the download before revoking.
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/**
 * One-line convenience: serialise + download.
 */
export function exportCsv<T extends Record<string, any>>(
  filename: string,
  rows:     T[],
  columns?: Array<{ key: keyof T & string; label?: string }>,
): void {
  downloadFile(filename, toCsv(rows, columns), 'text/csv;charset=utf-8');
}

// ── Private helpers ─────────────────────────────────────────────────────────

function escapeCell(v: string): string {
  if (v == null) return '';
  if (/[",\r\n]/.test(v)) {
    return `"${v.replace(/"/g, '""')}"`;
  }
  return v;
}

function format(v: any): string {
  if (v == null) return '';
  if (v instanceof Date) return v.toISOString();
  if (typeof v === 'object') return JSON.stringify(v);
  return String(v);
}
