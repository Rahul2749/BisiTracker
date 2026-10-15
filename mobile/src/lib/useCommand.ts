import { useRef, useState } from 'react';
import * as Crypto from 'expo-crypto';
import { ApiError } from '../api/client';

// Keep the exact command through an ambiguous response. A retry must not become a new payment.
// Durable, cross-restart pending operations will be introduced with SQLite sync in Phase 4.
export function useCommand<T>() {
  const pending = useRef<(() => Promise<T>) | null>(null);
  const confirmed = useRef(false);
  const inFlight = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [uncertain, setUncertain] = useState(false);
  const [locked, setLocked] = useState(false);
  const [isConfirmed, setConfirmed] = useState(false);

  async function run(factory: (operationId: string) => () => Promise<T>, onSaved: (result: T) => Promise<void> | void) {
    if (inFlight.current) return;
    inFlight.current = true; setBusy(true); setError(null); setUncertain(false); setLocked(true);
    try {
      pending.current ??= factory(Crypto.randomUUID());
      const result = await pending.current();
      confirmed.current = true;
      setConfirmed(true);
      // If the subsequent refresh fails, retry that refresh rather than resending the write.
      pending.current = () => Promise.resolve(result);
      await onSaved(result);
      pending.current = null; confirmed.current = false;
      setLocked(false); setConfirmed(false);
    } catch (error) {
      const definitiveRejection = error instanceof ApiError && error.status >= 400 && error.status < 500;
      if (definitiveRejection && !confirmed.current) pending.current = null;
      setLocked(pending.current !== null);
      setUncertain(!definitiveRejection && !confirmed.current);
      setError(error instanceof Error ? error.message : 'Could not complete the request.');
    } finally { inFlight.current = false; setBusy(false); }
  }
  return { run, busy, error, uncertain, setError, locked, confirmed: isConfirmed };
}
