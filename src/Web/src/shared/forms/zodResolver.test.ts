import { z } from 'zod';
import { zodResolver } from './zodResolver';

const schema = z.object({
  name: z.string().trim().min(1, 'name.required').max(5, 'name.tooLong'),
  code: z.string().regex(/^\d+$/, 'code.digits'),
});

const translate = (key: string) => `T(${key})`;
const resolve = (values: z.infer<typeof schema>) =>
  zodResolver(schema, translate)(values, undefined, { fields: {}, shouldUseNativeValidation: false });

describe('zodResolver', () => {
  it('passes the parsed values (trimmed) when everything is valid', async () => {
    const result = await resolve({ name: '  abc ', code: '42' });

    expect(result.errors).toEqual({});
    expect(result.values).toEqual({ name: 'abc', code: '42' });
  });

  it('translates the message of each invalid field', async () => {
    const result = await resolve({ name: '', code: 'x' });

    expect(result.values).toEqual({});
    expect(result.errors).toMatchObject({
      name: { message: 'T(name.required)' },
      code: { message: 'T(code.digits)' },
    });
  });

  it('keeps only the first problem of a field', async () => {
    const twoProblems = z.object({ code: z.string().min(3, 'first').regex(/^\d+$/, 'second') });
    const result = await zodResolver(twoProblems, translate)({ code: 'x' }, undefined, {
      fields: {},
      shouldUseNativeValidation: false,
    });

    expect(result.errors).toMatchObject({ code: { message: 'T(first)' } });
  });
});
