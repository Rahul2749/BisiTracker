import React from 'react';
import { Redirect } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';

WebBrowser.maybeCompleteAuthSession();
export default function OAuthReturnRoute() { return <Redirect href="/" />; }
