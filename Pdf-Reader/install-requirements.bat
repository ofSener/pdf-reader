@echo off
REM ========================================
REM PDF Reader API - Requirements Installation
REM ========================================
REM Sadece gerekli servisleri kurar
REM Publish, port veya IIS konfigurasyonu YAPMAZ
REM ========================================

echo ========================================
echo PDF Reader API - Requirements Kurulumu
echo ========================================
echo.
echo Bu script sadece gerekli servisleri kurar:
echo 1. .NET 9.0 Runtime kontrolu
echo 2. Redis (Memurai) kurulumu (opsiyonel)
echo.
echo NOT: Publish, IIS veya port ayari YAPMAZ.
echo      Siz kendiniz publish edip kopyalayacaksiniz.
echo.

REM Yönetici kontrolü
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo HATA: Bu script yonetici olarak calistirilmalidir!
    echo Sag tiklayin ve "Run as Administrator" seçin.
    pause
    exit /b 1
)

echo [1/2] .NET 9.0 Runtime kontrolu...
echo.

dotnet --version >nul 2>&1
if %errorLevel% neq 0 (
    echo HATA: .NET 9.0 Runtime bulunamadi!
    echo.
    echo Lutfen .NET 9.0 Hosting Bundle'i indirin:
    echo https://dotnet.microsoft.com/download/dotnet/9.0
    echo.
    echo Download link'e gidin ve:
    echo "ASP.NET Core Runtime 9.0.x - Windows Hosting Bundle" indirin.
    echo.
    pause
    exit /b 1
) else (
    dotnet --version
    echo   - .NET Runtime: OK
)
echo.

echo [2/2] Redis (Memurai) kurulumu...
echo.
echo Redis, background job'larin saklanmasi icin gereklidir.
echo.
echo Memurai kurmak istiyor musunuz?
echo - Production: EVET (tavsiye edilir)
echo - Development/Test: HAYIR (in-memory storage kullanilir)
echo.
set /p INSTALL_REDIS="Memurai yuklensin mi? (Y/N): "

if /i "%INSTALL_REDIS%"=="Y" (
    echo.
    echo Memurai kontrol ediliyor...

    REM Servis var mi kontrol et
    sc query Memurai >nul 2>&1
    if %errorLevel% equ 0 (
        echo   - Memurai zaten kurulu: OK
        echo   - Servis baslatiliyor...
        net start Memurai >nul 2>&1
        if %errorLevel% equ 0 (
            echo   - Memurai baslatildi
        ) else (
            echo   - Memurai zaten calisiyor
        )
    ) else (
        echo.
        echo Memurai bulunamadi. Indirme islemi basliyor...
        echo.
        echo Browser'da asagidaki link acilacak:
        echo https://www.memurai.com/get-memurai
        echo.
        echo Lutfen:
        echo 1. Memurai Developer Edition indirin (UCRETSIZ)
        echo 2. Setup dosyasini calistirin
        echo 3. Kurulum tamamlandiktan sonra bu pencereye donun
        echo.
        pause

        REM Browser'da ac
        start https://www.memurai.com/get-memurai

        echo.
        echo Kurulum tamamlandi mi? (Enter'a basin)
        pause >nul

        REM Kurulum kontrol
        sc query Memurai >nul 2>&1
        if %errorLevel% equ 0 (
            echo   - Memurai basariyla kuruldu: OK
            echo   - Servis baslatiliyor...
            net start Memurai >nul 2>&1
            if %errorLevel% equ 0 (
                echo   - Memurai baslatildi
            ) else (
                echo   - Memurai zaten calisiyor
            )
        ) else (
            echo.
            echo WARNING: Memurai kurulumu dogrulanamadi.
            echo Redis olmadan projeniz in-memory storage kullanacak.
            echo Bu development icin sorun degil, production icin tavsiye edilmez.
        )
    )
) else (
    echo.
    echo   - Redis kurulumu atlandi
    echo   - Proje in-memory storage kullanacak
    echo.
    echo NOT: Production deployment icin Redis kurmanizi oneririz.
)
echo.

echo ========================================
echo REQUIREMENTS KURULUMU TAMAMLANDI!
echo ========================================
echo.
echo KURULU SERVISLER:
echo ✓ .NET 9.0 Runtime

sc query Memurai >nul 2>&1
if %errorLevel% equ 0 (
    echo ✓ Redis (Memurai)
) else (
    echo - Redis (in-memory storage kullanilacak)
)

echo.
echo SONRAKI ADIMLAR:
echo.
echo 1. Projeyi kendi yontemizle publish edin:
echo    Visual Studio'dan: Publish
echo    VEYA
echo    dotnet publish -c Release -o ".\publish"
echo.
echo 2. Publish klasorunu IIS klasorunuze kopyalayin
echo.
echo 3. IIS Manager'dan Application Pool'u restart edin
echo.
echo 4. Test edin:
echo    https://your-domain.com/health
echo    https://your-domain.com/swagger
echo    https://your-domain.com/hangfire
echo.
echo REDIS BAGLANTI AYARI (appsettings.json):
echo   "ConnectionStrings": {
echo     "Redis": "localhost:6379,abortConnect=false"
echo   }
echo.
echo Redis yoksa uygulama otomatik in-memory storage kullanir.
echo.
pause
