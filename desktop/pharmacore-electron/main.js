'use strict';
const { app, BrowserWindow, ipcMain, shell } = require('electron');
const path = require('path');

const isDev = process.argv.includes('--dev');
// API the desktop app talks to. In production this is the Local Branch API (see docs/architecture).
const apiUrl = process.env.PHARMACORE_API_URL || 'http://localhost:5080';
const devServerUrl = 'http://localhost:4200';

function createWindow() {
  const win = new BrowserWindow({
    width: 1366, height: 820, minWidth: 1024, minHeight: 650,
    title: 'PharmaCore',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      additionalArguments: [`--pharmacore-api=${apiUrl}`],
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });

  // Never navigate the app window to foreign origins; open them in the system browser.
  win.webContents.setWindowOpenHandler(({ url }) => { shell.openExternal(url); return { action: 'deny' }; });

  if (isDev) win.loadURL(devServerUrl);
  else win.loadFile(path.join(__dirname, 'app', 'index.html'));
}

// Electron-specific capabilities live here, behind a narrow IPC surface. Angular only sees window.pharmaCoreDesktop.
ipcMain.handle('desktop:get-config', () => ({ apiUrl, version: app.getVersion() }));
ipcMain.handle('desktop:print', async (event) => {
  // Prints the current page to the default printer. Receipt-printer specifics are NOT implemented yet.
  return new Promise(resolve => event.sender.print({ silent: false, printBackground: true }, ok => resolve(ok)));
});

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
