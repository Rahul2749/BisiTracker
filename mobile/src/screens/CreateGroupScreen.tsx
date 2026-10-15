import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { api } from '../auth/session';
import { CreateGroup } from '../api/client';
import { Language, translator } from '../lib/i18n';
import { indiaDate, MAX_CONTRIBUTION_PAISE, parseRupees, validDate } from '../lib/money';
import { useCommand } from '../lib/useCommand';
import { Button, Card, Field, Notice, s } from '../ui/components';

export function CreateGroupScreen({ t, language, onBack, onCreated }: {
  t: ReturnType<typeof translator>; language: Language; onBack: () => void; onCreated: (id: string) => void;
}) {
  const [name, setName] = useState('');
  const [amount, setAmount] = useState('');
  const [startMonth, setMonth] = useState(indiaDate().slice(0, 7));
  const [dueDay, setDueDay] = useState('10');
  const [memberNames, setMembers] = useState('');
  const command = useCommand<{ groupId: string; entityId: string | null }>();
  function submit() {
    if (command.locked) { void command.run(() => { throw new Error('Pending command missing'); }, result => onCreated(result.groupId)); return; }
    const contributionPaise = parseRupees(amount);
    const names = memberNames.split('\n').map(x => x.trim()).filter(Boolean);
    if (!name.trim() || name.trim().length > 80 || !contributionPaise || contributionPaise > MAX_CONTRIBUTION_PAISE ||
      !/^\d{4}-\d{2}$/.test(startMonth) || !validDate(`${startMonth}-01`) || Number(startMonth.slice(0, 4)) < 2000 || Number(startMonth.slice(0, 4)) > 2090 ||
      !/^\d{1,2}$/.test(dueDay) || Number(dueDay) < 1 || Number(dueDay) > 28 || names.length > 50 || names.some(x => x.length > 80)) {
      command.setError(t('createInvalid')); return;
    }
    const body: Omit<CreateGroup, 'operationId'> = { name: name.trim(), contributionPaise, startMonth: `${startMonth}-01`, dueDay: Number(dueDay), language, memberNames: names };
    void command.run(operationId => () => api.create({ ...body, operationId }), result => onCreated(result.groupId));
  }
  return <View style={{ gap: 22 }}>
    <View style={s.row}><Button label={`← ${t('back')}`} kind="quiet" disabled={command.locked || command.busy} onPress={onBack} /><Text style={s.heading}>{t('newGroup')}</Text></View>
    <Card><Field label={t('name')} placeholder={t('namePlaceholder')} value={name} onChangeText={setName} maxLength={80} editable={!command.locked} />
      <Field label={t('contribution') + ' (₹)'} placeholder="1000" value={amount} onChangeText={setAmount} keyboardType="decimal-pad" editable={!command.locked} />
      <View style={{ flexDirection: 'row', gap: 12 }}><View style={{ flex: 1 }}><Field label={t('startMonth')} value={startMonth} onChangeText={setMonth} autoCapitalize="none" maxLength={7} editable={!command.locked} /></View><View style={{ flex: 1 }}><Field label={t('dueDay')} value={dueDay} onChangeText={setDueDay} keyboardType="number-pad" maxLength={2} editable={!command.locked} /></View></View>
      <Field label={t('memberNames')} placeholder="Asha\nMeera\nSunita" value={memberNames} onChangeText={setMembers} multiline editable={!command.locked} />
      <Text style={s.small}>{t('memberHint')}</Text>
      <Text style={s.small}>{t('groupLanguage')}: {language === 'mr' ? 'मराठी' : 'English'}</Text>
      {command.error && <Notice error>{command.error}</Notice>}{command.uncertain && <Notice>{t('uncertain')}</Notice>}
      <Button label={command.locked ? t('retry') : t('create')} busy={command.busy} onPress={submit} />
    </Card>
  </View>;
}
