# Jellyfin Poster Tags Plugin

Add configurable tags and badges to library posters: resolution (4K, HD), HDR (HDR10, Dolby Vision), premium audio (Dolby Atmos, DTS:X), audio language flags, IMDb and Rotten Tomatoes ratings, and a custom text tag. Preview in the dashboard before saving.

**Repository:** [https://github.com/bestopensors/JF-plugin](https://github.com/bestopensors/JF-plugin)

---

## Installation (JSON catalog — use this)

Install via the **plugin catalog** by adding this repository. Copy the link below, add it in Jellyfin once, and the plugin will show up in the catalog for one-click install.

### 1. Add the repository (copy/paste this JSON link)

1. In Jellyfin go to **Dashboard → Plugins → Repositories**.
2. Click **Add**.
3. Paste this URL exactly:

   ```
   https://raw.githubusercontent.com/bestopensors/JF-plugin/main/manifest-catalog.json
   ```

4. Save.

### 2. Install the plugin from the catalog

1. Go to **Dashboard → Plugins → Catalog**.
2. Find **Poster Tags** and click **Install**.
3. Restart Jellyfin when prompted (or restart the server).
4. Configure under **Dashboard → Plugins → Poster Tags**.

**That’s the intended way to install:** add the JSON link above, then install from the catalog. No manual download or file copying.

If installation fails with **404** in the logs: the catalog uses a direct zip URL (`releases/` in the repo) so Jellyfin gets a 200 response; avoid GitHub release download URLs if they cause 404 (redirects). See **docs/CATALOG.md** for more troubleshooting.

### Blank Settings page (workaround)

This plugin is set up like [SmartLists](https://github.com/jyourstone/jellyfin-smartlists-plugin) (config page as `config.html` with `EnableInMainMenu = true`). If your Jellyfin uses **Plugin Pages** for plugin config (e.g. SmartLists opens at `/Plugins/{PluginId}/Configuration`), the Settings button should work after updating to 1.0.9. If **Dashboard → Plugins → Poster Tags → Settings** still shows a black/blank screen, open the configuration page directly in the same browser (while logged in):

- **URL:** `http://YOUR_JELLYFIN_SERVER/Plugins/PosterTags/ConfigurationPage`  
  Example: `http://localhost:8096/Plugins/PosterTags/ConfigurationPage` or `https://jellyfin.example.com/Plugins/PosterTags/ConfigurationPage`

On that page you’ll see **“Poster Tags plugin version: 1.0.x”** at the top and in the browser console (`[Poster Tags] Plugin version: 1.0.x`). Use this to confirm that updates are applied. You can also call **`/Plugins/PosterTags/Version`** to get `{"version":"1.0.x"}`. Bookmark the ConfigurationPage URL to access settings until the built-in Settings button is fixed in your Jellyfin version.

**Important — please check these two things:**

1. **Response body:** In DevTools (F12) → **Network** tab → click **Settings** on the plugin → click the request **`configurationpage?name=Poster%20Tags`** → open the **Response** (or **Preview**) sub-tab. Is the response body **completely empty**, or do you see HTML (e.g. the text “Poster Tags” or “POSTERTAGS_CONFIG_PAGE_START”)? If it’s empty, the server is not sending our HTML (embedded resource not found when serving that route).

2. **Workaround URL:** In the same browser (while logged in), open:  
   **`https://YOUR_JELLYFIN_SERVER/Plugins/PosterTags/ConfigurationPage`**  
   (e.g. `https://gledaj.digitalnaspajza.com/Plugins/PosterTags/ConfigurationPage`). Does that page show the Poster Tags settings (libraries, preview, Save button)? If yes, use that URL to configure the plugin; the built-in Settings button is failing due to a client/server quirk (e.g. empty response or loadView expecting a different HTML shape).

### Updates (no GitHub Release needed)

New versions are published by updating the catalog and the zip in the repo (`releases/`). You do **not** need to create a new GitHub Release. After we push a new version (e.g. 1.0.1), **Dashboard → Plugins → Catalog** will show **Update** for Poster Tags if you have an older version installed. Click **Update**, then restart Jellyfin.

### If you can’t uninstall from the UI

If the **Uninstall** button is missing or doesn’t work (e.g. repo was removed before uninstalling), remove the plugin manually:

1. **Stop Jellyfin.**
2. **Open the plugins folder** (e.g. Windows: `%LOCALAPPDATA%\jellyfin\plugins\` or `C:\Program Data\Jellyfin\Server\plugins\`).
3. **Delete** the folder `Jellyfin.Plugin.PosterTags_1.0` or `Jellyfin.Plugin.PosterTags_1.0.1` (any folder whose name starts with `Jellyfin.Plugin.PosterTags_`).
4. **Start Jellyfin.** Then add the repository again (see above) and install from the catalog if you want to reinstall.

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

### Creating a GitHub release (e.g. Initial v1.0)

1. Run `scripts/build-repo.ps1` so `dist/Jellyfin.Plugin.PosterTags_1.0.zip` exists.
2. On GitHub: **Releases → Create a new release**.
3. **Tag:** `v1.0` (create new tag).
4. **Release title:** `Initial v1.0`.
5. **Description:** e.g. "Initial release. Resolution, HDR, premium audio, ratings, custom tag, live preview, auto-apply on scan."
6. **Upload** the file `dist/Jellyfin.Plugin.PosterTags_1.0.zip` as an asset.
7. Publish the release.

After that, the catalog URL `https://raw.githubusercontent.com/bestopensors/JF-plugin/main/manifest-catalog.json` will serve the plugin; users who added that JSON link will see **Poster Tags** in the catalog and can install it.

**If Jellyfin shows version as 1.0.0.0 instead of 1.0:** Ensure the release asset is the zip built *after* the version was set to 1.0 (i.e. `dist/Jellyfin.Plugin.PosterTags_1.0.zip`). Re-run `scripts/build-repo.ps1`, then on GitHub edit the release, remove the old asset, and upload the new zip. Jellyfin may also display versions in 4-part form (1.0 → 1.0.0.0); that is normal and does not affect behavior.

---

## License

GPL-3.0-or-later (same as Jellyfin).
