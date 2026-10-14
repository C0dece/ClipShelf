# Автоматический генератор мульти-размерной иконки Windows 11 Fluent и PNG для ClipShelf

$srcPath = if ($args[0]) { $args[0] } else { "$PSScriptRoot\concept_clipboard_master.jpg" }
$dstPng  = if ($args[1]) { $args[1] } else { "$PSScriptRoot\app_icon.png" }
$dstIco  = if ($args[2]) { $args[2] } else { "$PSScriptRoot\app.ico" }

if (-not (Test-Path $srcPath)) {
    Write-Error "Source image not found: $srcPath"
    exit 1
}

$processorCs = "$PSScriptRoot\IconProcessor.cs"
$processorExe = "$PSScriptRoot\IconProcessor.exe"

# Компилируем высокопроизводительный процессор иконки C#, если экзешник отсутствует или устарел
$needCompile = $false
if (-not (Test-Path $processorExe)) {
    $needCompile = $true
} elseif ((Get-Item $processorCs).LastWriteTime -gt (Get-Item $processorExe).LastWriteTime) {
    $needCompile = $true
}

if ($needCompile) {
    $csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $csc)) {
        $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    }
    Write-Host "Compiling IconProcessor..."
    & $csc /nologo /optimize+ /out:$processorExe $processorCs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "IconProcessor compilation failed."
        exit 1
    }
}

# Запускаем обработку
Write-Host "Processing isolated clipboard icon..."
& $processorExe $srcPath $dstPng $dstIco

if ($LASTEXITCODE -ne 0) {
    Write-Error "Icon generation failed."
    exit 1
}

# Синхронизируем в папку dist
$distDir = Join-Path (Split-Path $PSScriptRoot -Parent) "dist"
if (Test-Path $distDir) {
    Copy-Item $dstIco -Destination (Join-Path $distDir "app.ico") -Force
    Copy-Item $dstPng -Destination (Join-Path $distDir "app_icon.png") -Force
    Write-Host "Synchronized app.ico and app_icon.png to dist/"
}

Write-Host "Successfully generated transparent full-height icon without background!"
