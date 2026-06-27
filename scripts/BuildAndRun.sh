#!/usr/bin/env bash
set -euo pipefail

project="PicLens/PicLens.csproj"
skip_run=0
detach=0
extra_args=()

while (($# > 0)); do
    case "$1" in
        --skip-run)
            skip_run=1
            shift
            ;;
        --detach)
            detach=1
            shift
            ;;
        -*|/*)
            extra_args+=("$1")
            shift
            ;;
        *)
            if [[ "$project" == "PicLens/PicLens.csproj" ]]; then
                project="$1"
            else
                extra_args+=("$1")
            fi
            shift
            ;;
    esac
done

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
root="$(cd -- "$script_dir/.." && pwd -P)"
project_path="$(realpath -m "$root/$project")"
project_dir="$(dirname "$project_path")"
project_name="$(basename "$project_path" .csproj)"
platform="x64"
configuration="Debug"

for arg in "${extra_args[@]}"; do
    case "$arg" in
        -p:Platform=*|/p:Platform=*)
            platform="${arg#*=}"
            ;;
        -p:Configuration=*|/p:Configuration=*)
            configuration="${arg#*=}"
            ;;
    esac
done

if [[ ! -f "$project_path" ]]; then
    echo "Project file not found: $project_path" >&2
    exit 2
fi

echo "==> Building $project_name ($configuration|$platform)"
dotnet build "$project_path" \
    /restore \
    "-p:Platform=$platform" \
    "-p:Configuration=$configuration" \
    "${extra_args[@]}"

echo "BUILD SUCCEEDED"

if [[ "$skip_run" -eq 1 ]]; then
    echo "==> Skipping run (--skip-run)"
    exit 0
fi

bin_dir="$project_dir/bin/$platform/$configuration"
exe_path="$(find "$bin_dir" -type f -name "$project_name" -print 2>/dev/null | sort | tail -n 1 || true)"
dll_path="$(find "$bin_dir" -type f -name "$project_name.dll" -print 2>/dev/null | sort | tail -n 1 || true)"

if [[ -n "$exe_path" ]]; then
    echo "==> Launching $exe_path"
    if [[ "$detach" -eq 1 ]]; then
        nohup "$exe_path" >/dev/null 2>&1 &
    else
        "$exe_path"
    fi
elif [[ -n "$dll_path" ]]; then
    echo "==> Launching $dll_path"
    if [[ "$detach" -eq 1 ]]; then
        nohup dotnet "$dll_path" >/dev/null 2>&1 &
    else
        dotnet "$dll_path"
    fi
else
    echo "Build completed but $project_name executable was not found under $bin_dir." >&2
    exit 1
fi
