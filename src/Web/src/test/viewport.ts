/**
 * Makes window.matchMedia answer width queries ("(min-width: 1025px)", "(max-width: 767px)") as a browser window
 * of this width would, so Ant Design's Grid.useBreakpoint() sees it. Other queries do not match. jsdom has no
 * layout, so the default fake in setup.ts matches nothing (narrower than every breakpoint).
 */
export function viewportWidth(width: number): { restore: () => void } {
  const original = window.matchMedia.bind(window);
  window.matchMedia = (query: string) =>
    ({
      matches: matchesWidth(query, width),
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }) as Partial<MediaQueryList> as MediaQueryList;
  return {
    restore() {
      window.matchMedia = original;
    },
  };
}

function matchesWidth(query: string, width: number): boolean {
  const conditions = [...query.matchAll(/\((min|max)-width:\s*(\d+(?:\.\d+)?)px\)/g)];
  if (conditions.length === 0) {
    return false;
  }
  return conditions.every(([, kind, value]) =>
    kind === 'min' ? width >= Number(value) : width <= Number(value),
  );
}
