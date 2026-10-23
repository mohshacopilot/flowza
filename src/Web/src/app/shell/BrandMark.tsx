import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useInstallation } from '../../shared/installation/useInstallation';
import styles from './BrandMark.module.css';

/**
 * The installation's initials mark and name, linking to the home page (spec 007 UI-08, C6). A long name is
 * shortened with "…" on screen; the full name stays in the tooltip (title) and in the link's accessible name.
 * Until the name has loaded (or if it cannot be loaded) only the mark's outline shows, so no wrong name flashes up.
 */
export function BrandMark() {
  const { t } = useTranslation();
  const installation = useInstallation();
  const brand = installation.data;

  return (
    <Link
      to="/"
      className={styles.brand}
      aria-label={brand ? t('shell.homeLink', { name: brand.name }) : t('shell.homeLinkWithoutName')}
      {...(brand ? { title: brand.name } : {})}
    >
      <span className={brand ? styles.mark : styles.markPending} aria-hidden="true">
        {brand?.initials}
      </span>
      {brand && <span className={styles.name}>{brand.name}</span>}
    </Link>
  );
}
