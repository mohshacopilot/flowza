import { initI18n } from '../i18n/i18n';
import { ApiError } from './ApiError';
import { describeApiError } from './errorMessages';

const i18n = initI18n();

describe('describeApiError', () => {
  beforeAll(() => {
    // Codes are added to locales/en as services define them; the tests add their own.
    i18n.addResource(
      'en',
      'common',
      'errors.codes.users.lastPlatformAdmin',
      'The last Platform Admin must stay.',
    );
    i18n.addResource('en', 'common', 'errors.codes.required', 'Please fill in this field.');
  });

  it('FE5_translates_the_server_error_code', () => {
    const error = new ApiError({ status: 409, code: 'users.lastPlatformAdmin', detail: 'English fallback' });

    expect(describeApiError(error, i18n).message).toBe('The last Platform Admin must stay.');
  });

  it('FE5_falls_back_to_the_server_text_for_business_errors_without_a_translation', () => {
    const error = new ApiError({ status: 422, code: 'not.translated.yet', detail: 'Server says no.' });

    expect(describeApiError(error, i18n).message).toBe('Server says no.');
  });

  it.each([
    [0, 'The server could not be reached. Check your connection and try again.'],
    [401, 'Your session has ended. Please log in again.'],
    [403, 'You do not have permission to do this.'],
    [404, 'This item no longer exists.'],
    [409, 'Someone else changed this item in the meantime. Reload it and try again.'],
    [429, 'Too many attempts. Please wait a moment and try again.'],
    [500, 'Something went wrong on the server. Please try again.'],
    [503, 'Something went wrong on the server. Please try again.'],
    [418, 'Something went wrong. Please try again.'],
  ])('FE5_status_%i_without_code_gives_a_translated_message', (status, expected) => {
    expect(describeApiError(new ApiError({ status }), i18n).message).toBe(expected);
  });

  it('FE5_server_errors_never_show_the_server_text', () => {
    const error = new ApiError({ status: 500, detail: 'System.NullReferenceException at …' });

    expect(describeApiError(error, i18n).message).toBe(
      'Something went wrong on the server. Please try again.',
    );
  });

  it('PLT_05_AC2_keeps_the_reference_code', () => {
    expect(describeApiError(new ApiError({ status: 500, reference: 'cafe0001' }), i18n).reference).toBe(
      'cafe0001',
    );
  });

  it('FE5_field_errors_are_translated_when_they_are_codes_and_joined', () => {
    const error = new ApiError({
      status: 400,
      fieldErrors: { displayName: ['required'], email: ['Already used.', 'Too long.'] },
    });

    expect(describeApiError(error, i18n).fieldErrors).toEqual({
      displayName: 'Please fill in this field.',
      email: 'Already used. Too long.',
    });
  });

  it('FE5_anything_that_is_not_an_ApiError_gives_the_general_message', () => {
    expect(describeApiError(new TypeError('x'), i18n)).toEqual({
      message: 'Something went wrong. Please try again.',
      reference: undefined,
      fieldErrors: {},
    });
  });
});
