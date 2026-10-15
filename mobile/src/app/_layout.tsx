import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { Stack } from 'expo-router';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AuthProvider, useAuth } from '../auth/session';
import { LanguageProvider } from '../ui/Workspace';
import { colors } from '../ui/components';

export default function RootLayout() {
  return <SafeAreaProvider><AuthProvider><LanguageProvider><Routes /></LanguageProvider></AuthProvider></SafeAreaProvider>;
}

function Routes() {
  const { session, initializing } = useAuth();
  if (initializing) return <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg }}><ActivityIndicator color={colors.green} /></View>;
  return <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.bg } }}>
    <Stack.Protected guard={!session}><Stack.Screen name="index" /><Stack.Screen name="oauth" /></Stack.Protected>
    <Stack.Protected guard={!!session}><Stack.Screen name="groups/index" /><Stack.Screen name="groups/new" /><Stack.Screen name="groups/[id]" /></Stack.Protected>
  </Stack>;
}
