#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
    echo "Linux portable builds must run on Linux." >&2
    exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
qt_root="$(cd -- "$script_dir/.." && pwd)"
repo_root="$qt_root"
build_dir="${PICLENS_QT_BUILD_DIR:-$qt_root/build/release}"
artifact_root="$repo_root/artifacts/qt-portable"
output_dir="${PICLENS_QT_OUTPUT_DIR:-$artifact_root/PicLens-linux-x64}"
qt_source_root=""
if [[ -n "${QT_ROOT_DIR:-}" ]]; then
    qt_source_root="$(dirname -- "$QT_ROOT_DIR")/Src"
fi

case "$(realpath -m -- "$output_dir")" in
    "$(realpath -m -- "$artifact_root")"/*) ;;
    *)
        echo "Portable output must stay below $artifact_root" >&2
        exit 3
        ;;
esac

(cd -- "$qt_root" && cmake --preset release \
    -DPICLENS_SYSTEM_PACKAGE=OFF \
    -DPICLENS_USE_SYSTEM_QT=OFF \
    -DPICLENS_QT_SOURCE_ROOT="$qt_source_root")
cmake --build "$build_dir"
ctest --test-dir "$build_dir" --output-on-failure

rm -rf -- "$output_dir"
cmake --install "$build_dir" --prefix "$output_dir"

for qt_module in qtbase qtdeclarative; do
    license_dir="$output_dir/share/licenses/Qt/$qt_module"
    if [[ ! -d "$license_dir" ]] || ! find "$license_dir" -type f -print -quit | grep -q .; then
        echo "Qt $qt_module license texts were not installed from $qt_source_root" >&2
        exit 4
    fi
done

platform_plugin=""
if [[ -f "$output_dir/plugins/platforms/libqoffscreen.so" ]]; then
    platform_plugin="offscreen"
elif [[ -f "$output_dir/plugins/platforms/libqxcb.so" ]]; then
    platform_plugin="xcb"
    if [[ -z "${DISPLAY:-}" ]]; then
        echo "The packaged Qt runtime only provides xcb; run this verifier under Xvfb." >&2
        exit 5
    fi
else
    echo "No supported Qt platform plugin was deployed." >&2
    exit 5
fi

executable="$output_dir/bin/PicLens"
if [[ ! -x "$executable" ]]; then
    echo "Installed PicLens executable was not found: $executable" >&2
    exit 6
fi

smoke_root="$artifact_root/.linux-smoke"
rm -rf -- "$smoke_root"
mkdir -p -- "$smoke_root/home" "$smoke_root/runtime" "$smoke_root/data"
chmod 700 "$smoke_root/runtime"
env -i \
    HOME="$smoke_root/home" \
    PATH="/usr/bin:/bin" \
    DISPLAY="${DISPLAY:-}" \
    XAUTHORITY="${XAUTHORITY:-}" \
    QT_QPA_PLATFORM="$platform_plugin" \
    XDG_RUNTIME_DIR="$smoke_root/runtime" \
    "$executable" \
        --smoke-ms 750 \
        --data-root "$smoke_root/data" \
        --folder "$repo_root/assets"
rm -rf -- "$smoke_root"

file_count="$(find "$output_dir" -type f -printf '.' | wc -c)"
total_bytes="$(find "$output_dir" -type f -printf '%s\n' | awk '{ total += $1 } END { print total + 0 }')"
sha256="$(sha256sum "$executable" | awk '{ print toupper($1) }')"
echo "Portable output: $output_dir"
echo "Files: $file_count"
echo "Bytes: $total_bytes"
echo "PicLens SHA256: $sha256"
