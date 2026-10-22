import { expect, it } from 'vitest';
import { ApiError, createApi } from './client';

it('preserves the operation ID and amount when the caller retries a lost response', async () => {
  const bodies: string[] = [];
  let calls = 0;
  const api = createApi('http://localhost:5080', async (input) => {
    const request = input as Request;
    expect(request.headers.get('Authorization')).toBe('Bearer test-token');
    bodies.push(await request.text());
    if (calls++ === 0) throw new TypeError('connection lost');
    return new Response(JSON.stringify({ groupId: 'group', entityId: 'payment' }), { status: 200, headers: { 'Content-Type': 'application/json' } });
  });
  api.authenticate(async () => 'test-token', () => {});
  const body = { operationId: 'same-operation-id', amountPaise: 29, paymentDate: '2026-10-13', method: 'Cash', reference: null };
  await expect(api.payment('group', 'cycle', 'member', body)).rejects.toThrow('connection lost');
  await expect(api.payment('group', 'cycle', 'member', body)).resolves.toEqual({ groupId: 'group', entityId: 'payment' });
  expect(bodies[0]).toBe(bodies[1]);
  expect(JSON.parse(bodies[1]).amountPaise).toBe(29);
});

it('exposes server conflict details and expires the session on a 401', async () => {
  const api = createApi('http://localhost:5080', async () => new Response(JSON.stringify({ detail: 'Refresh the ledger', code: 'stale_version' }), { status: 409 }));
  try { await api.ledger('group'); throw new Error('expected rejection'); }
  catch (error) { expect(error).toBeInstanceOf(ApiError); expect((error as ApiError).code).toBe('stale_version'); }
  let expired = false;
  const denied = createApi('http://localhost:5080', async () => new Response(null, { status: 401 }));
  denied.authenticate(async () => 'expired', () => { expired = true; });
  await expect(denied.me()).rejects.toMatchObject({ status: 401 });
  expect(expired).toBe(true);
});

it('binds older history pages to the version the organizer is viewing', async () => {
  const api = createApi('http://localhost:5080', async input => {
    const url = new URL((input as Request).url);
    expect(url.pathname).toBe('/api/groups/group/history');
    expect(url.searchParams.get('page')).toBe('2');
    expect(url.searchParams.get('pageSize')).toBe('50');
    expect(url.searchParams.get('expectedVersion')).toBe('217');
    return new Response(JSON.stringify({ detail: 'Refresh the ledger', code: 'stale_version' }), { status: 409 });
  });
  await expect(api.history('group', 2, 217)).rejects.toMatchObject({ status: 409, code: 'stale_version' });
});

it('retries a payout reversal with the same ID, version and reason after a lost response', async () => {
  const bodies: string[] = [];
  const api = createApi('http://localhost:5080', async input => {
    const request = input as Request;
    expect(new URL(request.url).pathname).toBe('/api/groups/group/payouts/payout/reverse');
    bodies.push(await request.text());
    if (bodies.length === 1) throw new TypeError('connection lost');
    return new Response(JSON.stringify({ groupId: 'group', entityId: 'reversal' }), { status: 200 });
  });
  const body = { operationId: 'stable-operation', expectedVersion: 9, reason: 'Wrong date' };
  await expect(api.reversePayout('group', 'payout', body)).rejects.toThrow('connection lost');
  await expect(api.reversePayout('group', 'payout', body)).resolves.toEqual({ groupId: 'group', entityId: 'reversal' });
  expect(bodies[0]).toBe(bodies[1]);
  expect(JSON.parse(bodies[1])).toEqual(body);
});
