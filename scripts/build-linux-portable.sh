#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: build-linux-portable.sh [options]

Options:
  --build-dir PATH   Override the CMake build directory.
  --output-dir PATH  Override the portable artifact directory.
  --no-test          Skip CTest after building.
  --skip-smoke       Skip the packaged application smoke check.
  -h, --help         Show this help.

PICLENS_QT_BUILD_DIR and PICLENS_QT_OUTPUT_DIR remain supported as environment defaults.
EOF
}

if [[ "$(uname -s)" != "Linux" ]]; then
    echo "Linux portable builds must run on Linux." >&2
    exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
artifact_root="$repo_root/artifacts/qt-portable"
build_dir="${PICLENS_QT_BUILD_DIR:-$repo_root/build/linux-portable-release}"
output_dir="${PICLENS_QT_OUTPUT_DIR:-$artifact_root/PicLens-linux-x64}"
no_test=false
skip_smoke=false

while (($#)); do
    case "$1" in
        --build-dir)
            build_dir="${2:?A build directory is required}"
            shift 2
            ;;
        --output-dir)
            output_dir="${2:?An output directory is required}"
            shift 2
            ;;
        --no-test)
            no_test=true
            shift
            ;;
        --skip-smoke)
            skip_smoke=true
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

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

cmake -S "$repo_root" -B "$build_dir" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DPICLENS_SYSTEM_PACKAGE=OFF \
    -DPICLENS_USE_SYSTEM_QT=OFF \
    -DPICLENS_REQUIRE_BUNDLED_LICENSES=ON \
    -DPICLENS_QT_SOURCE_ROOT="$qt_source_root"
cmake --build "$build_dir"
if [[ "$no_test" == false ]]; then
    ctest --test-dir "$build_dir" --output-on-failure
fi

rm -rf -- "$output_dir"
cmake --install "$build_dir" --prefix "$output_dir"

required_qml_modules=(
    QtQuick/Controls
    QtQuick/Templates
    QtQuick/Dialogs
    QtQuick/Layouts
    QtQuick/Shapes
    QtQuick/Window
)
missing_qml_module="false"
for qml_module in "${required_qml_modules[@]}"; do
    if [[ ! -d "$output_dir/qml/$qml_module" ]]; then
        missing_qml_module="true"
    fi
done
if [[ "$missing_qml_module" == "true" && -n "${QT_ROOT_DIR:-}" ]]; then
    qt_qml_root="$QT_ROOT_DIR/qml"
    for qml_module in "${required_qml_modules[@]}"; do
        source_module="$qt_qml_root/$qml_module"
        target_module="$output_dir/qml/$qml_module"
        if [[ -d "$source_module" ]]; then
            mkdir -p -- "$(dirname -- "$target_module")"
            cp -a -- "$source_module" "$target_module"
        fi
    done
    mkdir -p -- "$output_dir/lib"
    shopt -s nullglob
    for qt_library in "$QT_ROOT_DIR"/lib/libQt6Quick*.so*; do
        cp -a -- "$qt_library" "$output_dir/lib/"
    done
    shopt -u nullglob
fi
controls_plugin="$output_dir/qml/QtQuick/Controls/libqtquickcontrols2plugin.so"
if [[ ! -f "$controls_plugin" ]]; then
    echo "Required Qt Quick Controls plugin was not deployed: $controls_plugin" >&2
    exit 5
fi
if [[ ! -f "$output_dir/lib/libQt6QuickControls2Impl.so.6" ]]; then
    echo "Required Qt Quick Controls implementation library was not deployed." >&2
    exit 5
fi
for qml_module in "${required_qml_modules[@]}"; do
    if [[ ! -d "$output_dir/qml/$qml_module" ]]; then
        echo "Required Qt QML module was not deployed: $output_dir/qml/$qml_module" >&2
        exit 5
    fi
done

for qt_module in qtbase qtdeclarative qtimageformats; do
    license_dir="$output_dir/share/licenses/Qt/$qt_module"
    if [[ ! -d "$license_dir" ]] || ! find "$license_dir" -type f -print -quit | grep -q .; then
        echo "Qt $qt_module license texts were not installed from $qt_source_root" >&2
        exit 4
    fi
done

webp_plugin="$output_dir/plugins/imageformats/libqwebp.so"
if [[ ! -f "$webp_plugin" ]]; then
    echo "Required WebP image plugin was not deployed: $webp_plugin" >&2
    exit 5
fi

executable="$output_dir/bin/PicLens"
if [[ ! -x "$executable" ]]; then
    echo "Installed PicLens executable was not found: $executable" >&2
    exit 6
fi

if [[ "$skip_smoke" == false ]]; then
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
fi

file_count="$(find "$output_dir" -type f -printf '.' | wc -c)"
total_bytes="$(find "$output_dir" -type f -printf '%s\n' | awk '{ total += $1 } END { print total + 0 }')"
sha256="$(sha256sum "$executable" | awk '{ print toupper($1) }')"
echo "Portable output: $output_dir"
echo "Files: $file_count"
echo "Bytes: $total_bytes"
echo "PicLens SHA256: $sha256"
