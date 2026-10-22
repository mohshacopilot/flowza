import type { ThemeConfig } from 'antd';

/**
 * The one place for Ant Design theme tokens (tech discussion T10, FE4). Components never set colours or fonts
 * themselves. From M3 the brand colour comes from the installation settings (PLT-08).
 */
export const theme: ThemeConfig = {
  // Tokens are also CSS variables (--flowza-color-primary, …), so layout CSS can use them too.
  cssVar: { prefix: 'flowza' },
  token: {
    colorPrimary: '#1f5fbf',
    colorInfo: '#1f5fbf',
    borderRadius: 6,
    // Help and secondary texts: Ant Design's default (45 % black) is too pale on the grey page background for
    // WCAG AA contrast (4.5:1); 65 % black passes on white and on the page grey (found by the axe checks, T029).
    colorTextSecondary: 'rgba(0, 0, 0, 0.65)',
    colorTextDescription: 'rgba(0, 0, 0, 0.65)',
    // Status colours dark enough for text on white and on their own light backgrounds (tags, alerts, danger
    // buttons; at least 4.5:1). The light backgrounds and borders are set too: derived from these darker colours
    // they would turn muddy.
    colorSuccess: '#2e7d32',
    colorSuccessBg: '#edf7ed',
    colorSuccessBorder: '#a5d6a7',
    colorWarning: '#a35a00',
    colorWarningBg: '#fff8e1',
    colorWarningBorder: '#ffd27f',
    colorError: '#c62828',
    colorErrorBg: '#fdecea',
    colorErrorBorder: '#f5b7b1',
    fontFamily:
      "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Noto Sans', 'Noto Sans Devanagari', 'Noto Sans Tamil', sans-serif",
  },
  components: {
    Layout: {
      headerBg: '#ffffff',
      siderBg: '#ffffff',
      headerPadding: '0 24px',
    },
  },
};
