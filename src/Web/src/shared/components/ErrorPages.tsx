import { Button, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { isRouteErrorResponse, useRouteError } from 'react-router';
import { ApiError, referenceOf } from '../api/ApiError';
import { StatusPage } from './StatusPage';

export function ForbiddenPage() {
  const { t } = useTranslation();
  return (
    <StatusPage
      kind="forbidden"
      title={t('errors.forbidden.title')}
      message={t('errors.forbidden.message')}
    />
  );
}

export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <StatusPage kind="notFound" title={t('errors.notFound.title')} message={t('errors.notFound.message')} />
  );
}

/** The error screen (PLT-05): shows the server's reference code, so support can find the trace in the logs. */
export function ErrorPage({ error }: { error: unknown }) {
  const { t } = useTranslation();
  const reference = referenceOf(error);
  return (
    <StatusPage
      kind="error"
      title={t('errors.page.title')}
      message={t('errors.page.message')}
      actions={
        <Button
          onClick={() => {
            window.location.reload();
          }}
        >
          {t('errors.page.retry')}
        </Button>
      }
    >
      {reference && (
        <>
          <Typography.Paragraph strong>{t('errors.page.reference', { reference })}</Typography.Paragraph>
          <Typography.Paragraph type="secondary">{t('errors.page.referenceHelp')}</Typography.Paragraph>
        </>
      )}
    </StatusPage>
  );
}

/** Chooses the page for an error: no access and not found get their own pages, everything else the error page. */
export function PageForError({ error }: { error: unknown }) {
  const status =
    error instanceof ApiError ? error.status : isRouteErrorResponse(error) ? error.status : undefined;
  if (status === 403) {
    return <ForbiddenPage />;
  }
  if (status === 404) {
    return <NotFoundPage />;
  }
  return <ErrorPage error={error} />;
}

/** Error boundary of a route (React Router's ErrorBoundary). */
export function RouteErrorPage() {
  return <PageForError error={useRouteError()} />;
}
