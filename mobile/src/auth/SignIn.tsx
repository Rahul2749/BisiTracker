import React, { useState } from 'react';
import { Text, View } from 'react-native';
import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import { api, developmentAuth, oidcClientId, oidcIssuer, oidcScopes, Session, useAuth } from './session';
import { Button, Card, Notice, colors, s } from '../ui/components';
import { translator } from '../lib/i18n';

WebBrowser.maybeCompleteAuthSession();

export function SignIn({ t }: { t: ReturnType<typeof translator> }) {
  const { complete, message } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  async function local() {
    setBusy(true); setError(null);
    try { await complete(await api.signIn()); }
    catch (error) { setError(error instanceof Error ? error.message : t('networkError')); }
    finally { setBusy(false); }
  }
  return <View style={{ gap: 30, paddingTop: 32 }}>
    <Text style={s.eyebrow}>{t('tagline').toUpperCase()}</Text>
    <Text style={[s.title, { fontSize: 45, lineHeight: 54 }]}>{t('welcome')}</Text>
    <Text style={[s.body, { fontSize: 17, maxWidth: 430, lineHeight: 28 }]}>{t('intro')}</Text>
    <View style={{ flexDirection: 'row', gap: 8, paddingVertical: 14 }}>
      {[36, 54, 72, 90, 108].map((height, i) => <View key={height} style={{ width: 42, height, borderRadius: 14, backgroundColor: i === 4 ? colors.orange : colors.green, opacity: .3 + i * .17, alignSelf: 'flex-end' }} />)}
    </View>
    {error && <Notice error>{error}</Notice>}
    {message && <Notice error>{t('sessionExpired')}</Notice>}
    {developmentAuth ? <Card><Text style={s.eyebrow}>{t('devLabel')}</Text><Text style={s.body}>{t('devHelp')}</Text><Button label={t('devSignIn')} busy={busy} onPress={() => void local()} /></Card>
      : oidcIssuer && oidcClientId ? <OidcSignIn t={t} onSession={complete} /> : <Notice error>{t('authSetup')}</Notice>}
    <Text style={s.small}>{t('paymentDisclaimer')}</Text>
  </View>;
}

function OidcSignIn({ t, onSession }: { t: ReturnType<typeof translator>; onSession: (session: Session) => Promise<void> }) {
  const discovery = AuthSession.useAutoDiscovery(oidcIssuer);
  const redirectUri = AuthSession.makeRedirectUri({ scheme: 'bisitracker', path: 'oauth' });
  const audience = process.env.EXPO_PUBLIC_OIDC_AUDIENCE;
  const [request, , prompt] = AuthSession.useAuthRequest({
    clientId: oidcClientId, redirectUri, scopes: oidcScopes, responseType: AuthSession.ResponseType.Code,
    usePKCE: true, extraParams: audience ? { audience } : undefined,
  }, discovery);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  async function login() {
    if (!discovery || !request?.codeVerifier) return;
    setBusy(true); setError(null);
    try {
      const response = await prompt();
      if (response.type === 'error') throw new Error(response.error?.message ?? t('authSetup'));
      if (response.type !== 'success') return;
      const token = await AuthSession.exchangeCodeAsync({ clientId: oidcClientId, code: response.params.code, redirectUri,
        extraParams: { code_verifier: request.codeVerifier } }, discovery);
      await onSession({
        accessToken: token.accessToken, refreshToken: token.refreshToken,
        expiresAt: new Date((token.issuedAt + (token.expiresIn ?? 3600)) * 1000).toISOString(), name: 'Organizer',
      });
    } catch (error) { setError(error instanceof Error ? error.message : t('authSetup')); }
    finally { setBusy(false); }
  }
  return <View style={{ gap: 14 }}>{error && <Notice error>{error}</Notice>}<Button label={t('signIn')} busy={busy} disabled={!request} onPress={() => void login()} /></View>;
}
