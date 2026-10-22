import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { Cycle, Due, Ledger, Payment, Payout } from '../api/client';
import { api } from '../auth/session';
import { translator } from '../lib/i18n';
import { indiaDate, inputRupees, parseRupees, rupees, validDate } from '../lib/money';
import { useCommand } from '../lib/useCommand';
import { Button, Dialog, Field, Notice, s } from '../ui/components';

type T = ReturnType<typeof translator>;
type Result = { groupId: string; entityId: string | null };
interface Shared { group: Ledger; t: T; onClose: () => void; onSaved: () => Promise<void> }

function Feedback({ command, t }: { command: ReturnType<typeof useCommand<Result>>; t: T }) {
  return <>{command.error && <Notice error>{command.error}</Notice>}{command.uncertain && <Notice>{t('uncertain')}</Notice>}</>;
}

export function PaymentDialog({ group, cycle, due, t, onClose, onSaved, onReverse }: Shared & { cycle: Cycle; due: Due; onReverse: (payment: Payment) => void }) {
  const [amount, setAmount] = useState(inputRupees(due.outstandingPaise));
  const [date, setDate] = useState(indiaDate());
  const [method, setMethod] = useState('Cash');
  const [reference, setReference] = useState('');
  const command = useCommand<Result>();
  function save() {
    const amountPaise = parseRupees(amount);
    if (!amountPaise || amountPaise > due.outstandingPaise || !validDate(date)) { command.setError(t('amountInvalid')); return; }
    const body = { amountPaise, paymentDate: date, method, reference: reference.trim() || null };
    void command.run(operationId => () => api.payment(group.id, cycle.id, due.memberId, { ...body, operationId }), async () => { await onSaved(); onClose(); });
  }
  return <Dialog title={due.name} closeLabel={t('close')} busy={command.busy || command.locked} onClose={onClose}>
    <Text style={s.body}>{t('received')}: {rupees(due.paidPaise)} · {t('balance')}: {rupees(due.outstandingPaise)}</Text>
    {cycle.closedAt && <Notice>{t('closedHelp')}</Notice>}
    {!cycle.closedAt && due.outstandingPaise > 0 && <><Field label={t('amount')} value={amount} onChangeText={setAmount} keyboardType="decimal-pad" editable={!command.locked} />
      <Field label={t('paymentDate')} value={date} onChangeText={setDate} maxLength={10} autoCapitalize="none" editable={!command.locked} />
      <Text style={s.label}>{t('method')}</Text><View style={{ flexDirection: 'row', gap: 6, flexWrap: 'wrap' }}>
        {(['Cash', 'UPI', 'Bank', 'Other'] as const).map(value => <Button key={value} label={t(({ Cash: 'cash', UPI: 'upi', Bank: 'bank', Other: 'other' } as const)[value])}
          kind={method === value ? 'primary' : 'secondary'} disabled={command.locked} onPress={() => setMethod(value)} />)}
      </View><Field label={t('reference')} value={reference} onChangeText={setReference} maxLength={140} editable={!command.locked} />
      <Feedback command={command} t={t} /><Button label={command.confirmed ? t('refresh') : command.locked ? t('retry') : t('savePayment')} busy={command.busy} onPress={save} />
    </>}
    <View style={s.divider} /><Text style={s.heading}>{t('paymentHistory')}</Text>
    {due.payments.length === 0 && <Text style={s.body}>{t('noPayments')}</Text>}
    {due.payments.map(payment => <View key={payment.id} style={{ gap: 7 }}>
      <View style={s.between}><Text style={[s.heading, payment.reversalReason !== null && { textDecorationLine: 'line-through', opacity: .6 }]}>{rupees(payment.amountPaise)}</Text><Text style={s.small}>{payment.paymentDate} · {payment.method}</Text></View>
      {payment.reference && <Text style={s.small}>{payment.reference}</Text>}
      {payment.reversalReason !== null ? <Text style={s.small}>{t('reversed')}: {payment.reversalReason}</Text> : !cycle.closedAt && <Button label={t('reverse')} kind="quiet" disabled={command.busy || command.locked} onPress={() => onReverse(payment)} />}
    </View>)}
  </Dialog>;
}

export function ReverseDialog({ group, payment, t, onClose, onSaved }: Shared & { payment: Payment }) {
  const [reason, setReason] = useState('');
  const command = useCommand<Result>();
  function save() {
    if (!reason.trim() || reason.trim().length > 300) { command.setError(t('reasonRequired')); return; }
    void command.run(operationId => () => api.reverse(group.id, payment.id, { operationId, expectedVersion: group.version, reason: reason.trim() }), async () => { await onSaved(); onClose(); });
  }
  return <Dialog title={t('correctionTitle')} closeLabel={t('cancel')} busy={command.busy || command.locked} onClose={onClose}>
    <Text style={s.heading}>{rupees(payment.amountPaise)} · {payment.paymentDate}</Text><Notice>{t('correctionHelp')}</Notice>
    <Field label={t('reason')} value={reason} onChangeText={setReason} multiline maxLength={300} editable={!command.locked} />
    <Feedback command={command} t={t} /><Button label={command.confirmed ? t('refresh') : command.locked ? t('retry') : t('saveCorrection')} busy={command.busy} onPress={save} />
  </Dialog>;
}

export function PayoutDialog({ group, cycle, t, onClose, onSaved }: Shared & { cycle: Cycle }) {
  const remaining = cycle.expectedPaise - (cycle.payout?.amountPaise ?? 0);
  const [amount, setAmount] = useState(inputRupees(remaining));
  const [date, setDate] = useState(indiaDate());
  const [note, setNote] = useState('');
  const command = useCommand<Result>();
  const gap = Math.max(0, (cycle.payout?.amountPaise ?? 0) + (parseRupees(amount) ?? 0) - cycle.collectedPaise);
  function save() {
    const amountPaise = parseRupees(amount);
    if (!amountPaise || amountPaise > remaining || !validDate(date) || (gap > 0 && !note.trim())) { command.setError(t('required')); return; }
    void command.run(operationId => () => api.payout(group.id, cycle.id, { operationId, expectedVersion: group.version, amountPaise, paymentDate: date, fundingNote: note.trim() || null }), async () => { await onSaved(); onClose(); });
  }
  return <Dialog title={t('recordPayout')} closeLabel={t('cancel')} busy={command.busy || command.locked} onClose={onClose}>
    <Text style={s.heading}>{cycle.recipientName}</Text><Text style={s.body}>{t('payoutHelp')}</Text>
    <Field label={t('amount')} value={amount} onChangeText={setAmount} keyboardType="decimal-pad" editable={!command.locked} />
    <Field label={t('paymentDate')} value={date} onChangeText={setDate} autoCapitalize="none" maxLength={10} editable={!command.locked} />
    {gap > 0 && <Notice>{t('gap')}: {rupees(gap)}</Notice>}
    <Field label={t('fundingNote')} value={note} onChangeText={setNote} multiline maxLength={300} editable={!command.locked} />
    <Feedback command={command} t={t} /><Button label={command.confirmed ? t('refresh') : command.locked ? t('retry') : t('savePayout')} busy={command.busy} onPress={save} />
  </Dialog>;
}

export function ActivateDialog({ group, t, onClose, onSaved }: Shared) {
  const command = useCommand<Result>();
  return <Dialog title={t('startTitle')} closeLabel={t('cancel')} busy={command.busy || command.locked} onClose={onClose}>
    <Notice>{t('startBody')}</Notice>
    {group.members.map(member => <Text key={member.id} style={s.body}>{member.position}. {member.name}</Text>)}
    <Text style={s.heading}>{rupees(group.contributionPaise)} × {group.members.length} = {rupees(group.contributionPaise * group.members.length)}</Text>
    <Feedback command={command} t={t} /><Button label={command.confirmed ? t('refresh') : command.locked ? t('retry') : t('start')} busy={command.busy}
      onPress={() => void command.run(operationId => () => api.activate(group.id, { operationId, expectedVersion: group.version }), async () => { await onSaved(); onClose(); })} />
  </Dialog>;
}

export function AddMemberDialog({ group, t, onClose, onSaved }: Shared) {
  const [name, setName] = useState('');
  const command = useCommand<Result>();
  function save() {
    if (!name.trim()) { command.setError(t('required')); return; }
    void command.run(operationId => () => api.addMember(group.id, { operationId, expectedVersion: group.version, name: name.trim() }), async () => { await onSaved(); onClose(); });
  }
  return <Dialog title={t('addMember')} closeLabel={t('cancel')} busy={command.busy || command.locked} onClose={onClose}>
    <Field label={t('memberName')} value={name} onChangeText={setName} maxLength={80} editable={!command.locked} />
    <Feedback command={command} t={t} /><Button label={command.confirmed ? t('refresh') : command.locked ? t('retry') : t('addMember')} busy={command.busy} onPress={save} />
  </Dialog>;
}

export function EditMembersDialog({ group, t, onClose, onSaved }: Shared) {
  const [members, setMembers] = useState(group.members.map(x => ({ id: x.id, name: x.name })));
  const command = useCommand<Result>();
  function move(index: number, direction: number) {
    setMembers(current => {
      const next = [...current];
      const destination = index + direction;
      if (destination < 0 || destination >= next.length) return current;
      [next[index], next[destination]] = [next[destination], next[index]];
      return next;
    });
  }
  function save() {
    if (members.some(x => !x.name.trim() || x.name.trim().length > 80)) { command.setError(t('required')); return; }
    const edited = members.map(x => ({ ...x, name: x.name.trim() }));
    void command.run(operationId => () => api.editMembers(group.id, { operationId, expectedVersion: group.version, members: edited }),
      async () => { await onSaved(); onClose(); });
  }
  return <Dialog title={t('editMembers')} closeLabel={t('cancel')} busy={command.busy || command.locked} onClose={onClose}>
    <Notice>{t('editMembersHelp')}</Notice>
    {members.length === 0 && <Text style={s.body}>{t('emptyMembers')}</Text>}
    {members.map((member, index) => <View key={member.id} style={{ gap: 8 }}>
      <Field label={`${t('memberName')} ${index + 1}`} value={member.name} maxLength={80} editable={!command.locked}
        onChangeText={name => setMembers(current => current.map(x => x.id === member.id ? { ...x, name } : x))} />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
        <Button label="↑" accessibilityLabel={`${t('moveUp')} ${member.name}`} kind="secondary" disabled={command.locked || index === 0} onPress={() => move(index, -1)} />
        <Button label="↓" accessibilityLabel={`${t('moveDown')} ${member.name}`} kind="secondary" disabled={command.locked || index === members.length - 1} onPress={() => move(index, 1)} />
        <Button label={t('removeMember')} accessibilityLabel={`${t('removeMember')} ${member.name}`} kind="danger" disabled={command.locked}
          onPress={() => setMembers(current => current.filter(x => x.id !== member.id))} />
      </View>
    </View>)}
    <Feedback command={command} t={t} /><Button label={command.confirmed ? t('refresh') : command.locked ? t('retry') : t('saveMembers')} busy={command.busy} onPress={save} />
  </Dialog>;
}

export function ReversePayoutDialog({ group, payout, t, onClose, onSaved }: Shared & { payout: Payout }) {
  const cycle = group.cycles.find(x => x.payout?.entries.some(entry => entry.id === payout.id));
  const [reason, setReason] = useState('');
  const command = useCommand<Result>();
  function save() {
    if (!reason.trim() || reason.trim().length > 300) { command.setError(t('reasonRequired')); return; }
    void command.run(operationId => () => api.reversePayout(group.id, payout.id, { operationId, expectedVersion: group.version, reason: reason.trim() }),
      async () => { await onSaved(); onClose(); });
  }
  return <Dialog title={t('payoutCorrectionTitle')} closeLabel={t('cancel')} busy={command.busy || command.locked} onClose={onClose}>
    {cycle && <Text style={s.label}>{cycle.recipientName} · {t('cycle')} {cycle.number}</Text>}
    <Text style={s.heading}>{rupees(payout.amountPaise)} · {payout.paymentDate}</Text><Notice>{t('payoutCorrectionHelp')}</Notice>
    <Field label={t('reason')} value={reason} onChangeText={setReason} multiline maxLength={300} editable={!command.locked} />
    <Feedback command={command} t={t} /><Button label={command.confirmed ? t('refresh') : command.locked ? t('retry') : t('saveCorrection')} busy={command.busy} onPress={save} />
  </Dialog>;
}

export function CycleStateDialog({ group, cycle, t, onClose, onSaved }: Shared & { cycle: Cycle }) {
  const reopening = cycle.closedAt !== null;
  const [reason, setReason] = useState('');
  const command = useCommand<Result>();
  const canClose = cycle.outstandingPaise === 0 && cycle.payout?.outstandingPaise === 0;
  function save() {
    if (reopening && (!reason.trim() || reason.trim().length > 300)) { command.setError(t('reasonRequired')); return; }
    void command.run(operationId => () => reopening
      ? api.reopenCycle(group.id, cycle.id, { operationId, expectedVersion: group.version, reason: reason.trim() })
      : api.closeCycle(group.id, cycle.id, { operationId, expectedVersion: group.version }),
    async () => { await onSaved(); onClose(); });
  }
  return <Dialog title={t(reopening ? 'reopenCycle' : 'closeCycle')} closeLabel={t('cancel')} busy={command.busy || command.locked} onClose={onClose}>
    <Text style={s.heading}>{t('cycle')} {cycle.number} · {cycle.recipientName}</Text>
    <Notice>{t(reopening ? 'reopenCycleHelp' : 'closeCycleHelp')}</Notice>
    <Text style={s.body}>{t('outstanding')}: {rupees(cycle.outstandingPaise)}</Text>
    <Text style={s.body}>{t('payoutRemaining')}: {rupees(cycle.payout?.outstandingPaise ?? cycle.expectedPaise)}</Text>
    {reopening && <Field label={t('reopenReason')} value={reason} onChangeText={setReason} multiline maxLength={300} editable={!command.locked} />}
    <Feedback command={command} t={t} /><Button label={command.confirmed ? t('refresh') : command.locked ? t('retry') : t(reopening ? 'reopenCycle' : 'closeCycle')}
      busy={command.busy} disabled={!reopening && !canClose} onPress={save} />
  </Dialog>;
}
