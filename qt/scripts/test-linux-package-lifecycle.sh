#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
    echo "Linux package lifecycle testing requires Linux." >&2
    exit 2
fi

package_kind=""
package_path=""
expected_executable=""
while (($#)); do
    case "$1" in
        --deb|--rpm)
            package_kind="${1#--}"
            package_path="${2:?A package path is required}"
            shift 2
            ;;
        --expected-executable)
            expected_executable="${2:?An expected executable path is required}"
            shift 2
            ;;
        *)
            echo "Unknown argument: $1" >&2
            exit 2
            ;;
    esac
done
if [[ -z "$package_kind" || -z "$package_path" ]]; then
    echo "Usage: $0 (--deb file.deb | --rpm file.rpm) [--expected-executable path]" >&2
    exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
qt_root="$(cd -- "$script_dir/.." && pwd)"
repo_root="$(cd -- "$qt_root/.." && pwd)"
package_path="$(realpath -- "$package_path")"
if [[ -n "$expected_executable" ]]; then
    expected_executable="$(realpath -- "$expected_executable")"
fi
artifact_root="$repo_root/artifacts/linux-package-lifecycle"
profile_root="$artifact_root/profile"
runtime_root="$artifact_root/runtime"
installed_executable="/opt/piclens/bin/PicLens"
desktop_entry="/usr/share/applications/piclens.desktop"
installed_icon="/usr/share/icons/hicolor/300x300/apps/piclens.png"

run_root() {
    if ((EUID == 0)); then
        "$@"
    else
        sudo "$@"
    fi
}

installed=false
remove_package() {
    if [[ "$package_kind" == "deb" ]]; then
        run_root apt-get remove --yes piclens
    else
        run_root dnf remove --assumeyes piclens
    fi
}
cleanup() {
    if [[ "$installed" == true ]]; then
        remove_package || true
    fi
}
trap cleanup EXIT

rm -rf -- "$artifact_root"
mkdir -p -- "$profile_root/Thumbnails" "$runtime_root"
chmod 700 "$runtime_root"
printf 'PicLens package must preserve this profile\n' > \
    "$profile_root/Thumbnails/package-profile-sentinel.bin"

if [[ "$package_kind" == "deb" ]]; then
    command -v apt-get >/dev/null
    run_root apt-get install --yes "$package_path"
else
    command -v dnf >/dev/null
    run_root dnf install --assumeyes "$package_path"
fi
installed=true

for installed_path in "$installed_executable" "$desktop_entry" "$installed_icon"; do
    if [[ ! -f "$installed_path" ]]; then
        echo "Expected installed package path is missing: $installed_path" >&2
        exit 3
    fi
done
if [[ -n "$expected_executable" ]]; then
    expected_hash="$(sha256sum "$expected_executable" | awk '{print $1}')"
    installed_hash="$(sha256sum "$installed_executable" | awk '{print $1}')"
    if [[ "$installed_hash" != "$expected_hash" ]]; then
        echo "Installed executable does not match the portable artifact." >&2
        exit 4
    fi
fi

env \
    PICLENS_DATA_ROOT="$profile_root" \
    QT_QPA_PLATFORM=offscreen \
    XDG_RUNTIME_DIR="$runtime_root" \
    "$installed_executable" \
        --smoke-ms 1500 \
        --folder "$repo_root/PicLens/Assets"

profile_before="$(find "$profile_root" -type f -print0 | sort -z | xargs -0 sha256sum)"
remove_package
installed=false

for removed_path in "$installed_executable" "$desktop_entry" "$installed_icon"; do
    if [[ -e "$removed_path" ]]; then
        echo "Package path remained after removal: $removed_path" >&2
        exit 5
    fi
done
profile_after="$(find "$profile_root" -type f -print0 | sort -z | xargs -0 sha256sum)"
if [[ "$profile_after" != "$profile_before" ]]; then
    echo "Package removal changed the isolated PicLens profile." >&2
    exit 6
fi

echo "Linux $package_kind lifecycle smoke passed"
echo "  Install/launch/remove: passed"
echo "  Desktop integration: passed"
echo "  User profile preservation: passed"
