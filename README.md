# Jellyfin Poster Tags Plugin

Add configurable tags and badges to library posters: resolution (4K, HD), HDR (HDR10, Dolby Vision), premium audio (Dolby Atmos, DTS:X), audio language flags, IMDb and Rotten Tomatoes ratings, and a custom text tag. Preview in the dashboard before saving.

**Repository:** [https://github.com/bestopensors/JF-plugin](https://github.com/bestopensors/JF-plugin)

---

## Install from Jellyfin Plugins Catalog (recommended)

The plugin is delivered via a **plugin repository**. Add the repository once; then the plugin appears in the catalog and you can install it like any other.

### 1. Add the repository URL

1. In Jellyfin go to **Dashboard → Plugins → Repositories**.
2. Click **Add** and paste this URL:

   ```
   https://raw.githubusercontent.com/bestopensors/JF-plugin/main/manifest-catalog.json
   ```

3. Save.

### 2. Install the plugin

1. Go to **Dashboard → Plugins → Catalog**.
2. Find **Poster Tags** and click **Install**.
3. Restart Jellyfin when prompted (or restart the server).
4. The plugin is then available under **Dashboard → Plugins → Poster Tags**.

That’s it. No manual download or file copying.

---

## Manual install (optional)

If you prefer not to use the catalog or the repository is unavailable:

1. **Build** (requires [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)):
   ```bash
   git clone https://github.com/bestopensors/JF-plugin.git
   cd JF-plugin
   dotnet publish Jellyfin.Plugin.PosterTags/Jellyfin.Plugin.PosterTags.csproj -c Release -o publish
   ```
2. **Copy** everything from `publish/` and `Jellyfin.Plugin.PosterTags/manifest.json` into your Jellyfin plugin folder (e.g. `~/.local/share/jellyfin/plugins/Jellyfin.Plugin.PosterTags/` on Linux, or `%LOCALAPPDATA%\jellyfin\plugins\Jellyfin.Plugin.PosterTags\` on Windows).
3. **Restart** Jellyfin and enable **Poster Tags** in **Dashboard → Plugins**.

See the table below for plugin paths on each platform.

| Platform   | Plugins path |
|-----------|---------------|
| **Linux** | `~/.local/share/jellyfin/plugins/` or `/var/lib/jellyfin/plugins/` |
| **Docker** | `/config/plugins/` (inside the container) |
| **Windows** | `%LOCALAPPDATA%\jellyfin\plugins\` |

---

## Configure and use

1. Go to **Dashboard → Plugins → Poster Tags**.
2. **Libraries:** Select which libraries to process (or leave empty for all).
3. Use **Live preview**: click **“Pick random & refresh preview”** to see a sample poster. Only items with a primary poster image are used.
4. Adjust options (resolution, HDR, Dolby Atmos, DTS:X, ratings, custom tag, position, curvature, etc.). The preview updates automatically after a short delay.
5. Click **Save**.
6. **Apply tags:**
   - **Automatic:** Enable **“Apply poster tags automatically when library scan completes”** so new/updated items get tags without running a task.
   - **Manual:** Go to **Dashboard → Scheduled Tasks**, find **“Add Poster Tags”**, and run it to process existing items.

---

## Features

- **Resolution:** SD / HD / FHD / UHD or 480p / 720p / 1080p / 4K  
- **HDR:** HDR10, HDR10+, Dolby Vision, HLG (when detected)  
- **Premium audio:** Dolby Atmos, DTS:X  
- **Other:** Audio language flags, IMDb rating, Rotten Tomatoes, custom text tag  
- **Shape:** Tag curvature from rectangle to pill (slider)  
- **Positions:** Top/bottom, left/center/right for main badges and custom tag  
- **Live preview:** Random movie/series from selected libraries; loading spinner and clear status  
- **Automation:** Optional auto-apply after library scan  

---

## Requirements

- **Jellyfin 10.11.x** (e.g. 10.11.6)  
- For manual build: **.NET 9.0 SDK**  

---

## Development

- **Solution:** `Jellyfin.Plugin.PosterTags.sln`  
- **Project:** `Jellyfin.Plugin.PosterTags/Jellyfin.Plugin.PosterTags.csproj`  

To rebuild and update the plugin zip for new releases, run `scripts/build-repo.ps1` (Windows) or `scripts/build-repo.sh` (Linux/macOS). See **docs/CATALOG.md** for maintaining the plugin repository and manifest.

---

## License

GPL-3.0-or-later (same as Jellyfin).
