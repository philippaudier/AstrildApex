# Google Fonts Integration Guide

## Overview
AstrildApex Editor now supports **Google Fonts** with automatic download and caching! You can browse 200+ popular Google Fonts directly from the Preferences window.

## ✨ Features

### 🎨 Modern Font Management UI
- **Two Tabs**: System Fonts and Google Fonts
- **Search & Filter**: Find fonts quickly
- **Download Status**: See which fonts are downloaded
- **Live Preview**: Preview fonts with custom size (10-24px)
- **One-Click Download**: Download any Google Font instantly

### 📦 Font Caching
All downloaded Google Fonts are cached locally:
```
%APPDATA%\AstrildApex\GoogleFonts\
```

Fonts persist across editor restarts and are automatically detected.

## 🔑 Setup (API Key Required)

### Option 1: Get Your Own API Key (Recommended)
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the **Google Fonts API**
4. Go to **Credentials** → **Create Credentials** → **API Key**
5. Copy your API key

### Option 2: Use Without API Key (Limited)
The editor will use cached metadata if available. You won't be able to refresh the fonts list from Google, but can still download and use fonts that are already in the cache.

### Configure Your API Key
Open `Editor/Utils/GoogleFontsManager.cs` and replace the placeholder:

```csharp
private static readonly string ApiKey = "YOUR_GOOGLE_FONTS_API_KEY_HERE";
```

**Note**: The API key is stored in code for simplicity. For production, consider using environment variables or secure storage.

## 📖 How to Use

### 1. Open Preferences
- Menu: **Tools** → **Preferences**
- Shortcut: Check your custom shortcuts

### 2. Navigate to Appearance
- Click **Appearance** in the left sidebar
- Scroll down to **Interface Font** section

### 3. Choose Font Source

#### System Fonts Tab
- Browse all fonts installed on your system
- Windows fonts directory: `C:\Windows\Fonts`
- Search by name or family
- Instantly preview and apply

#### Google Fonts Tab
- Browse 200 most popular Google Fonts
- Search by name
- Download status indicators:
  - **✓ Downloaded** - Ready to use
  - **⏳ Downloading...** - In progress
  - **⬇ Download** - Click to download
- Click **🔄 Refresh List** to fetch latest fonts from Google API

### 4. Preview & Apply
1. Select a font from the list
2. Adjust size with slider (10-24px) or quick buttons (S/M/L/XL)
3. Preview text shows:
   - The quick brown fox jumps over the lazy dog
   - ABCDEFGHIJKLMNOPQRSTUVWXYZ
   - abcdefghijklmnopqrstuvwxyz
   - 0123456789 !@#$%^&*()
4. Click **Apply** to save
5. **Restart editor** to see changes

## 🎯 Popular Fonts to Try

### Code/Monospace
- Roboto Mono
- Source Code Pro
- JetBrains Mono (if added to list)
- Fira Code
- IBM Plex Mono

### UI/Sans-Serif
- Roboto
- Open Sans
- Lato
- Montserrat
- Inter

### Readable/Elegant
- Merriweather
- Lora
- PT Serif
- Crimson Text

## 🔧 Technical Details

### Font Formats
- Downloaded as `.ttf` (TrueType Font)
- Variant: `regular` by default
- File naming: `{FontFamily}-{variant}.ttf`

### Caching System
```
GoogleFonts/
  ├── fonts_metadata.json    # Cached fonts list
  ├── Roboto-regular.ttf
  ├── OpenSans-regular.ttf
  └── ...
```

### API Rate Limits
- Google Fonts API has generous free tier
- Metadata is cached to minimize API calls
- Only refresh when you need updated fonts list

### Font Loading
- Fonts are loaded via ImGui font atlas
- Requires editor restart to rebuild atlas
- Future: Hot-reload support planned

## 🐛 Troubleshooting

### "No fonts available"
- Check if API key is configured
- Click **🔄 Refresh List** to fetch from API
- Check logs in Console panel

### "Download failed"
- Check internet connection
- Verify API key is valid and has Fonts API enabled
- Check firewall/proxy settings

### "Font doesn't appear after restart"
- Verify font file exists in cache directory
- Check Editor.log for font loading errors
- Ensure font path is correctly saved in EditorSettings

### Cache Issues
To reset cache:
1. Close editor
2. Delete: `%APPDATA%\AstrildApex\GoogleFonts\`
3. Reopen editor and refresh fonts list

## 🚀 Future Enhancements

- [ ] Support for font variants (Bold, Italic, etc.)
- [ ] Hot-reload fonts without restart
- [ ] Font preview with actual font rendering (not just scale)
- [ ] Custom font upload support
- [ ] Font metrics display (height, kerning, etc.)
- [ ] Batch download (download multiple fonts at once)
- [ ] Font favorites/bookmarks
- [ ] Font categories filter (serif, sans-serif, monospace, etc.)

## 📝 Notes

- Google Fonts are licensed under open source licenses (mostly OFL)
- Downloaded fonts are for personal/project use
- Check individual font licenses for commercial use
- Font files are not included in editor distribution (downloaded on-demand)

---

**Enjoy your new fonts!** 🎨✨
