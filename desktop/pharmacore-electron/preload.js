'use strict';
const { contextBridge, ipcRenderer } = require('electron');

// main.js passes the API URL through additionalArguments; Angular reads window.__PHARMACORE_API__ at startup.
const arg = process.argv.find(a => a.startsWith('--pharmacore-api='));
if (arg) contextBridge.exposeInMainWorld('__PHARMACORE_API__', arg.split('=')[1]);

contextBridge.exposeInMainWorld('pharmaCoreDesktop', {
  getConfig: () => ipcRenderer.invoke('desktop:get-config'),
  print: () => ipcRenderer.invoke('desktop:print')
});
