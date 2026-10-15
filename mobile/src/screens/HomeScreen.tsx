import React, { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { ActivityIndicator, Pressable, Text, View } from 'react-native';
import { Group } from '../api/client';
import { api, useAuth } from '../auth/session';
import { translator } from '../lib/i18n';
import { monthName, rupees } from '../lib/money';
import { Button, Card, Notice, colors, s } from '../ui/components';

export function HomeScreen({ t, onNew, onOpen }: { t: ReturnType<typeof translator>; onNew: () => void; onOpen: (id: string) => void }) {
  const { session } = useAuth();
  const [groups, setGroups] = useState<Group[]>([]);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  async function load(next = 1) {
    setBusy(true); setError(null);
    try {
      const result = await api.groups(next);
      setGroups(previous => next === 1 ? result : [...previous, ...result.filter(x => !previous.some(g => g.id === x.id))]);
      setPage(next); setHasMore(result.length === 20);
    } catch (error) { setError(error instanceof Error ? error.message : t('networkError')); }
    finally { setBusy(false); }
  }
  useFocusEffect(useCallback(() => {
    let alive = true;
    api.groups().then(result => {
      if (!alive) return;
      setGroups(result); setPage(1); setHasMore(result.length === 20); setError(null);
    }).catch(error => { if (alive) setError(error instanceof Error ? error.message : 'Could not reach the server.'); })
      .finally(() => { if (alive) setBusy(false); });
    return () => { alive = false; };
  }, []));
  return <View style={{ gap: 25 }}>
    <View style={{ gap: 7 }}><Text style={s.eyebrow}>{t('greeting').toUpperCase()}, {session?.name.toUpperCase()}</Text><Text accessibilityRole="header" style={s.title}>{t('groups')}</Text><Text style={s.body}>{t('homeSubtitle')}</Text></View>
    <View style={s.between}><Text style={s.small}>{groups.length} {t('groups').toLowerCase()}</Text><View style={s.row}><Button label={t('refresh')} kind="quiet" busy={busy} onPress={() => void load()} /><Button label={`+ ${t('newGroup')}`} onPress={onNew} /></View></View>
    {error && <Notice error>{error}</Notice>}
    {busy && groups.length === 0 && <ActivityIndicator color={colors.green} />}
    {!busy && !error && groups.length === 0 && <Card><View style={{ paddingVertical: 23, gap: 17 }}><Text style={[s.heading, { textAlign: 'center' }]}>{t('emptyTitle')}</Text><Text style={[s.body, { textAlign: 'center' }]}>{t('emptyBody')}</Text><Button label={t('newGroup')} kind="secondary" onPress={onNew} /></View></Card>}
    {groups.map(group => <Pressable key={group.id} accessibilityRole="button" accessibilityLabel={group.name} onPress={() => onOpen(group.id)}>
      <Card><View style={s.between}><View style={{ flex: 1, gap: 7 }}><Text style={s.heading}>{group.name}</Text><Text style={s.small}>{group.memberCount} {t('members')} · {monthName(group.startMonth)}</Text></View><Text style={{ color: colors.green, backgroundColor: colors.lightGreen, borderRadius: 8, padding: 8, fontWeight: '600', fontSize: 12 }}>{group.status === 'Draft' ? t('draft') : t('active')}</Text></View>
        <View style={s.divider} /><View style={s.between}><View style={{ gap: 4 }}><Text style={s.small}>{t('contribution')}</Text><Text style={[s.heading, { fontSize: 26 }]}>{rupees(Number(group.contributionPaise))}</Text></View><Text style={{ color: colors.green, fontSize: 26 }}>→</Text></View></Card>
    </Pressable>)}
    {hasMore && <Button label={t('more')} kind="secondary" busy={busy} onPress={() => void load(page + 1)} />}
  </View>;
}
