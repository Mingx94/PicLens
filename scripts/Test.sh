#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
root="$(cd -- "$script_dir/.." && pwd -P)"
nuget_config="$root/NuGet.Config"
test_projects=(
    "$root/tests/PicLens.Core.Tests/PicLens.Core.Tests.csproj"
    "$root/tests/PicLens.Infrastructure.Tests/PicLens.Infrastructure.Tests.csproj"
    "$root/tests/PicLens.ViewModels.Tests/PicLens.ViewModels.Tests.csproj"
)

if [[ ! -f "$nuget_config" ]]; then
    echo "NuGet.Config not found: $nuget_config" >&2
    exit 1
fi

for test_project in "${test_projects[@]}"; do
    if [[ ! -f "$test_project" ]]; then
        echo "Test project file not found: $test_project" >&2
        exit 1
    fi
done

for test_project in "${test_projects[@]}"; do
    echo "==> Restoring test project: $test_project"
    dotnet restore "$test_project" --configfile "$nuget_config"
done

for test_project in "${test_projects[@]}"; do
    echo "==> Running tests: $test_project"
    dotnet test "$test_project" --no-restore
done
