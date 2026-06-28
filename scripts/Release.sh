#!/usr/bin/env bash
set -euo pipefail

configuration="Release"
runtime_identifier="linux-x64"
platform="x64"
skip_tests=0
no_clean=0

usage() {
    cat <<'EOF'
Usage: bash ./scripts/Release.sh [options]

Options:
  --configuration Debug|Release   Build configuration. Default: Release
  --runtime linux-x64             Runtime identifier. Default: linux-x64
  --platform x64                  MSBuild platform. Default: x64
  --skip-tests                    Skip unit tests before publish
  --no-clean                      Keep existing output directory contents
  -h, --help                      Show help
EOF
}

while (($# > 0)); do
    case "$1" in
        --configuration)
            configuration="${2:-}"
            shift 2
            ;;
        --runtime)
            runtime_identifier="${2:-}"
            shift 2
            ;;
        --platform)
            platform="${2:-}"
            shift 2
            ;;
        --skip-tests)
            skip_tests=1
            shift
            ;;
        --no-clean)
            no_clean=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

if [[ "$configuration" != "Debug" && "$configuration" != "Release" ]]; then
    echo "Configuration must be Debug or Release." >&2
    exit 2
fi

if [[ "$runtime_identifier" != "linux-x64" ]]; then
    echo "Release.sh supports only linux-x64." >&2
    exit 2
fi

if [[ "$platform" != "x64" ]]; then
    echo "RuntimeIdentifier linux-x64 requires --platform x64." >&2
    exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
root="$(cd -- "$script_dir/.." && pwd -P)"
project="$root/PicLens/PicLens.csproj"
nuget_config="$root/NuGet.Config"
test_script="$script_dir/Test.sh"
output_root="$root/artifacts/portable"
output_dir="$output_root/PicLens-$runtime_identifier"
exe_path="$output_dir/PicLens"
publish_ready_to_run="true"
if [[ "$configuration" == "Debug" ]]; then
    publish_ready_to_run="false"
fi

assert_under_root() {
    local path="$1"
    local resolved_root
    local resolved_path
    resolved_root="$(realpath -m "$root")"
    resolved_path="$(realpath -m "$path")"

    case "$resolved_path" in
        "$resolved_root"|"$resolved_root"/*) ;;
        *)
            echo "Refusing to operate outside workspace root: $resolved_path" >&2
            exit 1
            ;;
    esac
}

require_file() {
    if [[ ! -f "$1" ]]; then
        echo "Required file not found: $1" >&2
        exit 1
    fi
}

require_file "$project"
require_file "$nuget_config"
require_file "$test_script"
assert_under_root "$output_root"
assert_under_root "$output_dir"

mkdir -p "$output_root"

if [[ "$no_clean" -eq 0 && -e "$output_dir" ]]; then
    rm -rf -- "$output_dir"
fi

if [[ "$skip_tests" -eq 0 ]]; then
    echo "==> Running unit tests"
    bash "$test_script"
fi

echo "==> Restoring app for $runtime_identifier"
dotnet restore "$project" \
    --configfile "$nuget_config" \
    -r "$runtime_identifier" \
    "/p:Configuration=$configuration" \
    "/p:Platform=$platform" \
    "/p:PublishReadyToRun=$publish_ready_to_run" \
    "/p:SelfContained=false"

echo "==> Publishing framework-dependent output"
dotnet publish "$project" \
    --no-restore \
    -c "$configuration" \
    -r "$runtime_identifier" \
    --self-contained false \
    "/p:Platform=$platform" \
    "/p:PublishSelfContained=false" \
    "/p:PublishSingleFile=false" \
    "/p:PublishReadyToRun=$publish_ready_to_run" \
    "/p:PublishTrimmed=false" \
    "/p:SelfContained=false" \
    "/p:DebugType=None" \
    "/p:DebugSymbols=false" \
    -o "$output_dir"

if [[ ! -f "$exe_path" ]]; then
    echo "Publish completed but PicLens was not found at: $exe_path" >&2
    exit 1
fi

file_count="$(find "$output_dir" -type f | wc -l | tr -d ' ')"
total_bytes="$(find "$output_dir" -type f -printf '%s\n' | awk '{ total += $1 } END { print total + 0 }')"
sha256="$(sha256sum "$exe_path" | awk '{ print toupper($1) }')"

echo
echo "Release output ready:"
echo "  Folder: $output_dir"
echo "  Exe:    $exe_path"
echo "  Files:  $file_count"
echo "  Bytes:  $total_bytes"
echo "  SHA256: $sha256"
