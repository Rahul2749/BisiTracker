import { describe, expect, it } from 'vitest';
import { indiaDate, parseRupees, validDate } from './money';

describe('money input', () => {
  it('converts decimal rupees to integer paise without floating point rounding', () => {
    expect(parseRupees('1000')).toBe(100000);
    expect(parseRupees('0.29')).toBe(29);
    expect(parseRupees('1.1')).toBe(110);
    expect(parseRupees(' 1234.56 ')).toBe(123456);
  });
  it.each(['', '0', '-1', '1.001', '1,000', '1e3', 'NaN', 'Infinity', '999999999999999999'])('rejects invalid or unsafe input %s', value => {
    expect(parseRupees(value)).toBeNull();
  });
});

it('uses the Indian calendar date at the UTC boundary', () => {
  expect(indiaDate(new Date('2026-10-13T20:00:00Z'))).toBe('2026-10-14');
});

it('rejects calendar rollovers rather than silently changing dates', () => {
  expect(validDate('2026-02-30')).toBe(false);
  expect(validDate('2024-02-29')).toBe(true);
  expect(validDate('2026-13-01')).toBe(false);
});
