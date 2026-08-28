#!/usr/bin/env bash
set -euo pipefail
kind="${1:?Usage: test-linux-package-lifecycle.sh deb|rpm package}"
package="$(realpath -- "${2:?Package path is required}")"
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="$repo_root/artifacts/linux-package-lifecycle"
profile="$artifact_root/profile"
rm -rf -- "$artifact_root"
mkdir -p -- "$profile"
printf preserve > "$profile/profile-preservation.txt"

install_package() {
  if [[ "$kind" == deb ]]; then apt-get install --yes "$package"; else dnf install --assumeyes "$package"; fi
}
remove_package() {
  if [[ "$kind" == deb ]]; then apt-get remove --yes piclens; else dnf remove --assumeyes piclens; fi
}
installed=false
trap 'if [[ "$installed" == true ]]; then remove_package || true; fi' EXIT
install_package
installed=true
test -x /usr/bin/PicLens
test -f /usr/share/applications/piclens.desktop
test -f /usr/share/icons/hicolor/300x300/apps/piclens.png
if command -v xvfb-run >/dev/null 2>&1; then
  xvfb-run -a env PICLENS_DATA_ROOT="$profile" /usr/bin/PicLens --smoke-ms 1500 --folder "$repo_root/assets"
else
  Xvfb :99 -screen 0 1280x800x24 >/tmp/piclens-xvfb.log 2>&1 &
  xvfb_pid=$!
  trap 'kill "$xvfb_pid" 2>/dev/null || true; if [[ "$installed" == true ]]; then remove_package || true; fi' EXIT
  DISPLAY=:99 PICLENS_DATA_ROOT="$profile" /usr/bin/PicLens --smoke-ms 1500 --folder "$repo_root/assets"
  kill "$xvfb_pid"
  wait "$xvfb_pid" 2>/dev/null || true
fi
install_package
remove_package
installed=false
test ! -e /usr/bin/PicLens
test -f "$profile/profile-preservation.txt"
echo "$kind install, launch, replace, uninstall, desktop integration, and profile preservation passed"
