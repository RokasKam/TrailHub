import type { AppIcon } from '@/lib/icons';
import type { ExpenseCategory } from '@/lib/types';

const formatters = new Map<string, Intl.NumberFormat>();

/** "€12.40" in the device's locale. Unknown codes fall back to "12.40 XYZ". */
export function formatMoney(amount: number, currency: string) {
  let f = formatters.get(currency);
  if (!f) {
    try {
      f = new Intl.NumberFormat(undefined, { style: 'currency', currency });
    } catch {
      return `${amount.toFixed(2)} ${currency}`;
    }
    formatters.set(currency, f);
  }
  return f.format(amount);
}

/** Money as integer cents, so sums and differences are exact. */
export const toCents = (amount: number) => Math.round(amount * 100 + (amount >= 0 ? 1e-7 : -1e-7));
export const fromCents = (cents: number) => cents / 100;

/**
 * Parses what someone typed into a number field, accepting "," or "." as the decimal separator (and a
 * thousands separator before it, e.g. "1.234,50"). Returns null for anything that isn't a plain positive
 * number with at most `decimals` decimal places.
 */
export function parseDecimal(text: string, decimals = 2): number | null {
  let s = text.replace(/[\s ']/g, '');
  const last = Math.max(s.lastIndexOf(','), s.lastIndexOf('.'));
  if (last >= 0) s = s.slice(0, last).replace(/[.,]/g, '') + '.' + s.slice(last + 1);
  if (!new RegExp(`^\\d+(\\.\\d{0,${decimals}})?$|^\\.\\d{1,${decimals}}$`).test(s)) return null;
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
}

/** `amount × rate` in the trip currency, rounded half away from zero like the server. */
export const convert = (amount: number, rate: number) => fromCents(Math.round(amount * rate * 100 + 1e-7));

/** A number for an input field: "12.5" → "12.50", with the device's decimal separator. */
export function amountInputText(amount: number) {
  const separator = (1.5).toLocaleString().includes(',') ? ',' : '.';
  return amount.toFixed(2).replace('.', separator);
}

/** "30 Jun" for a yyyy-MM-dd date. */
export function formatShortDate(dateOnly: string) {
  const [y, m, d] = dateOnly.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
}

export const EXPENSE_CATEGORIES: { value: ExpenseCategory; label: string; icon: AppIcon; color: string }[] = [
  { value: 'Accommodation', label: 'Stay', icon: 'bed.double.fill', color: '#7C3AED' },
  { value: 'Food', label: 'Food', icon: 'fork.knife', color: '#EA580C' },
  { value: 'Transport', label: 'Transport', icon: 'car.fill', color: '#2563EB' },
  { value: 'Gear', label: 'Gear', icon: 'backpack.fill', color: '#1F7A4D' },
  { value: 'Fees', label: 'Fees', icon: 'ticket', color: '#D97706' },
  { value: 'Other', label: 'Other', icon: 'tag.fill', color: '#64748B' },
];

export const categoryInfo = (category: ExpenseCategory) => EXPENSE_CATEGORIES.find((c) => c.value === category) ?? EXPENSE_CATEGORIES[EXPENSE_CATEGORIES.length - 1];

/** Colour for a balance: green when others owe you, red when you owe, grey when settled. */
export function netColor(net: number, theme: { accent: string; danger: string; textSecondary: string }) {
  const cents = toCents(net);
  return cents > 0 ? theme.accent : cents < 0 ? theme.danger : theme.textSecondary;
}
