@echo off
REM ========================================
REM PDF Reader API - Basit Güncelleme Script
REM ========================================
REM Mevcut IIS deployment'ınızı günceller
REM Port, Application Pool veya Site oluşturmaz
REM Sadece build + publish + kopyalama yapar
REM ========================================

echo ========================================
echo PDF Reader API - Guncelleme
echo ========================================
echo.
echo Bu script mevcut deployment'inizi gunceller:
echo - Projeyi build eder (Release)
echo - Publish klasorune kopyalar
echo - Mevcut IIS klasorune kopyalar
echo - Application Pool'u restart eder
echo.
echo NOTLAR:
echo - Yeni port ACMAZ (443 veya mevcut port kullanilir)
echo - Yeni Site OLUSTURMAZ
echo - Sadece dosyalari gunceller
echo.

REM Yönetici kontrolü
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo HATA: Bu script yonetici olarak calistirilmalidir!
    echo Sag tiklayin ve "Run as Administrator" seçin.
    pause
    exit /b 1
)

echo [1/5] .NET kontrolu...
dotnet --version >nul 2>&1
if %errorLevel% neq 0 (
    echo HATA: .NET 9.0 Runtime bulunamadi!
    echo Lutfen .NET 9.0 Hosting Bundle'i indirin.
    pause
    exit /b 1
)
echo   - .NET Runtime: OK
echo.

echo [2/5] Hedef klasor kontrol ediliyor...
echo.
echo Mevcut IIS klasorunuzun yolunu girin:
echo Ornek: C:\inetpub\wwwroot\PdfReaderAPI
echo Veya Enter'a basip varsayilan kullanin: C:\inetpub\PdfReaderAPI
echo.
set /p TARGET_PATH="IIS Klasor Yolu (Enter=varsayilan): "

if "%TARGET_PATH%"=="" (
    set TARGET_PATH=C:\inetpub\PdfReaderAPI
)

echo.
echo Hedef klasor: %TARGET_PATH%
echo.

if not exist "%TARGET_PATH%" (
    echo UYARI: Hedef klasor bulunamadi: %TARGET_PATH%
    echo Klasor olusturulsun mu? (Y/N)
    set /p CREATE_FOLDER="Olustur? (Y/N): "

    if /i "%CREATE_FOLDER%"=="Y" (
        mkdir "%TARGET_PATH%"
        echo   - Klasor olusturuldu
    ) else (
        echo Script iptal edildi.
        pause
        exit /b 1
    )
) else (
    echo   - Hedef klasor mevcut: OK
)
echo.

echo [3/5] Proje build ediliyor (Release mode)...
echo.

REM Mevcut dizini al
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

REM Publish klasörünü temizle
if exist ".\publish\" (
    echo   - Eski publish klasoru siliniyor...
    rmdir /s /q ".\publish"
)

echo   - Build basliyor...
dotnet publish -c Release -o ".\publish" --self-contained false

if %errorLevel% neq 0 (
    echo HATA: Build basarisiz!
    pause
    exit /b 1
)

echo   - Build basarili
echo.

echo [4/5] Application Pool bilgilerini girin...
echo.
echo Mevcut Application Pool adinizi girin:
echo Ornek: DefaultAppPool, PdfReaderAPI, vb.
echo Veya Enter'a basip varsayilan kullanin: PdfReaderAPI
echo.
set /p APP_POOL_NAME="Application Pool Adi (Enter=varsayilan): "

if "%APP_POOL_NAME%"=="" (
    set APP_POOL_NAME=PdfReaderAPI
)

echo.
echo Application Pool: %APP_POOL_NAME%

REM Application Pool var mı kontrol et
powershell -Command "Import-Module WebAdministration; Test-Path 'IIS:\AppPools\%APP_POOL_NAME%'" >nul 2>&1
if %errorLevel% equ 0 (
    echo   - Application Pool mevcut: %APP_POOL_NAME%
    echo   - Durdurulacak...
    powershell -Command "Stop-WebAppPool -Name '%APP_POOL_NAME%'" >nul 2>&1
    timeout /t 2 /nobreak >nul
    echo   - Durduruldu
) else (
    echo UYARI: Application Pool bulunamadi: %APP_POOL_NAME%
    echo Devam edilsin mi? (Y/N)
    set /p CONTINUE="Devam? (Y/N): "
    if /i not "%CONTINUE%"=="Y" (
        echo Script iptal edildi.
        pause
        exit /b 1
    )
)
echo.

echo [5/5] Dosyalar kopyalaniyor...
echo.

echo   - Hedef: %TARGET_PATH%
xcopy /E /I /Y /Q ".\publish\*" "%TARGET_PATH%\" >nul 2>&1

if %errorLevel% neq 0 (
    echo HATA: Dosya kopyalama basarisiz!
    echo Lutfen izinleri kontrol edin.
    pause
    exit /b 1
)

echo   - Dosyalar kopyalandi
echo.

REM Logs klasörü oluştur (yoksa)
if not exist "%TARGET_PATH%\logs" (
    mkdir "%TARGET_PATH%\logs"
    echo   - Logs klasoru olusturuldu
)

REM Application Pool'u başlat
powershell -Command "Import-Module WebAdministration; Test-Path 'IIS:\AppPools\%APP_POOL_NAME%'" >nul 2>&1
if %errorLevel% equ 0 (
    echo   - Application Pool baslatiliyor...
    powershell -Command "Start-WebAppPool -Name '%APP_POOL_NAME%'" >nul 2>&1
    timeout /t 3 /nobreak >nul
    echo   - Application Pool baslatildi
)
echo.

echo ========================================
echo GUNCELLEME TAMAMLANDI!
echo ========================================
echo.
echo Yapilan islemler:
echo 1. Proje build edildi (Release)
echo 2. Dosyalar kopyalandi: %TARGET_PATH%
echo 3. Application Pool restart edildi: %APP_POOL_NAME%
echo.
echo SONRAKI ADIMLAR:
echo.
echo 1. IIS Manager'dan site durumunu kontrol edin:
echo    inetmgr
echo.
echo 2. Site'inizi test edin (mevcut URL'inizi kullanin):
echo    https://your-domain.com/health
echo    https://your-domain.com/swagger
echo    https://your-domain.com/hangfire
echo.
echo 3. Logs kontrol edin:
echo    %TARGET_PATH%\logs\
echo.
echo NOTLAR:
echo - Mevcut port ve site ayarlari degismedi
echo - Sadece uygulama dosyalari guncellendi
echo - Redis kullanmiyorsaniz, production'da kurmayi dusunun
echo.
pause
