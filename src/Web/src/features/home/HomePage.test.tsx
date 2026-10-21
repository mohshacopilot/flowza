import { screen } from '@testing-library/react';
import { routes } from '../../app/routes';
import { renderRoutes } from '../../test/render';

describe('Home page', () => {
  it('NFR_10_home_page_text_comes_from_the_English_translation_file', async () => {
    renderRoutes(routes, '/');

    expect(await screen.findByRole('heading', { level: 1, name: 'Welcome' })).toBeInTheDocument();
    expect(screen.getByText('Choose a task from the menu to get started.')).toBeInTheDocument();
  });
});
