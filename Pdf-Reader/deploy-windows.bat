@echo off
REM ========================================
REM PDF Reader API - Windows Server Deployment Script
REM ========================================
REM Bu script Windows Server 2022'de IIS + Hangfire + Redis kurulumunu yapar
REM Yönetici (Administrator) olarak çalıştırılmalıdır
REM ========================================

echo ========================================
echo PDF Reader API - Windows Deployment
echo ========================================
echo.

REM Yönetici kontrolü
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo HATA: Bu script yonetici olarak calistirilmalidir!
    echo Sag tiklayin ve "Run as Administrator" seçin.
    pause
    exit /b 1
)

echo [1/6] Sistem kontrolu yapiliyor...
echo.

REM .NET 9.0 kontrolü
dotnet --version >nul 2>&1
if %errorLevel% neq 0 (
    echo HATA: .NET 9.0 Runtime bulunamadi!
    echo Lutfen .NET 9.0 Hosting Bundle'i indirin:
    echo https://dotnet.microsoft.com/download/dotnet/9.0
    pause
    exit /b 1
)

echo   - .NET Runtime: OK
echo.

echo [2/6] Redis for Windows (Memurai) kurulumu kontrol ediliyor...
echo.

REM Redis/Memurai kontrolü
sc query Memurai >nul 2>&1
if %errorLevel% neq 0 (
    echo WARNING: Memurai servisi bulunamadi.
    echo Redis gerekli mi? (Production'da gerekli, Development'ta opsiyonel)
    echo.
    set /p INSTALL_REDIS="Memurai (Redis for Windows) yuklensin mi? (Y/N): "

    if /i "%INSTALL_REDIS%"=="Y" (
        echo.
        echo Memurai indiriliyor...
        echo Lutfen browser'dan indirin: https://www.memurai.com/get-memurai
        echo Indirme tamamlandiktan sonra setup dosyasini calistirin.
        echo.
        pause

        REM Kurulum sonrası kontrol
        sc query Memurai >nul 2>&1
        if %errorLevel% neq 0 (
            echo WARNING: Memurai kurulumu dogrulanamadi.
            echo Devam etmek istiyor musunuz? (Redis olmadan calisacak)
            pause
        ) else (
            echo   - Memurai servisi: OK
            echo   - Memurai baslatiliyor...
            net start Memurai
        )
    ) else (
        echo   - Redis atlanıyor (In-memory storage kullanilacak)
    )
) else (
    echo   - Redis servisi: OK
    REM Redis'i başlat
    net start Memurai >nul 2>&1
    echo   - Redis baslatildi
)
echo.

echo [3/6] IIS kontrolu yapiliyor...
echo.

REM IIS kontrolü
sc query W3SVC >nul 2>&1
if %errorLevel% neq 0 (
    echo IIS bulunamadi. IIS yuklensin mi? (Y/N)
    set /p INSTALL_IIS="IIS yuklensin mi? (Y/N): "

    if /i "%INSTALL_IIS%"=="Y" (
        echo IIS kuruluyor (bu birkaç dakika sürebilir)...
        dism /online /enable-feature /featurename:IIS-WebServerRole /all
        dism /online /enable-feature /featurename:IIS-WebServer /all
        dism /online /enable-feature /featurename:IIS-CommonHttpFeatures /all
        dism /online /enable-feature /featurename:IIS-HttpErrors /all
        dism /online /enable-feature /featurename:IIS-ApplicationDevelopment /all
        dism /online /enable-feature /featurename:IIS-NetFxExtensibility45 /all
        dism /online /enable-feature /featurename:IIS-HealthAndDiagnostics /all
        dism /online /enable-feature /featurename:IIS-HttpLogging /all
        dism /online /enable-feature /featurename:IIS-Security /all
        dism /online /enable-feature /featurename:IIS-RequestFiltering /all
        dism /online /enable-feature /featurename:IIS-Performance /all
        dism /online /enable-feature /featurename:IIS-WebServerManagementTools /all
        dism /online /enable-feature /featurename:IIS-ManagementConsole /all
        dism /online /enable-feature /featurename:IIS-StaticContent /all
        dism /online /enable-feature /featurename:IIS-DefaultDocument /all

        echo   - IIS kurulumu tamamlandi
        net start W3SVC
    ) else (
        echo WARNING: IIS olmadan devam ediliyor
    )
) else (
    echo   - IIS servisi: OK
    net start W3SVC >nul 2>&1
)
echo.

echo [4/6] Proje publish ediliyor...
echo.

REM Mevcut dizini al
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

REM Publish klasörünü temizle
if exist ".\publish\" (
    echo   - Eski publish klasoru siliniyor...
    rmdir /s /q ".\publish"
)

echo   - Proje build ediliyor (Release mode)...
dotnet publish -c Release -o ".\publish" --self-contained false

if %errorLevel% neq 0 (
    echo HATA: Build basarisiz!
    pause
    exit /b 1
)

echo   - Publish basarili: .\publish\
echo.

echo [5/6] IIS Application Pool ve Site yapılandırılıyor...
echo.

REM IIS varsa Application Pool ve Site oluştur
sc query W3SVC >nul 2>&1
if %errorLevel% equ 0 (
    REM Application Pool kontrolü
    powershell -Command "Import-Module WebAdministration; Test-Path 'IIS:\AppPools\PdfReaderAPI'" >nul 2>&1
    if %errorLevel% equ 0 (
        echo   - Application Pool mevcut: PdfReaderAPI
        echo   - Durdurulup guncelleniyor...
        powershell -Command "Stop-WebAppPool -Name 'PdfReaderAPI'" >nul 2>&1
    ) else (
        echo   - Yeni Application Pool olusturuluyor: PdfReaderAPI
        powershell -Command "Import-Module WebAdministration; New-WebAppPool -Name 'PdfReaderAPI'" >nul 2>&1
    )

    REM Application Pool ayarlarını güncelle
    powershell -Command "Import-Module WebAdministration; Set-ItemProperty 'IIS:\AppPools\PdfReaderAPI' -Name 'managedRuntimeVersion' -Value ''; Set-ItemProperty 'IIS:\AppPools\PdfReaderAPI' -Name 'processModel.identityType' -Value 'ApplicationPoolIdentity'; Set-ItemProperty 'IIS:\AppPools\PdfReaderAPI' -Name 'startMode' -Value 'AlwaysRunning'; Set-ItemProperty 'IIS:\AppPools\PdfReaderAPI' -Name 'queueLength' -Value '5000'" >nul 2>&1

    echo   - Hedef klasor: C:\inetpub\PdfReaderAPI
    if not exist "C:\inetpub\PdfReaderAPI" mkdir "C:\inetpub\PdfReaderAPI"

    echo   - Dosyalar kopyalaniyor...
    xcopy /E /I /Y /Q ".\publish\*" "C:\inetpub\PdfReaderAPI\" >nul 2>&1

    echo   - Logs klasoru hazir
    if not exist "C:\inetpub\PdfReaderAPI\logs" mkdir "C:\inetpub\PdfReaderAPI\logs"

    echo   - Izinler guncelleniyor...
    icacls "C:\inetpub\PdfReaderAPI" /grant "IIS AppPool\PdfReaderAPI:(OI)(CI)RX" /T >nul 2>&1
    icacls "C:\inetpub\PdfReaderAPI\logs" /grant "IIS AppPool\PdfReaderAPI:(OI)(CI)M" /T >nul 2>&1

    REM Site kontrolü
    powershell -Command "Import-Module WebAdministration; Test-Path 'IIS:\Sites\PdfReaderAPI'" >nul 2>&1
    if %errorLevel% equ 0 (
        echo   - Site mevcut, guncelleniyor: PdfReaderAPI
        powershell -Command "Stop-Website -Name 'PdfReaderAPI'" >nul 2>&1
        powershell -Command "Set-ItemProperty 'IIS:\Sites\PdfReaderAPI' -Name 'physicalPath' -Value 'C:\inetpub\PdfReaderAPI'" >nul 2>&1
    ) else (
        echo   - Yeni Site olusturuluyor: PdfReaderAPI (Port: 5109)
        powershell -Command "Import-Module WebAdministration; New-Website -Name 'PdfReaderAPI' -PhysicalPath 'C:\inetpub\PdfReaderAPI' -ApplicationPool 'PdfReaderAPI' -Port 5109 -Force" >nul 2>&1
    )

    echo   - Application Pool baslatiliyor...
    powershell -Command "Start-WebAppPool -Name 'PdfReaderAPI'" >nul 2>&1

    echo   - Site baslatiliyor...
    powershell -Command "Start-Website -Name 'PdfReaderAPI'" >nul 2>&1

    echo.
    echo   ✓ IIS Deployment BASARILI!
    echo   ✓ URL: http://localhost:5109
    echo   ✓ Swagger: http://localhost:5109/swagger
    echo   ✓ Hangfire: http://localhost:5109/hangfire
) else (
    echo   ! IIS bulunamadi. Manuel deployment gerekli.
    echo   Publish klasoru: %SCRIPT_DIR%\publish
)
echo.

echo [6/6] Firewall kurallari olusturuluyor...
echo.

netsh advfirewall firewall show rule name="PDF Reader API - HTTP 5109" >nul 2>&1
if %errorLevel% neq 0 (
    echo   - Port 5109 aciliyor...
    netsh advfirewall firewall add rule name="PDF Reader API - HTTP 5109" dir=in action=allow protocol=TCP localport=5109
    echo   - Firewall kurali eklendi
) else (
    echo   - Firewall kurali zaten mevcut
)
echo.

echo ========================================
echo DEPLOYMENT TAMAMLANDI!
echo ========================================
echo.
echo SONRAKI ADIMLAR:
echo.
echo 1. Browser'da test edin:
echo    http://localhost:5109/health
echo.
echo 2. Swagger UI:
echo    http://localhost:5109/swagger
echo.
echo 3. Hangfire Dashboard:
echo    http://localhost:5109/hangfire
echo.
echo 4. Redis durumu kontrol edin:
echo    sc query Memurai
echo.
echo 5. IIS Manager'dan site durumunu kontrol edin:
echo    inetmgr
echo.
echo NOTLAR:
echo - Redis yoksa in-memory storage kullanilir (gelistirme icin)
echo - Production icin Redis kurulumu SART tavsiye edilir
echo - Hangfire Dashboard authentication production'da eklenmelidir
echo.
echo LOG KONUMLARI:
echo - Application Logs: C:\inetpub\PdfReaderAPI\logs\
echo - IIS Logs: C:\inetpub\logs\
echo - Event Viewer: Application Log
echo.
pause
