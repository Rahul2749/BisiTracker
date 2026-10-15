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
