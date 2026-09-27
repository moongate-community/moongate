import { readFile } from 'node:fs/promises';
import path from 'node:path';

// The authored coverage page carries this marker; the importer replaces it.
export const coverageMarker = '<!-- coverage-summary -->';

// Reads the merged report written by scripts/coverage.sh (or the CI artifact of the
// same name). Returns null when no report is present.
export async function readCoverage({ directory, commit }) {
  let summary, cobertura;
  try {
    summary = await readFile(path.join(directory, 'Summary.md'), 'utf8');
    cobertura = await readFile(path.join(directory, 'Cobertura.xml'), 'utf8');
  } catch (error) {
    if (error.code === 'ENOENT') return null;
    throw error;
  }
  const timestamp = Number(cobertura.match(/<coverage\b[^>]*\btimestamp="(\d+)"/)?.[1]);
  const measured = Number.isFinite(timestamp) ? new Date(timestamp * 1000).toISOString().slice(0, 10) : null;
  const source = [measured && `on ${measured}`, commit && `at commit \`${commit}\``].filter(Boolean).join(' ');
  const markdown = [
    source ? `Measured by the full test suite ${source}.` : 'Measured by the full test suite.',
    '',
    // ReportGenerator titles its summary; the page already has one.
    summary.replace(/^# .*\n+/, '').trim(),
    '',
    '[Open the full report](/coverage/) to browse coverage by class and source line.',
  ].join('\n');
  return { directory, markdown };
}

export const missingCoverageMarkdown =
  'No coverage report was available when this site was built. Run `scripts/coverage.sh all` for a local report.';
