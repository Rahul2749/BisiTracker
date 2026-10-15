import React, { createContext, useContext, useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaView } from 'react-native-safe-area-context';
import { developmentAuth, useAuth } from '../auth/session';
import { Language, translator } from '../lib/i18n';
import { Button, colors, s } from './components';

const LanguageContext = createContext<{ language: Language; toggle: () => void }>({ language: 'en', toggle: () => {} });
export function LanguageProvider({ children }: { children: React.ReactNode }) {
  const [language, setLanguage] = useState<Language>('en');
  return <LanguageContext.Provider value={{ language, toggle: () => setLanguage(previous => previous === 'en' ? 'mr' : 'en') }}>{children}</LanguageContext.Provider>;
}
export function useLanguage() { return useContext(LanguageContext); }

export function Workspace({ children }: { children: React.ReactNode }) {
  const { session, signOut } = useAuth();
  const { language, toggle } = useLanguage();
  const t = translator(language);
  return <SafeAreaView style={s.screen}><StatusBar style="dark" />
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
      <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={s.content}>
        <View style={s.between}><View style={s.row}><View style={{ width: 38, height: 38, backgroundColor: colors.green, borderRadius: 12, alignItems: 'center', justifyContent: 'center' }}><Text style={{ fontSize: 25, color: '#fff', fontWeight: '800' }}>b</Text></View><Text style={{ color: colors.ink, fontWeight: '800', fontSize: 20 }}>BisiTracker</Text></View>
          <Button label={language === 'en' ? 'मराठी' : 'English'} kind="secondary" onPress={toggle} /></View>
        {session && <View style={s.between}>{developmentAuth ? <Text style={s.eyebrow}>{t('devLabel')}</Text> : <View />}<Button label={t('signOut')} kind="quiet" onPress={() => { void signOut(); }} /></View>}
        {children}
      </ScrollView>
    </KeyboardAvoidingView>
  </SafeAreaView>;
}
