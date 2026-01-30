# Jellyfin Poster Tags Plugin

Add configurable tags and badges to library posters: resolution (4K, HD), HDR (HDR10, Dolby Vision), premium audio (Dolby Atmos, DTS:X), audio language flags, IMDb and Rotten Tomatoes ratings, and a custom text tag. Preview in the dashboard before saving.

**Repository:** [https://github.com/bestopensors/JF-plugin](https://github.com/bestopensors/JF-plugin)

---

## Step-by-step setup (build and install on Jellyfin)

### 1. Clone the repository

```bash
git clone https://github.com/bestopensors/JF-plugin.git
cd JF-plugin
```

### 2. Requirements

- **.NET 9.0 SDK**  
  - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)  
  - Check: `dotnet --version` (should be 9.x)

- **Jellyfin 10.11.x** (e.g. 10.11.6)  
  - Plugin targets Jellyfin 10.11.6; usually works on 10.11.x.

### 3. Build the plugin

**Debug (for development):**
```bash
dotnet build Jellyfin.Plugin.PosterTags.sln
```

**Release (for installation):**
```bash
dotnet publish Jellyfin.Plugin.PosterTags/Jellyfin.Plugin.PosterTags.csproj -c Release -o publish
```

Output will be in the `publish` folder (and `publish` is in `.gitignore`).

### 4. Install the plugin on Jellyfin

1. **Locate Jellyfin’s plugin directory** on your system:

   | Platform   | Plugins path |
   |-----------|---------------|
   | **Linux** | `~/.local/share/jellyfin/plugins/` or `/var/lib/jellyfin/plugins/` |
   | **Docker** | `/config/plugins/` (inside the container) |
   | **Windows** | `%LOCALAPPDATA%\jellyfin\plugins\` (e.g. `C:\Users\YourName\AppData\Local\jellyfin\plugins\`) |

2. **Create the plugin folder** (if it doesn’t exist):
   ```bash
   mkdir -p /path/to/jellyfin/plugins/Jellyfin.Plugin.PosterTags
   ```
   Use your actual path (e.g. `~/.local/share/jellyfin/plugins/Jellyfin.Plugin.PosterTags`).

3. **Copy the built files** into that folder:
   - Copy **everything** from `publish/` (after the `dotnet publish` step) into  
     `.../plugins/Jellyfin.Plugin.PosterTags/`.
   - Copy **manifest.json** from  
     `Jellyfin.Plugin.PosterTags/manifest.json`  
     into the same `Jellyfin.Plugin.PosterTags` plugin folder (overwrite if it exists).

   **Example (Linux):**
   ```bash
   cp -r publish/* ~/.local/share/jellyfin/plugins/Jellyfin.Plugin.PosterTags/
   cp Jellyfin.Plugin.PosterTags/manifest.json ~/.local/share/jellyfin/plugins/Jellyfin.Plugin.PosterTags/
   ```

4. **Restart Jellyfin** so it loads the plugin.

5. **Enable the plugin**  
   - Open **Dashboard → Plugins → Catalog** (or **Installed**).  
   - Find **Poster Tags** and enable it if it’s disabled.

### 5. Configure and use

1. Go to **Dashboard → Plugins → Poster Tags**.
2. **Libraries:** Select which libraries to process (or leave empty for all).
3. Use the **Live preview** section: click **“Pick random & refresh preview”** to see a sample poster with your current options. Only items with a primary poster image are used.
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
- **Live preview:** Random movie/series from selected libraries; only items with a primary poster are used; loading spinner and clear status messages  
- **Automation:** Optional auto-apply after library scan  

---

## Development

- **Solution:** `Jellyfin.Plugin.PosterTags.sln`  
- **Project:** `Jellyfin.Plugin.PosterTags/Jellyfin.Plugin.PosterTags.csproj`  
- **Target:** .NET 9.0, Jellyfin 10.11.6  

After changing code, run `dotnet publish ...` again and copy the `publish` output and `manifest.json` into your Jellyfin plugin folder, then restart Jellyfin.

---

## License

GPL-3.0-or-later (same as Jellyfin).
