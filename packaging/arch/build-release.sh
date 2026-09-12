#!/usr/bin/env bash
# Local: sudo ./packaging/arch/build-release.sh
# Release container: BUILD_UID=<host uid>, with an exported source in /work.
set -euo pipefail

die() { printf '錯誤：%s\n' "$*" >&2; exit 2; }
if [[ ${1:-} == --help && $# == 1 ]]; then
  printf '用法：sudo %s\n本機先清空 repo/dist/arch/，再以原使用者在該目錄匯出來源及建置（不跑測試）。\n' "$0"
  exit 0
fi
[[ $# == 0 ]] || die '不接受額外參數；使用 --help 查看用法。'
[[ $(id -u) == 0 ]] || die "請使用 sudo 執行：sudo $0"
command -v pacman >/dev/null || die '需要 Arch Linux 或提供 pacman 的相容環境。'

dependencies=(base-devel cmake ninja pkgconf qt6-base qt6-declarative
  qt6-svg qt6-imageformats qt6-wayland libwebp glib2)
if [[ -n ${BUILD_UID:-} ]]; then
  # Preserve the GitHub Actions container entry point.
  [[ $BUILD_UID =~ ^[1-9][0-9]*$ ]] || die 'BUILD_UID 必須是非 root 的使用者 UID。'
  [[ -f /work/PKGBUILD ]] || die '容器模式需要 /work/PKGBUILD；請先匯出來源並掛載 /work。'
  work_dir=/work
  output_dir=$work_dir
  if ! build_user=$(id -nu "$BUILD_UID" 2>/dev/null); then
    useradd --create-home --user-group --uid "$BUILD_UID" builder
    build_user=builder
  fi
  chown -R "$build_user:$(id -gn "$build_user")" "$work_dir"
else
  [[ ${SUDO_UID:-} =~ ^[1-9][0-9]*$ ]] || die '本機請由一般使用者透過 sudo 執行；容器則設定 BUILD_UID。'
  build_user=$(id -nu "$SUDO_UID") || die '找不到執行 sudo 的原使用者。'
  source_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd -P)
  [[ -e $source_root/.git && -f $source_root/packaging/arch/handoff.py ]] || die '本機模式需要完整的 PicLens Git 工作樹。'
  dependencies+=(git python)
fi

# Avoid upgrading a prepared host just to rebuild. If dependencies are missing,
# use a full Arch update rather than refreshing only part of the system.
if ! pacman -T "${dependencies[@]}" >/dev/null; then
  pacman -Syu --noconfirm --needed "${dependencies[@]}"
fi

as_builder() { runuser -u "$build_user" -- "$@"; }
if [[ -z ${BUILD_UID:-} ]]; then
  # Git/export/build must run as the caller, never root. Do not chown the repo.
  work_dir=$source_root/dist/arch
  output_dir=$work_dir
  [[ ! -L $source_root/dist && ! -L $work_dir ]] || die 'dist 或 dist/arch 是符號連結，已停止清理。'
  as_builder mkdir -p "$source_root/dist"
  # This fixed directory is disposable; remove hidden files and prior builds too.
  cd -- "$source_root"
  printf '清空建置目錄：%s\n' "$work_dir"
  as_builder rm -rf -- "$work_dir"
  as_builder python3 "$source_root/packaging/arch/handoff.py" --output "$work_dir"
fi
printf '建置目錄：%s\n建置使用者：%s\n' "$work_dir" "$build_user"
pacman -Q | as_builder tee "$work_dir/build-environment.txt" >/dev/null
cd -- "$work_dir"
# Keep validate.sh and ordinary makepkg checks available; release omits them.
as_builder env PICLENS_BUILD_TESTING=OFF CMAKE_BUILD_PARALLEL_LEVEL=2 \
  makepkg --noconfirm --nocheck
package_list=$(as_builder makepkg --packagelist)
install_package=
while IFS= read -r package; do
  package_name=${package##*/}
  [[ $package_name == *.pkg.tar* ]] || die "無法辨識套件檔名：$package_name"
  package_id=$(as_builder pacman -Qqp "$package")
  # Rename the actual build output, retaining version, pkgrel and architecture.
  output_package="$output_dir/${package_name%.pkg.tar*}"
  as_builder mv -- "$package" "$output_package"
  if [[ -f $package.sig ]]; then
    as_builder mv -- "$package.sig" "$output_package.sig"
  fi
  printf '建置成品：%s\n' "$output_package"
  if [[ $package_id == piclens ]]; then install_package=$output_package; fi
done <<< "$package_list"
[[ -n $install_package ]] || die '找不到 piclens 主套件。'
printf '安裝套件：%s\n' "$install_package"
