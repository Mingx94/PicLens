#!/usr/bin/env bash
set -euo pipefail

version="1.0.0"
skip_tests=0
no_release=0
runtime_identifier="linux-x64"
platform="x64"
runtime_requires="dotnet-runtime-10.0"

usage() {
    cat <<'EOF'
Usage: bash ./build/fedora-x64.sh [options]

Options:
  --version VERSION             RPM version. Default: 1.0.0
  --runtime linux-x64           Runtime identifier. Default: linux-x64
  --platform x64                MSBuild platform. Default: x64
  --runtime-requires PACKAGE    RPM runtime dependency. Default: dotnet-runtime-10.0
  --skip-tests                  Skip tests in the portable release build
  --no-release                  Reuse artifacts/portable/PicLens-linux-x64
  -h, --help                    Show help
EOF
}

while (($# > 0)); do
    case "$1" in
        --version)
            version="${2:-}"
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
        --runtime-requires)
            runtime_requires="${2:-}"
            shift 2
            ;;
        --skip-tests)
            skip_tests=1
            shift
            ;;
        --no-release)
            no_release=1
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

if [[ ! "$version" =~ ^[0-9A-Za-z._+~]+$ ]]; then
    echo "RPM version contains unsupported characters: $version" >&2
    exit 2
fi

if [[ "$runtime_identifier" != "linux-x64" || "$platform" != "x64" ]]; then
    echo "fedora-x64.sh supports only linux-x64 with --platform x64." >&2
    exit 2
fi

if ! command -v rpmbuild >/dev/null 2>&1; then
    echo "rpmbuild not found. Install it with: sudo dnf install rpm-build" >&2
    exit 1
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
root="$(cd -- "$script_dir/.." && pwd -P)"
release_script="$root/scripts/Release.sh"
payload_dir="$root/artifacts/portable/PicLens-$runtime_identifier"
rpm_top="$root/artifacts/rpm"
rpm_output_dir="$root/artifacts/installer"
spec_file="$rpm_top/SPECS/piclens.spec"

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

require_file "$release_script"
require_file "$root/PicLens/Assets/Square150x150Logo.scale-200.png"
require_file "$root/PicLens/Assets/Square44x44Logo.targetsize-48_altform-lightunplated.png"
assert_under_root "$payload_dir"
assert_under_root "$rpm_top"
assert_under_root "$rpm_output_dir"

if [[ "$no_release" -eq 0 ]]; then
    release_args=(
        --configuration Release
        --runtime "$runtime_identifier"
        --platform "$platform"
    )
    if [[ "$skip_tests" -eq 1 ]]; then
        release_args+=(--skip-tests)
    fi

    echo "==> Building portable release"
    bash "$release_script" "${release_args[@]}"
fi

require_file "$payload_dir/PicLens"

rm -rf -- "$rpm_top"
mkdir -p "$rpm_top/BUILD" "$rpm_top/BUILDROOT" "$rpm_top/RPMS" "$rpm_top/SOURCES" "$rpm_top/SPECS" "$rpm_top/SRPMS" "$rpm_output_dir"

cat > "$spec_file" <<'SPEC'
%global debug_package %{nil}

Name: piclens
Version: %{piclens_version}
Release: 1%{?dist}
Summary: PicLens image organizer and viewer
License: LicenseRef-PicLens
Requires: %{piclens_runtime_requires}
Requires: hicolor-icon-theme

%description
PicLens is a Windows and Linux Avalonia image organizer and viewer.

%prep

%build

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}%{_libdir}/piclens
cp -a "%{piclens_payload_dir}/." %{buildroot}%{_libdir}/piclens/
chmod 0755 %{buildroot}%{_libdir}/piclens/PicLens

mkdir -p %{buildroot}%{_bindir}
ln -s %{_libdir}/piclens/PicLens %{buildroot}%{_bindir}/piclens

install -Dm0644 "%{piclens_root_dir}/PicLens/Assets/Square44x44Logo.targetsize-48_altform-lightunplated.png" \
    %{buildroot}%{_datadir}/icons/hicolor/48x48/apps/piclens.png
install -Dm0644 "%{piclens_root_dir}/PicLens/Assets/Square150x150Logo.scale-200.png" \
    %{buildroot}%{_datadir}/icons/hicolor/300x300/apps/piclens.png

mkdir -p %{buildroot}%{_datadir}/applications
cat > %{buildroot}%{_datadir}/applications/piclens.desktop <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=PicLens
Comment=Image organizer and viewer
Exec=piclens
Icon=piclens
Terminal=false
Categories=Graphics;Photography;
StartupNotify=true
DESKTOP

%files
%{_bindir}/piclens
%{_libdir}/piclens/
%{_datadir}/applications/piclens.desktop
%{_datadir}/icons/hicolor/48x48/apps/piclens.png
%{_datadir}/icons/hicolor/300x300/apps/piclens.png
SPEC

echo "==> Building Fedora RPM"
rpmbuild -bb "$spec_file" \
    --target x86_64 \
    --define "_topdir $rpm_top" \
    --define "piclens_version $version" \
    --define "piclens_payload_dir $payload_dir" \
    --define "piclens_root_dir $root" \
    --define "piclens_runtime_requires $runtime_requires"

rpm_file="$(find "$rpm_top/RPMS/x86_64" -type f -name "piclens-$version-*.x86_64.rpm" -print | sort | tail -n 1)"
if [[ -z "$rpm_file" ]]; then
    echo "RPM build completed but package was not found." >&2
    exit 1
fi

output_file="$rpm_output_dir/PicLens-$version-fedora-x86_64.rpm"
cp -f -- "$rpm_file" "$output_file"
sha256="$(sha256sum "$output_file" | awk '{ print toupper($1) }')"

echo
echo "Fedora RPM ready:"
echo "  File:   $output_file"
echo "  Install: sudo dnf install $output_file"
echo "  SHA256: $sha256"
