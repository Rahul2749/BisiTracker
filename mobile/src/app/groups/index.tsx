import React from 'react';
import { router } from 'expo-router';
import { HomeScreen } from '../../screens/HomeScreen';
import { translator } from '../../lib/i18n';
import { useLanguage, Workspace } from '../../ui/Workspace';

export default function GroupsRoute() {
  const { language } = useLanguage();
  return <Workspace><HomeScreen t={translator(language)} onNew={() => router.push('/groups/new')}
    onOpen={id => router.push({ pathname: '/groups/[id]', params: { id } })} /></Workspace>;
}
