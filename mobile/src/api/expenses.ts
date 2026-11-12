import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { tripKeys } from '@/api/planning';
import { api } from '@/lib/api';
import type { Currency, ExchangeRate, Expense, ExpenseInput, Trip, TripExpenses, TripPayment } from '@/lib/types';

const HOUR = 60 * 60 * 1000;

export const expenseKeys = {
  // Nested under the trip's own key, so anything that reloads the trip (its hub events, pull to refresh)
  // reloads its expenses too.
  forTrip: (tripId: string) => [...tripKeys.detail(tripId), 'expenses'] as const,
  currencies: ['currencies'] as const,
  rate: (from: string, to: string, date: string) => ['exchange-rate', from, to, date] as const,
};

/**
 * Expenses, payments, balances and settle-up suggestions for a trip. Persisted like trips (default), so the
 * ledger is readable without signal on the trip itself; refreshed whenever the trip hub says it changed.
 */
export function useTripExpenses(tripId: string) {
  return useQuery({
    queryKey: expenseKeys.forTrip(tripId),
    queryFn: ({ signal }) => api<TripExpenses>(`/api/trips/${tripId}/expenses`, { signal }),
  });
}

/** Currencies an expense can be entered in. Rarely changes. */
export function useCurrencies() {
  return useQuery({
    queryKey: expenseKeys.currencies,
    queryFn: ({ signal }) => api<Currency[]>('/api/trips/currencies', { signal }),
    staleTime: 24 * HOUR,
  });
}

/** The provider's (ECB) rate for `from` → `to` on `date`; `date` in the result is the rate's actual date. */
export function useExchangeRate(from: string, to: string, date: string, enabled: boolean) {
  return useQuery({
    queryKey: expenseKeys.rate(from, to, date),
    queryFn: ({ signal }) => api<ExchangeRate>('/api/trips/exchange-rate', { query: { from, to, date }, signal }),
    enabled: enabled && from !== to,
    staleTime: 12 * HOUR,
    retry: 1,
  });
}

/** After any ledger change: the expenses and the trip's summary card both change. */
function useRefreshTrip(tripId: string) {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: tripKeys.detail(tripId) }); // prefix: covers the expenses query
}

export function useSaveExpense(tripId: string) {
  const refresh = useRefreshTrip(tripId);
  return useMutation({
    mutationFn: ({ id, input }: { id: string | null; input: ExpenseInput }) =>
      id
        ? api<Expense>(`/api/trips/${tripId}/expenses/${id}`, { method: 'PUT', body: input })
        : api<Expense>(`/api/trips/${tripId}/expenses`, { method: 'POST', body: input }),
    onSettled: refresh,
  });
}

export function useDeleteExpense(tripId: string) {
  const refresh = useRefreshTrip(tripId);
  return useMutation({
    mutationFn: (expenseId: string) => api<void>(`/api/trips/${tripId}/expenses/${expenseId}`, { method: 'DELETE' }),
    onSettled: refresh,
  });
}

export function useRecordPayment(tripId: string) {
  const refresh = useRefreshTrip(tripId);
  return useMutation({
    mutationFn: (body: { from: string; to: string; amount: number; date: string }) =>
      api<TripPayment>(`/api/trips/${tripId}/payments`, { method: 'POST', body }),
    onSettled: refresh,
  });
}

export function useDeletePayment(tripId: string) {
  const refresh = useRefreshTrip(tripId);
  return useMutation({
    mutationFn: (paymentId: string) => api<void>(`/api/trips/${tripId}/payments/${paymentId}`, { method: 'DELETE' }),
    onSettled: refresh,
  });
}

/** Owner only, and only while nothing is logged (409 `Trip.CurrencyLocked` otherwise). */
export function useSetTripCurrency(tripId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (currency: string) => api<Trip>(`/api/trips/${tripId}/currency`, { method: 'PUT', body: { currency } }),
    onSuccess: (trip) => {
      qc.setQueryData(tripKeys.detail(tripId), trip);
      void qc.invalidateQueries({ queryKey: expenseKeys.forTrip(tripId) });
      void qc.invalidateQueries({ queryKey: tripKeys.all, exact: true });
      void qc.invalidateQueries({ queryKey: ['trips', 'group'] });
    },
  });
}
