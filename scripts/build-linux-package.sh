#!/usr/bin/env bash
set -euo pipefail

kind="${1:?Usage: build-linux-package.sh deb|rpm [version]}"
version="${2:-}"
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
if [[ -z "$version" ]]; then
  version="$(cargo metadata --no-deps --format-version 1 | jq -r '.packages[] | select(.name == "piclens-gpui") | .version')"
fi
case "$kind" in deb) package_type=deb; arch=amd64 ;; rpm) package_type=rpm; arch=x86_64 ;; *) echo "Unknown package kind: $kind" >&2; exit 2 ;; esac
command -v fpm >/dev/null || { echo "fpm is required" >&2; exit 2; }
cargo build --release --locked -p piclens-gpui
stage="dist/$kind-root"
rm -rf -- "$stage"
install -Dm755 target/release/piclens-gpui "$stage/usr/bin/PicLens"
install -Dm644 packaging/piclens.desktop "$stage/usr/share/applications/piclens.desktop"
install -Dm644 packaging/piclens.metainfo.xml "$stage/usr/share/metainfo/piclens.metainfo.xml"
install -Dm644 assets/Square150x150Logo.scale-200.png "$stage/usr/share/icons/hicolor/300x300/apps/piclens.png"
install -Dm644 LICENSE "$stage/usr/share/doc/piclens/LICENSE"
install -Dm644 README.md "$stage/usr/share/doc/piclens/README.md"
install -Dm644 assets/Fonts/NotoSansCJKtc-OFL.txt "$stage/usr/share/doc/piclens/NotoSansCJKtc-OFL.txt"
output="dist/PicLens-$version-linux-x86_64.$kind"
rm -f -- "$output" "$output.sha256"
fpm -s dir -t "$package_type" -n piclens -v "$version" -a "$arch" \
  --description "Desktop image viewer and organizer" --license MIT --vendor PicLens \
  --url "https://github.com/piclens/piclens" -C "$stage" -p "$output" .
(cd dist && sha256sum "$(basename "$output")" > "$(basename "$output").sha256")
echo "$kind package ready: $output (unsigned)"
