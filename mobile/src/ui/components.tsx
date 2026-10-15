import React from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, TextInputProps, View } from 'react-native';

export const colors = {
  bg: '#F6F4EE', paper: '#FFFFFF', ink: '#203F35', muted: '#6D7870', green: '#245E49',
  lightGreen: '#E7EFE6', line: '#DFE4DA', orange: '#B7602D', lightOrange: '#FFF0DD', red: '#AC3C35', lightRed: '#FDECE8',
};

export function Button({ label, onPress, busy = false, disabled = false, kind = 'primary' }: {
  label: string; onPress: () => void; busy?: boolean; disabled?: boolean; kind?: 'primary' | 'secondary' | 'quiet' | 'danger';
}) {
  return <Pressable accessibilityRole="button" accessibilityLabel={label} accessibilityState={{ disabled: disabled || busy, busy }}
    disabled={disabled || busy} onPress={onPress} style={({ pressed }) => [s.button,
      kind === 'primary' && s.primary, kind === 'secondary' && s.secondary, kind === 'quiet' && s.quiet,
      kind === 'danger' && s.danger, (disabled || busy) && { opacity: .55 }, pressed && { opacity: .75 }]}>
    {busy ? <ActivityIndicator color={kind === 'primary' ? '#fff' : colors.green} /> : <Text style={[s.buttonText, kind === 'primary' && { color: '#fff' }, kind === 'danger' && { color: colors.red }]}>{label}</Text>}
  </Pressable>;
}

export function Field({ label, ...props }: TextInputProps & { label: string }) {
  return <View style={{ gap: 7 }}><Text style={s.label}>{label}</Text><TextInput accessibilityLabel={label}
    placeholderTextColor="#879187" autoCapitalize="sentences" {...props} style={[s.input, props.multiline && { minHeight: 115, textAlignVertical: 'top' }, props.style]} /></View>;
}

export function Notice({ children, error = false }: { children: React.ReactNode; error?: boolean }) {
  return <View accessibilityRole={error ? 'alert' : undefined} style={[s.notice, error && { backgroundColor: colors.lightRed }]}>
    <Text style={[s.body, { color: error ? colors.red : colors.muted }]}>{children}</Text>
  </View>;
}

export function Card({ children }: { children: React.ReactNode }) { return <View style={s.card}>{children}</View>; }

export function Dialog({ title, onClose, closeLabel, children, busy = false }: {
  title: string; onClose: () => void; closeLabel: string; children: React.ReactNode; busy?: boolean;
}) {
  return <Modal visible transparent animationType="fade" onRequestClose={() => { if (!busy) onClose(); }}>
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={s.scrim}>
      <View accessibilityViewIsModal role="dialog" accessibilityLabel={title} style={s.dialog}>
        <View style={s.row}><Text accessibilityRole="header" style={[s.heading, { flex: 1 }]}>{title}</Text><Button label={closeLabel} kind="quiet" disabled={busy} onPress={onClose} /></View>
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={{ gap: 18, paddingBottom: 16 }}>{children}</ScrollView>
      </View>
    </KeyboardAvoidingView>
  </Modal>;
}

export const s = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.bg }, content: { width: '100%', maxWidth: 780, alignSelf: 'center', padding: 22, gap: 22, paddingBottom: 55 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 }, between: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 10 },
  title: { fontSize: 31, fontWeight: '800', color: colors.ink, letterSpacing: -.7, lineHeight: 40 },
  heading: { fontSize: 20, fontWeight: '700', color: colors.ink }, body: { fontSize: 15, color: colors.muted, lineHeight: 23 },
  small: { fontSize: 12, color: colors.muted, lineHeight: 19 }, eyebrow: { fontSize: 11, fontWeight: '800', letterSpacing: 1.5, color: colors.green },
  label: { fontSize: 13, fontWeight: '600', color: colors.ink },
  card: { backgroundColor: colors.paper, borderRadius: 20, borderWidth: 1, borderColor: colors.line, padding: 20, gap: 16 },
  button: { minHeight: 44, borderRadius: 12, paddingHorizontal: 17, paddingVertical: 11, alignItems: 'center', justifyContent: 'center' },
  primary: { backgroundColor: colors.green }, secondary: { backgroundColor: colors.lightGreen }, quiet: { backgroundColor: 'transparent', paddingHorizontal: 9 }, danger: { backgroundColor: colors.lightRed },
  buttonText: { color: colors.green, fontSize: 14, fontWeight: '700', textAlign: 'center' },
  input: { backgroundColor: '#FAFBF8', borderWidth: 1, borderColor: colors.line, borderRadius: 12, paddingHorizontal: 14, paddingVertical: 13, fontSize: 16, color: colors.ink, minHeight: 48 },
  notice: { borderRadius: 12, backgroundColor: '#ECEFE6', padding: 14 },
  scrim: { flex: 1, backgroundColor: '#14251DB3', justifyContent: 'center', alignItems: 'center', padding: 18 },
  dialog: { backgroundColor: colors.paper, borderRadius: 22, padding: 22, gap: 20, width: '100%', maxWidth: 530, maxHeight: '90%' },
  divider: { height: 1, backgroundColor: colors.line },
});
