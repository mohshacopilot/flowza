import { createAntdTheme, type DesignValues } from './antdTheme';
import { contrastRatio } from './contrast';
import designValues from './designValues.json';

describe('Ant Design theme from the design values', () => {
  const theme = createAntdTheme(designValues);

  it('UI_02_brand_colour_and_font_come_from_the_design_values', () => {
    expect(theme.token?.colorPrimary).toBe(designValues.brand);
    expect(theme.token?.colorLink).toBe(designValues.brand);
    expect(theme.token?.fontFamily).toBe(designValues.font.family);
    expect(theme.token?.fontSize).toBe(designValues.font.size.base);
    expect(theme.token?.borderRadius).toBe(designValues.radius.control);
  });

  it('UI_02_keeps_the_flowza_css_variables_for_layout_css', () => {
    expect(theme.cssVar).toEqual({ prefix: 'flowza' });
  });

  it('UI_03_navigation_is_dark_with_the_brand_colour_on_the_selected_item', () => {
    expect(theme.components?.Layout?.siderBg).toBe(designValues.navigation.background);
    expect(theme.components?.Menu?.darkItemBg).toBe(designValues.navigation.background);
    expect(theme.components?.Menu?.darkItemSelectedBg).toBe(designValues.brand);
  });

  it('UI_07_AC1_text_on_brand_surfaces_is_chosen_for_contrast', () => {
    const lightBrand: DesignValues = { ...designValues, brand: '#ffd27f' };
    const onLight = createAntdTheme(lightBrand).token?.colorTextLightSolid;

    expect(onLight).toBe(designValues.onColour.dark);
    expect(contrastRatio(onLight ?? '', lightBrand.brand)).toBeGreaterThanOrEqual(4.5);
    expect(theme.token?.colorTextLightSolid).toBe(designValues.onColour.light);
    expect(theme.components?.Menu?.darkItemSelectedColor).toBe(theme.token?.colorTextLightSolid);
  });

  it('UI_06_the_selected_menu_item_is_readable_in_the_light_and_the_dark_menu', () => {
    const menu = theme.components?.Menu;
    expect(menu?.itemSelectedBg).toBe(designValues.brand);
    expect(menu?.darkItemSelectedBg).toBe(designValues.brand);
    for (const text of [menu?.itemSelectedColor, menu?.darkItemSelectedColor]) {
      expect(contrastRatio(text ?? '', designValues.brand)).toBeGreaterThanOrEqual(4.5);
    }
  });

  it('UI_06_navigation_texts_reach_4_5_to_1_on_the_dark_navigation', () => {
    const navigation = designValues.navigation;
    for (const text of [navigation.text, navigation.textActive, navigation.groupLabel]) {
      expect(contrastRatio(text, navigation.background)).toBeGreaterThanOrEqual(4.5);
    }
  });

  it('UI_06_page_texts_reach_4_5_to_1_on_the_page_and_on_cards', () => {
    const { text, textSecondary, page, surface } = designValues.neutral;
    for (const foreground of [text, textSecondary]) {
      for (const background of [page, surface]) {
        expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(4.5);
      }
    }
  });

  it('UI_06_status_texts_reach_4_5_to_1_on_white_and_on_their_own_background', () => {
    for (const status of Object.values(designValues.status)) {
      expect(contrastRatio(status.text, designValues.neutral.surface)).toBeGreaterThanOrEqual(4.5);
      expect(contrastRatio(status.text, status.background)).toBeGreaterThanOrEqual(4.5);
    }
  });

  it('animations_are_on_unless_the_user_prefers_reduced_motion', () => {
    expect(theme.token?.motion).toBe(true);
    expect(createAntdTheme(designValues, { reducedMotion: true }).token?.motion).toBe(false);
  });
});
