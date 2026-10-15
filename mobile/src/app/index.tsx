import React from 'react';
import { SignIn } from '../auth/SignIn';
import { translator } from '../lib/i18n';
import { useLanguage, Workspace } from '../ui/Workspace';

export default function SignInRoute() {
  const { language } = useLanguage();
  return <Workspace><SignIn t={translator(language)} /></Workspace>;
}
