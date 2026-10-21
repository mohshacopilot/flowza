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
