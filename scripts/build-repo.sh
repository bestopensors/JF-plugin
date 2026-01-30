#!/usr/bin/env bash
# Build Poster Tags plugin and create a .zip for Jellyfin plugin repository.
# Run from repo root. Outputs: dist/Jellyfin.Plugin.PosterTags_<version>.zip and prints manifest entry.

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/Jellyfin.Plugin.PosterTags"
PUBLISH_DIR="$REPO_ROOT/publish"
DIST_DIR="$REPO_ROOT/dist"

# Read version from manifest.json (requires jq)
VERSION=$(jq -r '.version' "$PROJECT_DIR/manifest.json")
NAME=$(jq -r '.name' "$PROJECT_DIR/manifest.json")
ZIP_NAME="Jellyfin.Plugin.PosterTags_${VERSION}.zip"

echo "Building $NAME v$VERSION..."
dotnet publish "$PROJECT_DIR/Jellyfin.Plugin.PosterTags.csproj" -c Release -o "$PUBLISH_DIR"

# Copy manifest into publish
cp "$PROJECT_DIR/manifest.json" "$PUBLISH_DIR/manifest.json"

# Create zip
mkdir -p "$DIST_DIR"
(cd "$PUBLISH_DIR" && zip -r "$DIST_DIR/$ZIP_NAME" . -x "*.pdb")

# Copy to releases/ for direct raw URL (avoids GitHub release redirect 404 in Jellyfin)
RELEASES_DIR="$REPO_ROOT/releases"
mkdir -p "$RELEASES_DIR"
cp "$DIST_DIR/$ZIP_NAME" "$RELEASES_DIR/$ZIP_NAME"

# MD5 (32-char lowercase)
if command -v md5sum &>/dev/null; then
  HASH=$(md5sum "$DIST_DIR/$ZIP_NAME" | awk '{ print $1 }')
elif command -v md5 &>/dev/null; then
  HASH=$(md5 -q "$DIST_DIR/$ZIP_NAME")
else
  echo "Warning: md5sum/md5 not found, cannot compute checksum"
  HASH=""
fi

TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
echo ""
echo "Created: $DIST_DIR/$ZIP_NAME"
echo "Copied to: $RELEASES_DIR/$ZIP_NAME (use this URL in catalog for reliable install)"
echo "MD5: $HASH"
echo ""
echo "Add this repository in Jellyfin: Dashboard -> Plugins -> Repositories"
echo "Use the URL to your manifest-catalog.json (e.g. raw GitHub URL)."
echo ""
echo "Recommended sourceUrl (direct, no redirects):"
echo "  https://raw.githubusercontent.com/bestopensors/JF-plugin/main/releases/$ZIP_NAME"
echo ""
echo "Paste this into manifest-catalog.json:"
echo "  {"
echo "    \"version\": \"$VERSION\","
echo "    \"changelog\": \"See https://github.com/bestopensors/JF-plugin/releases\","
echo "    \"targetAbi\": \"10.11.0.0\","
echo "    \"sourceUrl\": \"https://raw.githubusercontent.com/bestopensors/JF-plugin/main/releases/$ZIP_NAME\","
echo "    \"checksum\": \"$HASH\","
echo "    \"timestamp\": \"$TIMESTAMP\""
echo "  }"
