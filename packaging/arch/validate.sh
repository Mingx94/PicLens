#!/usr/bin/env bash
# No package installation, removal, trash, source edits or fixture cleanup.
set -euo pipefail
mode=${1:-inspect}
source_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
case "$mode" in
  inspect)
    cat /etc/os-release
    uname -m
    printf 'Session=%s Desktop=%s Platform override=%s\n' \
      "${XDG_SESSION_TYPE:-unset}" "${XDG_CURRENT_DESKTOP:-unset}" "${QT_QPA_PLATFORM:-unset}"
    pacman -Q base-devel cmake ninja pkgconf qt6-base qt6-declarative qt6-svg qt6-imageformats qt6-wayland libwebp glib2
    command -v gio
    exit 0 ;;
  build|smoke) ;;
  *) printf 'Usage: bash validate.sh inspect|build|smoke [absolute-piclens-binary]\n' >&2; exit 2 ;;
esac
run=$(mktemp -d /tmp/piclens-arch-validation.XXXXXXXX)
printf 'Evidence retained at: %s\n' "$run"
mkdir -p "$run"/{home,data,config,cache,profile,fixtures}
export HOME="$run/home" XDG_DATA_HOME="$run/data" XDG_CONFIG_HOME="$run/config"
export XDG_CACHE_HOME="$run/cache" PICLENS_DATA_ROOT="$run/profile"
# Keep the desktop runtime directory, display and DBus connection for GUI smoke.
if [[ $mode == build ]]; then
  cmake -S "$source_root/apps/linux" -B "$run/build" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=/usr -DBUILD_TESTING=ON 2>&1 | tee "$run/configure.log"
  cmake --build "$run/build" 2>&1 | tee "$run/build.log"
  QT_QPA_PLATFORM=offscreen ctest --test-dir "$run/build" --output-on-failure \
    --no-tests=error --timeout 120 2>&1 | tee "$run/ctest.log"
  DESTDIR="$run/stage" cmake --install "$run/build" 2>&1 | tee "$run/install-stage.log"
  test -x "$run/stage/usr/bin/piclens"
  test -x "$run/stage/usr/libexec/piclens/piclens-worker"
  desktop-file-validate "$run/stage/usr/share/applications/piclens.desktop"
  appstreamcli validate --no-net "$run/stage/usr/share/metainfo/io.github.Mingx94.PicLens.metainfo.xml"
  find "$run/stage" -type f -print | sort > "$run/installed-files.txt"
else
  binary=${2:?Provide an absolute path to piclens}
  [[ $binary == /* && -x $binary ]] || { echo 'Expected an executable absolute path' >&2; exit 2; }
  # Codec acceptance is separate from this isolated empty-folder launch.
  timeout --kill-after=5s 30s "$binary" --data-root "$run/profile" \
    --folder "$run/fixtures" --smoke-ms 1500 2>&1 | tee "$run/smoke.log"
fi
printf 'Completed %s only. This does not certify desktop/lifecycle acceptance. Evidence: %s\n' "$mode" "$run"
