import createClient from 'openapi-fetch';
import type { components, paths } from './schema';

export type Ledger = components['schemas']['LedgerView'];
export type Group = components['schemas']['GroupSummary'];
export type Due = components['schemas']['DueView'];
export type Cycle = components['schemas']['CycleView'];
export type Payment = components['schemas']['PaymentView'];
export type CreateGroup = components['schemas']['CreateGroupRequest'];
export type RecordPayment = components['schemas']['RecordPaymentRequest'];
export type RecordPayout = components['schemas']['RecordPayoutRequest'];
export type ReversePayment = components['schemas']['ReversePaymentRequest'];

export class ApiError extends Error {
  constructor(public status: number, message: string, public code = 'request_failed') { super(message); }
}

function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (!result.response.ok || result.data === undefined) {
    const error = result.error as { detail?: string; title?: string; code?: string } | undefined;
    throw new ApiError(result.response.status, error?.detail ?? (result.response.status === 401 ? 'Session expired.' : 'The request could not be completed.'), error?.code ?? error?.title);
  }
  return result.data;
}

export function createApi(baseUrl: string, fetcher?: typeof fetch) {
  const client = createClient<paths>({ baseUrl, fetch: fetcher });
  let tokenProvider: (() => Promise<string | null>) | undefined;
  let unauthorized: (() => void) | undefined;
  client.use({
    async onRequest({ request }) {
      if (new URL(request.url).pathname !== '/api/dev/sign-in') {
        const token = await tokenProvider?.();
        if (token) request.headers.set('Authorization', `Bearer ${token}`);
      }
      return request;
    },
    onResponse({ response }) { if (response.status === 401) unauthorized?.(); return response; },
  });
  return {
    authenticate(provider: () => Promise<string | null>, onUnauthorized: () => void) { tokenProvider = provider; unauthorized = onUnauthorized; },
    async signIn(profile = 'primary') { return unwrap(await client.POST('/api/dev/sign-in', { body: { profile } })); },
    async me() { return unwrap(await client.GET('/api/me')); },
    async groups(page = 1) { return unwrap(await client.GET('/api/groups', { params: { query: { page, pageSize: 20 } } })); },
    async ledger(id: string) { return unwrap(await client.GET('/api/groups/{id}', { params: { path: { id } } })); },
    async create(body: CreateGroup) { return unwrap(await client.POST('/api/groups', { body })); },
    async addMember(id: string, body: components['schemas']['AddMemberRequest']) {
      return unwrap(await client.POST('/api/groups/{id}/members', { params: { path: { id } }, body }));
    },
    async activate(id: string, body: components['schemas']['ActivateRequest']) {
      return unwrap(await client.POST('/api/groups/{id}/activate', { params: { path: { id } }, body }));
    },
    async payment(id: string, cycleId: string, memberId: string, body: RecordPayment) {
      return unwrap(await client.POST('/api/groups/{id}/cycles/{cycleId}/members/{memberId}/payments', { params: { path: { id, cycleId, memberId } }, body }));
    },
    async reverse(id: string, paymentId: string, body: ReversePayment) {
      return unwrap(await client.POST('/api/groups/{id}/payments/{paymentId}/reverse', { params: { path: { id, paymentId } }, body }));
    },
    async payout(id: string, cycleId: string, body: RecordPayout) {
      return unwrap(await client.POST('/api/groups/{id}/cycles/{cycleId}/payout', { params: { path: { id, cycleId } }, body }));
    },
  };
}
