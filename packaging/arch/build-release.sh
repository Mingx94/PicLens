#!/usr/bin/env bash
# Runs only inside the disposable Arch container. /work is the export directory.
set -euo pipefail
[[ $(id -u) == 0 && -f /work/PKGBUILD ]] || exit 2
pacman -Syu --noconfirm --needed base-devel cmake ninja pkgconf \
  qt6-base qt6-declarative qt6-svg qt6-imageformats qt6-wayland libwebp glib2
useradd --create-home --uid "${BUILD_UID:?Missing host runner uid}" builder
chown -R builder:builder /work
pacman -Q > /work/build-environment.txt
cd /work
# Keep local makepkg checks available; this release job intentionally omits them.
runuser -u builder -- env PICLENS_BUILD_TESTING=OFF CMAKE_BUILD_PARALLEL_LEVEL=2 \
  makepkg --noconfirm --nocheck
