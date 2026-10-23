import type { ThemeConfig } from 'antd';
import { readableTextOn } from './contrast';
import type designValues from './designValues.json';

export type DesignValues = typeof designValues;

export interface ThemeOptions {
  /** The user asked their system for reduced motion: Ant Design's animations are switched off. */
  reducedMotion?: boolean;
}

/**
 * Adapter from our design values to Ant Design's theme (plan 007 section 5.1, pattern "Adapter"): the only place
 * that knows Ant Design's token names. Tokens are also CSS variables (--flowza-color-primary, …), so layout CSS
 * can read them too.
 */
export function createAntdTheme(values: DesignValues, options: ThemeOptions = {}): ThemeConfig {
  const onBrand = readableTextOn(values.brand, values.onColour);
  return {
    cssVar: { prefix: 'flowza' },
    token: {
      colorPrimary: values.brand,
      colorInfo: values.brand,
      colorLink: values.brand,
      colorTextLightSolid: onBrand,
      colorText: values.neutral.text,
      colorTextHeading: values.neutral.text,
      // Help and secondary texts must reach AA (4.5 : 1) on the page and on cards (found by the axe checks, T029).
      colorTextSecondary: values.neutral.textSecondary,
      colorTextDescription: values.neutral.textSecondary,
      colorBgLayout: values.neutral.page,
      colorBgContainer: values.neutral.surface,
      colorBorderSecondary: values.neutral.border,
      // Status colours dark enough for text on white and on their own light backgrounds (tags, alerts, danger
      // buttons); the light backgrounds and borders are set too, because derived ones turn muddy.
      colorSuccess: values.status.success.text,
      colorSuccessBg: values.status.success.background,
      colorSuccessBorder: values.status.success.border,
      colorWarning: values.status.warning.text,
      colorWarningBg: values.status.warning.background,
      colorWarningBorder: values.status.warning.border,
      colorError: values.status.error.text,
      colorErrorBg: values.status.error.background,
      colorErrorBorder: values.status.error.border,
      fontFamily: values.font.family,
      fontFamilyCode: values.font.familyCode,
      fontSize: values.font.size.base,
      // The spacing scale (UI-02): Ant Design's margins and paddings, and so the --flowza-margin-* and
      // --flowza-padding-* variables the screens' CSS reads, come from the design values.
      marginXXS: values.space.xs,
      marginXS: values.space.sm,
      marginSM: values.space.md,
      margin: values.space.lg,
      marginLG: values.space.xl,
      marginXL: values.space.xxl,
      paddingXXS: values.space.xs,
      paddingXS: values.space.sm,
      paddingSM: values.space.md,
      padding: values.space.lg,
      paddingLG: values.space.xl,
      paddingXL: values.space.xxl,
      borderRadius: values.radius.control,
      borderRadiusLG: values.radius.card,
      boxShadowTertiary: values.shadow.card,
      lineWidthFocus: values.focusRing,
      motion: !options.reducedMotion,
      // Ant Design's md and lg breakpoints become our tablet and desktop widths (C3, C4), so Grid.useBreakpoint()
      // answers in our terms: md = tablet and wider, lg = desktop.
      screenSMMax: values.breakpoints.tablet - 1,
      screenMD: values.breakpoints.tablet,
      screenMDMin: values.breakpoints.tablet,
      screenMDMax: values.breakpoints.desktop - 1,
      screenLG: values.breakpoints.desktop,
      screenLGMin: values.breakpoints.desktop,
    },
    components: {
      Layout: {
        headerBg: values.neutral.surface,
        headerHeight: values.layout.headerHeight,
        bodyBg: values.neutral.page,
        siderBg: values.navigation.background,
        triggerBg: values.navigation.background,
        triggerColor: values.navigation.text,
        headerPadding: `0 ${values.space.xl}px`,
      },
      Menu: {
        // The selected item is the brand colour with the text chosen for it (UI-07), in the light and the dark menu:
        // Ant Design's own derived tint gives only 2.7 : 1 with the teal brand (found by the axe checks).
        itemSelectedBg: values.brand,
        itemSelectedColor: onBrand,
        darkItemBg: values.navigation.background,
        darkSubMenuItemBg: values.navigation.background,
        darkPopupBg: values.navigation.background,
        darkItemColor: values.navigation.text,
        darkItemHoverColor: values.navigation.textActive,
        darkItemHoverBg: values.navigation.raised,
        collapsedWidth: values.layout.navigationCollapsedWidth,
        darkItemSelectedBg: values.brand,
        darkItemSelectedColor: onBrand,
        darkGroupTitleColor: values.navigation.groupLabel,
      },
      Avatar: {
        // The user's initials sit on the brand colour with the text chosen for it (UI-07), like the installation
        // mark; Ant Design's default grey gives white text only 2.3 : 1.
        colorTextPlaceholder: values.brand,
      },
      Card: {
        borderRadiusLG: values.radius.card,
      },
    },
  };
}
