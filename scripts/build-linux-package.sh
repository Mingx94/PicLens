#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: build-linux-package.sh (--deb | --rpm) [options]

Options:
  --build-dir PATH   Override the CMake build directory.
  --output-dir PATH  Override the installer artifact directory.
  --no-build         Package an already configured and built tree.
  --no-test          Skip CTest after building.
  --dry-run          Print the commands without executing them.
  -h, --help         Show this help.
EOF
}

package_kind=""
build_dir=""
output_dir=""
no_build=false
no_test=false
dry_run=false

while (($#)); do
    case "$1" in
        --deb|--rpm)
            if [[ -n "$package_kind" ]]; then
                echo "Choose exactly one package format." >&2
                exit 2
            fi
            package_kind="${1#--}"
            shift
            ;;
        --build-dir)
            build_dir="${2:?A build directory is required}"
            shift 2
            ;;
        --output-dir)
            output_dir="${2:?An output directory is required}"
            shift 2
            ;;
        --no-build)
            no_build=true
            shift
            ;;
        --no-test)
            no_test=true
            shift
            ;;
        --dry-run)
            dry_run=true
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

if [[ -z "$package_kind" ]]; then
    echo "Choose --deb or --rpm." >&2
    usage >&2
    exit 2
fi
if [[ "$(uname -s)" != "Linux" ]]; then
    echo "Linux installer packages must be built on Linux." >&2
    exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
version="$(tr -d '[:space:]' < "$repo_root/VERSION")"
if [[ ! "$version" =~ ^[0-9]{2}\.[0-9]{4}\.(0[1-9]|[1-9][0-9])$ ]]; then
    echo "VERSION must use the UTC release format YY.MMDD.NN: $version" >&2
    exit 3
fi
qt_source_root="${PICLENS_QT_SOURCE_ROOT:-}"
if [[ -z "$qt_source_root" && -n "${QT_ROOT_DIR:-}" ]]; then
    qt_source_root="$(dirname -- "$QT_ROOT_DIR")/Src"
fi

if [[ -z "$build_dir" ]]; then
    if [[ "$package_kind" == "deb" ]]; then
        build_dir="$repo_root/build/linux-deb-release"
    else
        build_dir="$repo_root/build/fedora-rpm-release"
    fi
fi
if [[ -z "$output_dir" ]]; then
    output_dir="$repo_root/artifacts/installer"
fi
mkdir -p -- "$output_dir"

run() {
    if [[ "$dry_run" == true ]]; then
        printf '  '
        printf '%q ' "$@"
        printf '\n'
    else
        "$@"
    fi
}

require_command() {
    if ! command -v "$1" >/dev/null; then
        echo "Required command was not found: $1" >&2
        exit 4
    fi
}

require_command cpack
require_command sha256sum
if [[ "$no_build" == false ]]; then
    require_command cmake
    require_command ninja
    if [[ "$no_test" == false ]]; then
        require_command ctest
    fi
fi
if [[ "$package_kind" == "deb" ]]; then
    require_command dpkg-deb
    package_extension="deb"
    use_system_qt="OFF"
    require_bundled_licenses="ON"
else
    require_command rpm
    require_command rpmbuild
    package_extension="rpm"
    use_system_qt="ON"
    require_bundled_licenses="OFF"
fi

echo "PicLens $package_kind installer version: $version"
echo "Build directory: $build_dir"
echo "Artifact directory: $output_dir"

if [[ "$no_build" == false ]]; then
    run cmake -S "$repo_root" -B "$build_dir" -G Ninja \
        -DCMAKE_BUILD_TYPE=Release \
        -DPICLENS_SYSTEM_PACKAGE=ON \
        -DPICLENS_USE_SYSTEM_QT="$use_system_qt" \
        -DPICLENS_REQUIRE_BUNDLED_LICENSES="$require_bundled_licenses" \
        -DPICLENS_QT_SOURCE_ROOT="$qt_source_root"
    run cmake --build "$build_dir"
    if [[ "$no_test" == false ]]; then
        run ctest --test-dir "$build_dir" --output-on-failure
    fi
elif [[ ! -f "$build_dir/CPackConfig.cmake" ]]; then
    echo "--no-build requires an existing CPackConfig.cmake in $build_dir" >&2
    exit 5
fi

package_output_dir="$build_dir/package-output/$package_kind"
if [[ "$dry_run" == false ]]; then
    rm -rf -- "$package_output_dir"
    mkdir -p -- "$package_output_dir"
fi
run cpack -G "${package_kind^^}" --config "$build_dir/CPackConfig.cmake" -B "$package_output_dir"
if [[ "$dry_run" == true ]]; then
    exit 0
fi

shopt -s nullglob
package_candidates=("$package_output_dir"/*."$package_extension")
shopt -u nullglob
if (( ${#package_candidates[@]} != 1 )); then
    echo "Expected exactly one .$package_extension package in $package_output_dir, found ${#package_candidates[@]}" >&2
    exit 6
fi
package_path="${package_candidates[0]}"

if [[ "$package_kind" == "deb" ]]; then
    packaged_version="$(dpkg-deb -f "$package_path" Version)"
    packaged_architecture="$(dpkg-deb -f "$package_path" Architecture)"
    dpkg-deb --fsys-tarfile "$package_path" \
        | tar -tf - \
        | grep -Fx './opt/piclens/bin/PicLens' >/dev/null
else
    packaged_version="$(rpm --query --package --queryformat '%{VERSION}' "$package_path")"
    packaged_architecture="$(rpm --query --package --queryformat '%{ARCH}' "$package_path")"
    rpm --query --package --list "$package_path" \
        | grep -Fx '/opt/piclens/bin/PicLens' >/dev/null
fi
if [[ "$packaged_version" != "$version" ]]; then
    echo "Package version $packaged_version does not match VERSION $version" >&2
    exit 7
fi

artifact_name="$(basename -- "$package_path")"
artifact_path="$output_dir/$artifact_name"
if [[ "$(realpath -m -- "$package_path")" != "$(realpath -m -- "$artifact_path")" ]]; then
    cp -f -- "$package_path" "$artifact_path"
fi
artifact_bytes="$(stat -c %s "$artifact_path")"
artifact_sha256="$(sha256sum "$artifact_path" | awk '{print toupper($1)}')"
manifest_path="$output_dir/piclens-$package_kind-manifest.json"
manifest_temp="$manifest_path.tmp"
printf '{\n  "schemaVersion": 1,\n  "kind": "%s",\n  "version": "%s",\n  "architecture": "%s",\n  "file": "%s",\n  "bytes": %s,\n  "sha256": "%s"\n}\n' \
    "$package_kind" \
    "$packaged_version" \
    "$packaged_architecture" \
    "$artifact_name" \
    "$artifact_bytes" \
    "$artifact_sha256" > "$manifest_temp"
mv -f -- "$manifest_temp" "$manifest_path"

echo "Installer ready: $artifact_path"
echo "Manifest: $manifest_path"
echo "Bytes: $artifact_bytes"
echo "SHA256: $artifact_sha256"
