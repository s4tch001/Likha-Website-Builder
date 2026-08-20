# Likha - Website Builder

Likha is a Windows desktop visual website builder for creating responsive pages through drag and
drop. Build layouts on the canvas, edit text and styles, organize pages and layers, import local
assets, and export the finished project as clean HTML/CSS/JavaScript or React source code.

## System requirements

- 64-bit Windows 10 or Windows 11
- Microsoft Edge WebView2 Runtime (already included with current Windows installations)
- About 250 MB of free disk space

The installer is self-contained. You do not need to install .NET, Node.js, Next.js, React, or
TypeScript to use the app.

## Install

1. Download `Likha-<version>-win-x64-setup.exe`.
2. Run the installer. This personal build is currently unsigned, so Windows may show a SmartScreen
   warning. Only continue when the file came from the official GitHub Release and its SHA-256
   matches the accompanying `.sha256` file.
3. Keep **Create a desktop shortcut** selected, then choose **Install**.
4. Open **Likha - Website Builder** from the Desktop or Start Menu shortcut.

The default installation is for the current Windows user and does not require administrator
permission.

## Use the app

1. Create a new project or open an existing project folder.
2. Drag elements or reusable blocks from the Components panel onto the canvas.
3. Select an element to edit its text, position, size, colors, spacing, and other styles in the
   Properties panel.
4. Use the Layers and Pages panels to organize the document. Switch breakpoints to check responsive
   layouts.
5. Import images, SVGs, fonts, video, audio, or documents through the Assets panel.
6. Save regularly. Undo and redo are available from the Edit menu and keyboard shortcuts.
7. Choose **Preview** to open the current project in your default browser on a temporary localhost
   address. The button changes to **Stop Preview** while it is running. Stopping Preview or closing
   Likha shuts down the local server and cleans its temporary files.
8. Use Export when the website is ready, then choose static HTML/CSS/JavaScript or React output.

Project files and imported assets stay in the project folder you choose. Uninstalling Likha removes
the application but does not delete those project folders.

## Uninstall

Open **Windows Settings → Apps → Installed apps**, find **Likha - Website Builder**, and choose
**Uninstall**.
