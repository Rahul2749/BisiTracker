import React, { useRef, useState } from 'react';
import { Text, View } from 'react-native';
import { ApiError, Ledger } from '../api/client';
import { api } from '../auth/session';
import { Language, translator } from '../lib/i18n';
import { Button, Card, Notice, colors, s } from '../ui/components';

// The parent keys this panel by ledger version, resetting pagination after a refresh or mutation.
export function HistoryPanel({ group, language, onRefresh }: { group: Ledger; language: Language; onRefresh: () => Promise<void> }) {
  const t = translator(language);
  const [items, setItems] = useState(group.history);
  const [nextPage, setNextPage] = useState(group.historyNextPage);
  const [busy, setBusy] = useState(false);
  const [changed, setChanged] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inFlight = useRef(false);
  async function loadMore() {
    if (nextPage === null || inFlight.current) return;
    inFlight.current = true; setBusy(true); setError(null);
    try {
      const page = await api.history(group.id, nextPage, group.version);
      setItems(current => [...current, ...page.items]);
      setNextPage(page.nextPage);
    } catch (error) {
      if (error instanceof ApiError && error.code === 'stale_version') setChanged(true);
      else setError(error instanceof Error ? error.message : t('networkError'));
    } finally { inFlight.current = false; setBusy(false); }
  }
  return <Card><Text style={s.heading}>{t('history')}</Text>
    {items.length === 0 && <Text style={s.body}>{t('noHistory')}</Text>}
    {items.map(event => <View key={event.id} style={{ borderTopWidth: 1, borderTopColor: colors.line, paddingTop: 15, gap: 5 }}>
      <Text style={[s.body, { color: colors.ink }]}>{event.description}</Text>
      <Text style={s.small}>{new Date(event.recordedAt).toLocaleString(language === 'mr' ? 'mr-IN' : 'en-IN', { timeZone: 'Asia/Kolkata' })}</Text>
    </View>)}
    {error && <Notice error>{error}</Notice>}
    {changed ? <><Notice>{t('historyChanged')}</Notice><Button label={t('refresh')} kind="secondary" onPress={() => void onRefresh().catch(() => {})} /></>
      : nextPage !== null ? <Button label={t('historyMore')} kind="secondary" busy={busy} onPress={() => void loadMore()} />
        : items.length > 0 && <Text style={s.small}>{t('historyEnd')}</Text>}
  </Card>;
}
