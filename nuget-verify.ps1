param(
    [ValidateSet('Simple', 'Full')]
    [string]$Mode = 'Full',
    # 默认取 Pty.Net.csproj 里的 <Version>；也可手动传入（例如 1.0.2-local）
    [string]$Version
)

$ErrorActionPreference = 'Stop';

$root = (Resolve-Path -LiteralPath $PSScriptRoot).Path;
$projDir = Join-Path $root 'NupkgVerify';
$releaseDir = Join-Path $root 'Pty.Net\bin\Release';
$csproj = Join-Path $root 'Pty.Net\Pty.Net.csproj';

function Get-Version {
    [xml]$xml = Get-Content $csproj;
    $pg = @($xml.Project.PropertyGroup) | Where-Object { $_ -and $_.Version } | Select-Object -First 1;
    if (-not $pg -or -not $pg.Version) {
        throw "<Version> not found in: $csproj";
    }
    return [string]$pg.Version;
}

function New-PtyNupkgVerifyProject {
    if (-not (Test-Path $projDir)) {
        dotnet new console -n NupkgVerify -o $projDir -f net8.0;
    }
}

function Get-LatestNupkgPath([string]$ver = $null) {
    if (-not (Test-Path $releaseDir)) { throw "Release dir not found: $releaseDir" }

    $pattern = if ($ver) { "PTY.$ver*.nupkg" } else { 'PTY.*.nupkg' };
    $pkg = Get-ChildItem $releaseDir -Filter $pattern |
        Where-Object { $_.Name -notmatch '\.snupkg$' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1;

    if (-not $pkg) {
        throw "nupkg not found in $releaseDir (pattern: $pattern)";
    }
    return $pkg.FullName;
}

function Test-ZipHasEntry([System.IO.Compression.ZipArchive]$zip, [string]$fullName) {
    return [bool]($zip.Entries | Where-Object { $_.FullName -eq $fullName } | Select-Object -First 1)
}

function Test-ZipHasAnyEntryLike([System.IO.Compression.ZipArchive]$zip, [string]$regex) {
    return [bool]($zip.Entries | Where-Object { $_.FullName -match $regex } | Select-Object -First 1)
}

function Test-PtyNupkgLayout([string]$pkgPath) {
    if (-not (Test-Path $pkgPath)) { throw "nupkg not found: $pkgPath" }

    Add-Type -AssemblyName System.IO.Compression.FileSystem;
    $zip = [System.IO.Compression.ZipFile]::OpenRead($pkgPath);
    try {
        "--- verifying nupkg ---";
        "pkg: $pkgPath";

        # 1) 必须包含 build / buildTransitive targets
        if (-not (Test-ZipHasEntry $zip 'build/PTY.targets')) { throw 'MISSING in nupkg: build/PTY.targets' }
        if (-not (Test-ZipHasEntry $zip 'buildTransitive/PTY.targets')) { throw 'MISSING in nupkg: buildTransitive/PTY.targets' }

        # 2) 必须包含 content/deps（targets 会从 content/deps 复制到输出 deps）
        if (-not (Test-ZipHasAnyEntryLike $zip '^content/deps/')) { throw 'MISSING in nupkg: content/deps/**' }

        # 3) Windows 关键文件（按仓库 deps 结构）
        if (-not (Test-ZipHasEntry $zip 'content/deps/conpty/x64/conpty.dll')) { throw 'MISSING in nupkg: content/deps/conpty/x64/conpty.dll' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/conpty/x64/OpenConsole.exe')) { throw 'MISSING in nupkg: content/deps/conpty/x64/OpenConsole.exe' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/conpty/x86/conpty.dll')) { throw 'MISSING in nupkg: content/deps/conpty/x86/conpty.dll' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/conpty/x86/OpenConsole.exe')) { throw 'MISSING in nupkg: content/deps/conpty/x86/OpenConsole.exe' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/conpty/arm64/conpty.dll')) { throw 'MISSING in nupkg: content/deps/conpty/arm64/conpty.dll' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/conpty/arm64/OpenConsole.exe')) { throw 'MISSING in nupkg: content/deps/conpty/arm64/OpenConsole.exe' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/winpty/winpty.dll')) { throw 'MISSING in nupkg: content/deps/winpty/winpty.dll' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/winpty/winpty.exe')) { throw 'MISSING in nupkg: content/deps/winpty/winpty.exe' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/winpty/winpty-agent.exe')) { throw 'MISSING in nupkg: content/deps/winpty/winpty-agent.exe' }
        if (-not (Test-ZipHasEntry $zip 'content/deps/winpty/winpty-debugserver.exe')) { throw 'MISSING in nupkg: content/deps/winpty/winpty-debugserver.exe' }

        # 4) 不应该把 native 的 pdb/lib/exp 打进 nupkg（csproj 已 Exclude，这里再验一遍）
        $bad = $zip.Entries | Where-Object { $_.FullName -match '^content/deps/.+\.(pdb|lib|exp)$' } | Select-Object -ExpandProperty FullName;
        if ($bad) {
            "--- unexpected native artifacts in nupkg ---";
            $bad;
            throw "nupkg contains excluded native artifacts under content/deps (pdb/lib/exp)";
        }

        "OK: nupkg layout verified";
    }
    finally {
        $zip.Dispose();
    }
}

function Test-PtyNupkgSimple {
    $ver = if ($Version) { $Version } else { Get-Version };
    if (Test-Path $projDir) { Remove-Item $projDir -Recurse -Force };
    New-PtyNupkgVerifyProject;
    Push-Location $projDir;
    dotnet add package PTY -v $ver -s $releaseDir;
    dotnet build;
    Pop-Location;
    $out = Join-Path $projDir "bin\Debug\net8.0\deps\conpty\x64\conpty.dll";
    if (Test-Path $out) { "OK: copied -> $out" } else { "MISSING: $out" }
}

function Test-PtyNupkgFull {
    $ver = if ($Version) { $Version } else { Get-Version };

    # 1) 重新打包（输出在 Pty.Net\bin\Release）
    dotnet pack $csproj -c Release;

    # 2) 找到刚生成的 nupkg，并做结构校验（build/buildTransitive + content/deps）
    $pkg = Get-LatestNupkgPath $ver;
    Test-PtyNupkgLayout $pkg;

    # 3) 清理 NupkgVerify 对 PTY 的缓存安装（只删这个版本，别全局清）
    $ptyCacheRoot = Join-Path $env:USERPROFILE '.nuget\packages\pty';
    $ptyCacheVer = Join-Path $ptyCacheRoot $ver;
    if (Test-Path $ptyCacheVer) { Remove-Item $ptyCacheVer -Recurse -Force };

    # 4) 让 NupkgVerify 从本地包源安装这个版本并重新编译
    New-PtyNupkgVerifyProject;
    Push-Location $projDir;
    dotnet add package PTY -v $ver -s $releaseDir;
    dotnet restore;
    dotnet build;
    Pop-Location;

    # 5) 验证输出目录是否出现 deps，并检查关键文件
    $p = Join-Path $projDir "bin\Debug\net8.0\deps";
    if (Test-Path $p) {
        "deps exists";
        $expected = @(
            (Join-Path $p 'conpty\x64\conpty.dll'),
            (Join-Path $p 'conpty\x64\OpenConsole.exe'),
            (Join-Path $p 'conpty\x86\conpty.dll'),
            (Join-Path $p 'conpty\x86\OpenConsole.exe'),
            (Join-Path $p 'conpty\arm64\conpty.dll'),
            (Join-Path $p 'conpty\arm64\OpenConsole.exe'),
            (Join-Path $p 'winpty\winpty.dll'),
            (Join-Path $p 'winpty\winpty.exe'),
            (Join-Path $p 'winpty\winpty-agent.exe'),
            (Join-Path $p 'winpty\winpty-debugserver.exe')
        );
        $missing = $expected | Where-Object { -not (Test-Path $_) };
        if ($missing) {
            "--- missing deps outputs ---";
            $missing;
            throw 'deps copied but some expected files are missing';
        }
        "OK: deps files exist in output";
    }
    else {
        "deps missing";
    }
}

if ($Mode -eq 'Simple') {
    Test-PtyNupkgSimple;
}
else {
    Test-PtyNupkgFull;
}

# 兼容：保留旧命名习惯（如你有 dot-source 后手动调用）
# Set-Alias -Name Verify-Nupkg -Value Test-PtyNupkgLayout -Scope Script -Force;
# Set-Alias -Name Ensure-VerifyProject -Value New-PtyNupkgVerifyProject -Scope Script -Force;
# Set-Alias -Name Nupkg-Verify-Simple -Value Test-PtyNupkgSimple -Scope Script -Force;
# Set-Alias -Name Nupkg-Verify-Full -Value Test-PtyNupkgFull -Scope Script -Force;