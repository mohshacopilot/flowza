import type { ParseKeys } from 'i18next';
import type { UIMatch } from 'react-router';

/** What a route tells the frame about its page (React Router's `handle`). */
export interface PageHandle {
  titleKey: ParseKeys;
}

/** The handle of a route whose page has this title (spec 007 UI-03; plan 007 §5.5). */
export const titled = (titleKey: ParseKeys): PageHandle => ({ titleKey });

function isPageHandle(handle: unknown): handle is PageHandle {
  return typeof handle === 'object' && handle !== null && 'titleKey' in handle;
}

/** The title key of the deepest matched route that has one. O(route depth). */
export function pageTitleKey(matches: readonly UIMatch[]): ParseKeys | undefined {
  for (let index = matches.length - 1; index >= 0; index--) {
    const handle = matches[index]?.handle;
    if (isPageHandle(handle)) {
      return handle.titleKey;
    }
  }
  return undefined;
}
