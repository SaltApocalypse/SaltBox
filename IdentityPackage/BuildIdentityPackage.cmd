@echo off
REM ============================================================
REM Build and sign sparse identity package for SaltBox
REM ============================================================
REM Prerequisites:
REM   1. Windows SDK (for MakeAppx.exe and SignTool.exe)
REM   2. A code signing certificate (.pfx) or self-signed cert
REM ============================================================

set OUT_DIR=%~dp0output
if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"

REM --- Step 1: Pack the identity package ---
REM /nv bypasses validation of referenced external file paths
MakeAppx.exe pack /o /d "%~dp0" /nv /p "%OUT_DIR%\SaltBox.Identity.msix"
if %ERRORLEVEL% neq 0 (
    echo ERROR: MakeAppx.exe failed (errorlevel=%ERRORLEVEL%)
    exit /b %ERRORLEVEL%
)
echo [OK] Packed SaltBox.Identity.msix

REM --- Step 2: Sign the identity package (optional) ---
REM For production: use a CA-trusted certificate
REM For dev: use a self-signed cert imported to Trusted People store

if not "%1"=="" (
    if "%2"=="" (
        SignTool.exe sign /fd SHA256 /a /f %1 /p "" "%OUT_DIR%\SaltBox.Identity.msix"
    ) else (
        SignTool.exe sign /fd SHA256 /a /f %1 /p %2 "%OUT_DIR%\SaltBox.Identity.msix"
    )

    if %ERRORLEVEL% neq 0 (
        echo ERROR: SignTool.exe failed (errorlevel=%ERRORLEVEL%)
        exit /b %ERRORLEVEL%
    )
    echo [OK] Signed SaltBox.Identity.msix
) else (
    echo [INFO] No certificate provided. Skipping signing.
)

REM --- Step 3: Copy to destination if specified ---
if not "%3"=="" (
    copy /Y "%OUT_DIR%\SaltBox.Identity.msix" "%3%\SaltBox.Identity.msix"
    if %ERRORLEVEL% neq 0 (
        echo WARNING: Failed to copy to %3
    ) else (
        echo [OK] Copied to %3
    )
)

:done
echo.
echo Identity package built: %OUT_DIR%\SaltBox.Identity.msix
echo.
echo Examples:
echo   BuildIdentityPackage.cmd                              # pack only
echo   BuildIdentityPackage.cmd MyCert.pfx pass123            # pack + sign
echo   BuildIdentityPackage.cmd MyCert.pfx pass123 .\publish  # pack + sign + deploy
echo.
