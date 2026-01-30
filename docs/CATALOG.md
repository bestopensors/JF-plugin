# Making Poster Tags Show Up in the Jellyfin Plugins Page

Plugins appear in **Dashboard → Plugins → Catalog** when Jellyfin reads them from a **plugin repository**. There are two ways to get your plugin there.

---

## Option 1: Official Jellyfin Plugin Catalog

Plugins on the default catalog (e.g. Bookshelf, Fanart, Trakt) are hosted at **repo.jellyfin.org** and maintained by the Jellyfin project.

**To have your plugin considered for the official catalog:**

1. **Quality and license**  
   Your plugin should be stable, documented, and use a license compatible with Jellyfin (e.g. GPL-3.0-or-later).

2. **Contribute / submit**  
   - Open an issue or discussion on [Jellyfin GitHub](https://github.com/jellyfin/jellyfin) asking how to submit a plugin for the official repository, or  
   - Check [Jellyfin Docs – Plugins](https://jellyfin.org/docs/general/server/plugins) and the [Plugin Repositories](https://jellyfin.org/posts/plugin-updates) post for current process.  
   - The team will explain how they accept new plugins (often: add your manifest/package to their repo and build pipeline).

3. **Format**  
   Your plugin must match what the official repo expects: correct manifest fields, versioning, and a `.zip` that unzips to the standard plugin layout (e.g. DLLs + `manifest.json`).

There is no “form” to fill; you need to follow the process the Jellyfin team specifies (issue, PR, or doc).

---

## Option 2: Your Own Plugin Repository (Recommended to Start)

You can host your **own** plugin repository. Users add your repository URL once; after that, your plugin shows up in the same Catalog and can be installed/updated from the Plugins page.

### How it works

1. You host a **manifest JSON** file at a public URL (e.g. GitHub raw, or GitHub Pages).  
   That file is a **JSON array** of plugin definitions (see format below).

2. Each plugin entry has a **versions** array. Each version has:
   - **sourceUrl**: direct link to the plugin **.zip** (e.g. GitHub Release asset).
   - **checksum**: MD5 of the .zip file (32-character hex).
   - **targetAbi**: Jellyfin ABI version (e.g. `10.11.0.0`).
   - **version**, **changelog**, **timestamp**.

3. Users go to **Dashboard → Plugins → Repositories**, add your manifest URL, and save.  
   Jellyfin fetches that URL and merges your plugins into the Catalog. Your plugin then appears in **Dashboard → Plugins → Catalog** and can be installed like any other.

### Step-by-step: Host your own repo

1. **Build and create the plugin .zip**
   - From the repo root, run the script that builds and zips the plugin (see below).  
   - It will print the **checksum** and a **manifest entry** you can use.

2. **Upload the .zip**
   - Create a **GitHub Release** (e.g. tag `v1.0.0.0`), attach the generated `.zip` as an asset.  
   - Copy the **asset URL** (e.g. `https://github.com/bestopensors/JF-plugin/releases/download/v1.0.0.0/Jellyfin.Plugin.PosterTags_1.0.0.0.zip`).

3. **Create the catalog manifest**
   - Use the manifest entry printed by the script (or the template in the repo).  
   - Put your **sourceUrl** (the release asset URL) and the **checksum** the script printed.  
   - Save as a JSON file, e.g. `manifest-catalog.json`, in the repo.

4. **Host the manifest**
   - Commit `manifest-catalog.json` to the repo.  
   - Use the **raw** URL for that file, e.g.:  
     `https://raw.githubusercontent.com/bestopensors/JF-plugin/main/manifest-catalog.json`  
   - (If you use a branch other than `main`, replace `main` in the URL.)

5. **Add the repository in Jellyfin**
   - **Dashboard → Plugins → Repositories**  
   - Add a new repository: paste the raw manifest URL.  
   - Save.  
   - Open **Dashboard → Plugins → Catalog** and refresh if needed; **Poster Tags** should appear.

6. **Updates**  
   - For new versions: build, zip, create a new release, update `manifest-catalog.json` with a new entry in **versions** (new version, sourceUrl, checksum, targetAbi, changelog, timestamp), commit, and push.  
   - Users with your repo added will see the update in the Catalog.

### Manifest format (minimal)

Your hosted file should be a **JSON array** of plugins. One plugin can look like this (one version only for brevity):

```json
[
  {
    "guid": "a7b8c9d0-e1f2-3456-7890-abcdef123456",
    "name": "Poster Tags",
    "description": "Add quality, 4K, HD, HDR, audio flags, IMDB and Rotten Tomatoes badges to library item posters. Optional live preview and auto-apply on library scan.",
    "overview": "Embed tags (4K, HD, HDR, Dolby Atmos, DTS:X, ratings, custom tag) onto library posters.",
    "owner": "bestopensors",
    "category": "Library",
    "versions": [
      {
        "version": "1.0.0.0",
        "changelog": "Initial release.",
        "targetAbi": "10.11.0.0",
        "sourceUrl": "https://github.com/bestopensors/JF-plugin/releases/download/v1.0.0.0/Jellyfin.Plugin.PosterTags_1.0.0.0.zip",
        "checksum": "REPLACE_WITH_MD5_FROM_SCRIPT",
        "timestamp": "2025-01-30T12:00:00Z"
      }
    ]
  }
]
```

Replace:

- **sourceUrl** with the real download URL of your .zip (e.g. GitHub Release asset).  
- **checksum** with the MD5 hex string from the build script.  
- **timestamp** with the release time in ISO 8601.  
- **changelog** / **description** / **overview** as you like.

**targetAbi** must match the Jellyfin server version you target (e.g. `10.11.0.0` for Jellyfin 10.11.x). See [Plugin Repositories](https://jellyfin.org/posts/plugin-updates) for details.

---

## Build script (zip + manifest entry)

From the repo root, run:

- **Windows (PowerShell):**  
  `.\scripts\build-repo.ps1`
- **Linux/macOS:**  
  `./scripts/build-repo.sh`

The script will:

1. Build the plugin in Release.  
2. Create a .zip suitable for Jellyfin (plugin contents + manifest).  
3. Compute the MD5 checksum of the .zip.  
4. Print a **manifest version entry** (and optionally the full catalog array) so you can paste it into `manifest-catalog.json` and set **sourceUrl** after uploading the zip to GitHub Releases.

After you upload the zip and update `manifest-catalog.json` with the real **sourceUrl** and **checksum**, host that file (e.g. on GitHub) and add its URL in **Dashboard → Plugins → Repositories** so the plugin shows up on the Jellyfin plugins page.
