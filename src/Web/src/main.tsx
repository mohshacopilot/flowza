import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { AppProviders } from './app/AppProviders';
import { routes } from './app/routes';
import { initI18n } from './shared/i18n/i18n';

const i18n = initI18n();
document.title = i18n.t('app.name');

const router = createBrowserRouter(routes);
const root = document.getElementById('root');
if (!root) {
  throw new Error('The page has no #root element.');
}

createRoot(root).render(
  <StrictMode>
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
