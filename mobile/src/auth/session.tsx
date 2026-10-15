import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';
import * as AuthSession from 'expo-auth-session';
import { ApiError, createApi } from '../api/client';

const configuredUrl = process.env.EXPO_PUBLIC_API_URL;
export const apiUrl = configuredUrl ?? (Platform.OS === 'android' ? 'http://10.0.2.2:5080' : 'http://localhost:5080');
export const developmentAuth = __DEV__ && process.env.EXPO_PUBLIC_AUTH_MODE !== 'oidc';
export const oidcIssuer = process.env.EXPO_PUBLIC_OIDC_ISSUER ?? '';
export const oidcClientId = process.env.EXPO_PUBLIC_OIDC_CLIENT_ID ?? '';
export const oidcScopes = (process.env.EXPO_PUBLIC_OIDC_SCOPES ?? 'openid profile offline_access').split(' ').filter(Boolean);
export const api = createApi(apiUrl, (request, init) => {
  if (!__DEV__ && !apiUrl.startsWith('https://')) throw new Error('Production API must use HTTPS.');
  return fetch(request, init);
});
const key = 'bisitracker.organizer-session.v1';

export interface Session { accessToken: string; refreshToken?: string; expiresAt: string; name: string }
interface AuthState {
  session: Session | null; initializing: boolean; message: string | null;
  complete: (session: Session) => Promise<void>; signOut: () => Promise<void>;
}
const AuthContext = createContext<AuthState | null>(null);

async function persist(session: Session | null) {
  // Browser previews keep tokens in memory. Native builds use the OS credential store.
  if (Platform.OS === 'web') return;
  if (session) await SecureStore.setItemAsync(key, JSON.stringify(session));
  else await SecureStore.deleteItemAsync(key);
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [initializing, setInitializing] = useState(true);
  const [message, setMessage] = useState<string | null>(null);
  const current = useRef<Session | null>(null);
  const refresh = useRef<Promise<string> | null>(null);
  const generation = useRef(0);

  const signOut = useCallback(async () => {
    generation.current++;
    current.current = null; setSession(null); setMessage(null);
    await persist(null);
  }, []);

  const expired = useCallback(() => {
    void signOut().then(() => setMessage('sessionExpired'));
  }, [signOut]);

  useEffect(() => {
    api.authenticate(async () => {
      const saved = current.current;
      if (!saved) return null;
      if (Date.parse(saved.expiresAt) > Date.now() + 30_000) return saved.accessToken;
      if (!saved.refreshToken || !oidcIssuer || !oidcClientId) { expired(); throw new ApiError(401, 'Session expired.'); }
      if (!refresh.current) {
        const epoch = generation.current;
        refresh.current = (async () => {
          const discovery = await AuthSession.fetchDiscoveryAsync(oidcIssuer);
          const token = await AuthSession.refreshAsync({ clientId: oidcClientId, refreshToken: saved.refreshToken, scopes: oidcScopes }, discovery);
          if (generation.current !== epoch) throw new ApiError(401, 'Session ended.');
          const next: Session = {
            ...saved, accessToken: token.accessToken, refreshToken: token.refreshToken ?? saved.refreshToken,
            expiresAt: new Date((token.issuedAt + (token.expiresIn ?? 3600)) * 1000).toISOString(),
          };
          await persist(next);
          current.current = next; setSession(next);
          return next.accessToken;
        })().catch(error => {
          // Preserve recoverable credentials on temporary connection failures.
          if (error?.code === 'invalid_grant' || error instanceof ApiError) expired();
          throw error;
        }).finally(() => { refresh.current = null; });
      }
      return refresh.current;
    }, expired);
  }, [expired]);

  useEffect(() => {
    let alive = true;
    (async () => {
      if (Platform.OS === 'web') return;
      const raw = await SecureStore.getItemAsync(key);
      if (!raw || !alive) return;
      const saved: Session = JSON.parse(raw);
      if (typeof saved.accessToken !== 'string' || !Number.isFinite(Date.parse(saved.expiresAt))) { await persist(null); return; }
      if (Date.parse(saved.expiresAt) <= Date.now() && !saved.refreshToken) { await persist(null); return; }
      current.current = saved; setSession(saved);
    })().catch(() => { if (alive) setMessage('sessionExpired'); })
      .finally(() => { if (alive) setInitializing(false); });
    return () => { alive = false; };
  }, []);

  const complete = useCallback(async (next: Session) => {
    if (!__DEV__ && !apiUrl.startsWith('https://')) throw new Error('Production API must use HTTPS.');
    const epoch = ++generation.current;
    current.current = next;
    try {
      const organizer = await api.me();
      if (generation.current !== epoch) throw new ApiError(401, 'Session ended.');
      const verified = { ...next, name: organizer.name };
      await persist(verified);
      current.current = verified; setSession(verified); setMessage(null);
    } catch (error) { current.current = null; throw error; }
  }, []);

  return <AuthContext.Provider value={{ session, initializing, message, complete, signOut }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const auth = useContext(AuthContext);
  if (!auth) throw new Error('AuthProvider is required.');
  return auth;
}
