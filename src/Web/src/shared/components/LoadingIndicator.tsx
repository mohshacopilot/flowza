import { Spin } from 'antd';
import styles from './LoadingIndicator.module.css';

/**
 * A spinner that screen readers announce (constitution 12.2): a status region with hidden text. Ant Design's
 * Spin is a plain element, which may not carry an aria-label itself (found by the axe checks, T029).
 */
export function LoadingIndicator({ label, size }: { label: string; size?: 'small' | 'large' }) {
  return (
    <div role="status">
      <Spin {...(size ? { size } : {})} />
      <span className={styles.visuallyHidden}>{label}</span>
    </div>
  );
}
