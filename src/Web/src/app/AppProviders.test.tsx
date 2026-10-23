import { render, screen } from '@testing-library/react';
import { theme } from 'antd';
import { AppProviders } from './AppProviders';

function MotionSetting() {
  const { token } = theme.useToken();
  return <p>{token.motion ? 'motion on' : 'motion off'}</p>;
}

function systemAsksForReducedMotion(reduced: boolean): () => void {
  const original = window.matchMedia.bind(window);
  window.matchMedia = (query: string) =>
    ({
      matches: reduced && query.includes('prefers-reduced-motion: reduce'),
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }) as Partial<MediaQueryList> as MediaQueryList;
  return () => {
    window.matchMedia = original;
  };
}

describe('App providers', () => {
  it('UI_06_reduced_motion_switches_ant_design_animations_off_for_the_whole_app', () => {
    const restore = systemAsksForReducedMotion(true);
    render(
      <AppProviders>
        <MotionSetting />
      </AppProviders>,
    );

    expect(screen.getByText('motion off')).toBeInTheDocument();
    restore();
  });

  it('animations_stay_on_when_the_system_does_not_ask_for_reduced_motion', () => {
    const restore = systemAsksForReducedMotion(false);
    render(
      <AppProviders>
        <MotionSetting />
      </AppProviders>,
    );

    expect(screen.getByText('motion on')).toBeInTheDocument();
    restore();
  });
});
