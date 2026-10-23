import { useGetInstallation } from '../api/generated/identity/installation/installation';

/** The name changes only at installation (C7); Identity lets the browser keep it 5 minutes too. */
const installationStaleTime = 5 * 60_000;

/**
 * The installation's name and initials mark from Identity (spec 007 UI-08; plan 007 §5.3). Identity computes the
 * initials (C6), so the frame and the login pages always show the same mark.
 */
export function useInstallation() {
  return useGetInstallation({ query: { staleTime: installationStaleTime } });
}
