param(
    [string]$Version = "",
    [string]$Runtime = "win-x64"
)

# FlashStickNote 标准发布流程脚本
# 用法: .\publish.ps1 -Version 1.0.1 [-Runtime win-x64]
# 产物: release\FlashStickNote-v{版本}-{RID}.zip (框架依赖, Release 构建)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "FlashStickNote.csproj"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectXml = Get-Content -Raw -LiteralPath $proj
    $versionNode = Select-Xml -Xml $projectXml -XPath "/Project/PropertyGroup/Version" | Select-Object -First 1
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.Node.InnerText)) {
        throw "无法从 FlashStickNote.csproj 读取 <Version>，请补充版本号或使用 -Version 指定。"
    }

    $Version = $versionNode.Node.InnerText.Trim()
}

$publishDir = Join-Path $root "bin\Release\net9.0-windows\$Runtime\publish"
$releaseDir = Join-Path $root "release"
$stageDir = Join-Path $releaseDir "FlashStickNote-v$Version-$Runtime"
$zipPath = Join-Path $releaseDir "FlashStickNote-v$Version-$Runtime.zip"

Write-Host "==> 1/4 发布构建 (Release, $Runtime, 框架依赖, v$Version) ..." -ForegroundColor Cyan
dotnet publish $proj -c Release -r $Runtime --self-contained false `
    -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }

Write-Host "==> 2/4 组装发布目录 ..." -ForegroundColor Cyan
if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item -Recurse -Force (Join-Path $publishDir "*") $stageDir
Copy-Item -Force (Join-Path $root "release-templates\conf.json") $stageDir
Copy-Item -Force (Join-Path $root "release-templates\shortcut.json") $stageDir
Copy-Item -Recurse -Force (Join-Path $root "theme") $stageDir

Write-Host "==> 3/4 生成 README ..." -ForegroundColor Cyan
$readme = (Get-Content -Raw (Join-Path $root "release-templates\README.txt")) -replace "%VERSION%", $Version
Set-Content -Path (Join-Path $stageDir "README.txt") -Value $readme -Encoding UTF8

Write-Host "==> 4/4 打包 zip ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
$fileCount = (Get-ChildItem $stageDir -Recurse -File | Measure-Object).Count
Write-Host ""
Write-Host "发布完成: $zipPath ($sizeMb MB, $fileCount 个文件)" -ForegroundColor Green
Write-Host "后续步骤: git commit -m 'release v$Version'; git tag v$Version" -ForegroundColor Yellow
