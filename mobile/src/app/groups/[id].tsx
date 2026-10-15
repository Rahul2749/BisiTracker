import React from 'react';
import { router, useLocalSearchParams } from 'expo-router';
import { LedgerScreen } from '../../screens/LedgerScreen';
import { useLanguage, Workspace } from '../../ui/Workspace';

export default function LedgerRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { language } = useLanguage();
  return <Workspace><LedgerScreen key={id} id={id} language={language} onBack={() => router.replace('/groups')} /></Workspace>;
}
