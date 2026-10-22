import React, { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, Text, View } from 'react-native';
import { Cycle, Due, Ledger, Payment, Payout } from '../api/client';
import { api } from '../auth/session';
import { Language, translator } from '../lib/i18n';
import { indiaDate, monthName, rupees } from '../lib/money';
import { Button, Card, Notice, colors, s } from '../ui/components';
import { ActivateDialog, AddMemberDialog, CycleStateDialog, EditMembersDialog, PaymentDialog, PayoutDialog, ReverseDialog, ReversePayoutDialog } from './LedgerDialogs';
import { HistoryPanel } from './HistoryPanel';

type Tab = 'payments' | 'payouts' | 'history';

export function LedgerScreen({ id, language, onBack }: { id: string; language: Language; onBack: () => void }) {
  const t = translator(language);
  const [ledger, setLedger] = useState<Ledger | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [cycleId, setCycleId] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>('payments');
  const [selectedDue, setDue] = useState<Due | null>(null);
  const [reversal, setReversal] = useState<Payment | null>(null);
  const [payout, setPayout] = useState<Cycle | null>(null);
  const [activate, setActivate] = useState(false);
  const [addMember, setAddMember] = useState(false);
  const [editMembers, setEditMembers] = useState(false);
  const [payoutReversal, setPayoutReversal] = useState<Payout | null>(null);
  const [cycleState, setCycleState] = useState<Cycle | null>(null);
  const applyLedger = useCallback((next: Ledger) => {
    setLedger(next); setError(null);
    setCycleId(previous => next.cycles.some(x => x.id === previous) ? previous :
      (next.cycles.find(x => x.dueDate.slice(0, 7) === indiaDate().slice(0, 7)) ?? next.cycles.find(x => x.outstandingPaise > 0) ?? next.cycles[0])?.id ?? null);
  }, []);
  async function load() {
    setBusy(true); setError(null);
    try {
      const next = await api.ledger(id);
      applyLedger(next);
    } catch (error) { setError(error instanceof Error ? error.message : t('networkError')); throw error; }
    finally { setBusy(false); }
  }
  useEffect(() => {
    let alive = true;
    api.ledger(id).then(next => { if (alive) applyLedger(next); })
      .catch(error => { if (alive) setError(error instanceof Error ? error.message : 'Could not reach the server.'); })
      .finally(() => { if (alive) setBusy(false); });
    return () => { alive = false; };
  }, [id, applyLedger]);
  const cycle = ledger?.cycles.find(x => x.id === cycleId);
  return <View style={{ gap: 22 }}>
    <View style={s.between}><Button label={`← ${t('back')}`} kind="quiet" onPress={onBack} /><Button label={t('refresh')} kind="quiet" busy={busy} onPress={() => void load().catch(() => {})} /></View>
    {error && <Notice error>{error}</Notice>}
    {!ledger && busy && <ActivityIndicator color={colors.green} />}
    {ledger && <>
      <View style={{ gap: 7 }}><Text style={s.eyebrow}>BISI / भिशी</Text><Text accessibilityRole="header" style={s.title}>{ledger.name}</Text>
        <Text style={s.body}>{ledger.members.length} {t('members')} · {rupees(ledger.contributionPaise)} {t('contribution').toLowerCase()}</Text></View>
      {ledger.status === 'Draft' ? <>
        <Notice>{t('draftHelp')}</Notice><Card><Text style={s.heading}>{t('members')}</Text>
          {ledger.members.map(member => <View key={member.id} style={s.row}><Avatar label={String(member.position)} /><Text style={[s.body, { color: colors.ink }]}>{member.name}</Text></View>)}
          <Button label={`+ ${t('addMember')}`} kind="secondary" disabled={ledger.members.length >= 50} onPress={() => setAddMember(true)} />
          <Button label={t('editMembers')} kind="secondary" disabled={ledger.members.length === 0} onPress={() => setEditMembers(true)} />
          <Button label={t('start')} disabled={ledger.members.length < 2} onPress={() => setActivate(true)} />
        </Card>
        <HistoryPanel key={ledger.version} group={ledger} language={language} onRefresh={load} />
      </> : <>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 9, paddingBottom: 3 }}>
          {ledger.cycles.map(item => <Pressable key={item.id} accessibilityRole="button" accessibilityState={{ selected: cycleId === item.id }}
            onPress={() => setCycleId(item.id)} style={{ padding: 14, borderRadius: 14, minWidth: 120, gap: 3, backgroundColor: cycleId === item.id ? colors.green : colors.paper, borderWidth: 1, borderColor: colors.line }}>
            <Text style={{ color: cycleId === item.id ? '#D3E7DA' : colors.muted, fontSize: 11 }}>{t('cycle')} {item.number}</Text>
            <Text style={{ color: cycleId === item.id ? '#fff' : colors.ink, fontWeight: '700', fontSize: 14 }}>{monthName(item.dueDate, language)}</Text>
            {item.closedAt && <Text style={{ color: cycleId === item.id ? '#D3E7DA' : colors.green, fontSize: 11 }}>{t('cycleClosed')}</Text>}
          </Pressable>)}
        </ScrollView>
        {cycle && <>
          <View style={{ gap: 8 }}>
            <View style={s.between}><Text style={s.label}>{t('cycle')} {cycle.number} · {t(cycle.closedAt ? 'cycleClosed' : 'cycleOpen')}</Text>
              <Button label={t(cycle.closedAt ? 'reopenCycle' : 'closeCycle')} kind="secondary" onPress={() => setCycleState(cycle)} /></View>
            {cycle.closedAt && <Notice>{t('closedHelp')}</Notice>}
            {cycle.closedAt && ledger.cycles.some(x => x.number === cycle.number + 1) && <Button label={t('nextCycle')} kind="quiet"
              onPress={() => setCycleId(ledger.cycles.find(x => x.number === cycle.number + 1)!.id)} />}
          </View>
          <View style={{ backgroundColor: colors.green, borderRadius: 22, padding: 24, gap: 20 }}>
            <View style={s.between}><View style={{ gap: 7 }}><Text style={{ fontSize: 12, color: '#C2DBC9' }}>{t('collected')}</Text><Text style={{ fontSize: 36, fontWeight: '800', color: '#fff' }}>{rupees(cycle.collectedPaise)}</Text></View><Text style={{ color: '#C2DBC9', fontSize: 13 }}>{t('due')} {cycle.dueDate}</Text></View>
            <View accessibilityRole="progressbar" accessibilityLabel={t('collected')} accessibilityValue={{ min: 0, max: cycle.expectedPaise, now: cycle.collectedPaise }}
              style={{ height: 7, backgroundColor: '#4A7B60', borderRadius: 4, overflow: 'hidden' }}><View style={{ height: 7, width: `${Math.min(100, cycle.collectedPaise / cycle.expectedPaise * 100)}%`, backgroundColor: '#DCECC1', borderRadius: 4 }} /></View>
            <View style={s.between}><Stat label={t('expected')} value={rupees(cycle.expectedPaise)} light /><Stat label={t('outstanding')} value={rupees(cycle.outstandingPaise)} light /></View>
          </View>
          <View style={{ flexDirection: 'row', gap: 7, flexWrap: 'wrap' }}>{(['payments', 'payouts', 'history'] as const).map(value =>
            <Button key={value} label={t(value)} kind={tab === value ? 'primary' : 'secondary'} onPress={() => setTab(value)} />)}</View>
          {tab === 'payments' && <>
            <Card><View style={s.between}><View style={{ flex: 1, gap: 5 }}><Text style={s.small}>{t('recipient')}</Text><Text style={s.heading}>{cycle.recipientName}</Text></View><Text style={s.heading}>{rupees(cycle.payout?.amountPaise ?? cycle.expectedPaise)}</Text></View>
              <Text style={s.small}>{cycle.payout ? t('paidOut') : t('planned')}</Text>
              {cycle.payout && <Text style={s.small}>{t('payoutRemaining')}: {rupees(cycle.payout.outstandingPaise)}</Text>}
              {cycle.payoutShortfallPaise > 0 && <Notice>{t('gap')}: {rupees(cycle.payoutShortfallPaise)}. {t('gapHelp')}</Notice>}
            </Card>
            <Card><View style={s.between}><Text style={s.heading}>{t('payments')}</Text><Text style={s.small}>{cycle.dues.filter(x => x.status === 'Paid').length}/{cycle.dues.length} {t('paid').toLowerCase()}</Text></View>
              {cycle.dues.map(due => <View key={due.id} style={{ borderTopWidth: 1, borderTopColor: colors.line, paddingTop: 17, gap: 10 }}>
                <View style={s.between}><View style={{ flexDirection: 'row', alignItems: 'center', gap: 11, flex: 1 }}><Avatar label={due.name.slice(0, 1)} /><View style={{ flex: 1, gap: 3 }}><Text style={{ color: colors.ink, fontSize: 16, fontWeight: '700' }}>{due.name}</Text><Text style={s.small}>{rupees(due.paidPaise)} / {rupees(due.expectedPaise)}</Text></View></View><PaymentStatus due={due} t={t} /></View>
                <View style={s.between}><Text style={s.small}>{t('balance')}: {rupees(due.outstandingPaise)}</Text><Button label={!cycle.closedAt && due.outstandingPaise > 0 ? t('record') : t('view')} kind="secondary" onPress={() => setDue(due)} /></View>
              </View>)}
            </Card>
          </>}
          {tab === 'payouts' && <Card><Text style={s.heading}>{t('payouts')}</Text>
            {ledger.cycles.map(item => <View key={item.id} style={{ paddingTop: 15, borderTopWidth: 1, borderTopColor: colors.line, gap: 10 }}>
              <View style={s.row}><Avatar label={String(item.number)} /><View style={{ flex: 1, gap: 4 }}><Text style={s.label}>{item.recipientName}</Text><Text style={s.small}>{monthName(item.dueDate, language)}</Text></View><Text style={s.heading}>{rupees(item.payout?.amountPaise ?? item.expectedPaise)}</Text></View>
              <Text style={s.small}>{item.payout ? `${t('payoutRemaining')}: ${rupees(item.payout.outstandingPaise)}` : t('notPaidOut')}</Text>
              {item.closedAt && <Text style={s.label}>{t('cycleClosed')}</Text>}
              {item.payout?.entries.map(entry => <View key={entry.id} style={{ gap: 5 }}>
                <Text style={[s.small, entry.reversalReason !== null && { textDecorationLine: 'line-through' }]}>{t('paidOut')}: {rupees(entry.amountPaise)} · {entry.paymentDate}</Text>
                {entry.fundingNote && <Text style={s.small}>{entry.fundingNote}</Text>}
                {entry.reversalReason !== null ? <Text style={s.small}>{t('reversed')}: {entry.reversalReason}</Text>
                  : !item.closedAt && <Button label={t('reversePayout')} kind="quiet" onPress={() => setPayoutReversal(entry)} />}
              </View>)}
              {!item.closedAt && (!item.payout || item.payout.outstandingPaise > 0) && <Button label={t('recordPayout')} kind="secondary" onPress={() => setPayout(item)} />}
            </View>)}
          </Card>}
          {tab === 'history' && <HistoryPanel key={ledger.version} group={ledger} language={language} onRefresh={load} />}
        </>}
      </>}
      <Text style={s.small}>{t('synced')}: {new Date(ledger.updatedAt).toLocaleString(language === 'mr' ? 'mr-IN' : 'en-IN', { timeZone: 'Asia/Kolkata' })}</Text>
      <Text style={s.small}>{t('paymentDisclaimer')}</Text>
      {selectedDue && cycle && <PaymentDialog group={ledger} cycle={cycle} due={selectedDue} t={t} onClose={() => setDue(null)} onSaved={load} onReverse={payment => { setDue(null); setReversal(payment); }} />}
      {reversal && <ReverseDialog group={ledger} payment={reversal} t={t} onClose={() => setReversal(null)} onSaved={load} />}
      {payout && <PayoutDialog group={ledger} cycle={payout} t={t} onClose={() => setPayout(null)} onSaved={load} />}
      {activate && <ActivateDialog group={ledger} t={t} onClose={() => setActivate(false)} onSaved={load} />}
      {addMember && <AddMemberDialog group={ledger} t={t} onClose={() => setAddMember(false)} onSaved={load} />}
      {editMembers && <EditMembersDialog group={ledger} t={t} onClose={() => setEditMembers(false)} onSaved={load} />}
      {payoutReversal && <ReversePayoutDialog group={ledger} payout={payoutReversal} t={t} onClose={() => setPayoutReversal(null)} onSaved={load} />}
      {cycleState && <CycleStateDialog group={ledger} cycle={cycleState} t={t} onClose={() => setCycleState(null)} onSaved={load} />}
    </>}
  </View>;
}

function Avatar({ label }: { label: string }) {
  return <View style={{ width: 39, height: 39, borderRadius: 13, backgroundColor: colors.lightGreen, alignItems: 'center', justifyContent: 'center' }}><Text style={{ color: colors.green, fontWeight: '700', fontSize: 16 }}>{label.toUpperCase()}</Text></View>;
}

function Stat({ label, value, light = false }: { label: string; value: string; light?: boolean }) {
  return <View style={{ gap: 5, flex: 1 }}><Text style={{ fontSize: 12, color: light ? '#C2DBC9' : colors.muted }}>{label}</Text><Text style={{ fontSize: 19, fontWeight: '700', color: light ? '#fff' : colors.ink }}>{value}</Text></View>;
}

function PaymentStatus({ due, t }: { due: Due; t: ReturnType<typeof translator> }) {
  const tone = due.status === 'Paid' ? [colors.green, colors.lightGreen] : due.isOverdue ? [colors.red, colors.lightRed] : [colors.orange, colors.lightOrange];
  const label = due.status === 'Paid' ? t('paid') : due.status === 'Partial' ? t('partial') : t('pending');
  return <View style={{ alignItems: 'flex-end', gap: 3 }}><Text style={{ color: tone[0], backgroundColor: tone[1], fontSize: 11, fontWeight: '700', borderRadius: 7, padding: 7 }}>{label}</Text>{due.isOverdue && <Text style={{ fontSize: 10, color: colors.red }}>{t('overdue')}</Text>}</View>;
}
