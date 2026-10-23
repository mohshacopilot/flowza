import { Card } from 'antd';
import { useId, type ReactNode } from 'react';
import styles from './SectionCard.module.css';

/**
 * A part of a screen on a white card with its own heading (spec 007 UI-09, plan 007 §5.6). The card is a region
 * named by its heading, so screen readers can jump between the parts; the heading is an <h2> under the page's <h1>.
 */
export function SectionCard({ title, children }: { title: string; children: ReactNode }) {
  const headingId = useId();
  return (
    <section className={styles.section} aria-labelledby={headingId}>
      <Card
        variant="borderless"
        title={
          <h2 id={headingId} className={styles.title}>
            {title}
          </h2>
        }
      >
        {children}
      </Card>
    </section>
  );
}
