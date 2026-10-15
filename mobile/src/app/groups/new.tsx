import React from 'react';
import { router } from 'expo-router';
import { CreateGroupScreen } from '../../screens/CreateGroupScreen';
import { translator } from '../../lib/i18n';
import { useLanguage, Workspace } from '../../ui/Workspace';

export default function NewGroupRoute() {
  const { language } = useLanguage();
  return <Workspace><CreateGroupScreen t={translator(language)} language={language} onBack={() => router.replace('/groups')}
    onCreated={id => router.replace({ pathname: '/groups/[id]', params: { id } })} /></Workspace>;
}
