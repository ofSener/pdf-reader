# PDF Extraction İyileştirme Planı

## 📊 Mevcut Durum
- **Toplam Test**: 418 PDF
- **Başarılı**: 203 (%48.6)
- **Kısmi Başarı**: 184 (%44.0)
- **Başarısız**: 31 (%7.4)

---

## 🎯 Hedef
Başarı oranını %85+ seviyesine çıkarmak

---

## 📋 Öncelikli İyileştirmeler

### 1. ⚠️ ACİL: Allianz Trafik Brüt Prim Düzeltmesi
**Durum**: Kısmen çalışıyor (Net prim ✅, Brüt prim ❌)

**Sorun**:
```
PRİM BİLGİLERİ
Net Prim                  SGK Devri         BSM Vergisi
13,944.89 TL             1,267.72 TL        760.63 TL
Garanti Fonu             Trafik Fonu        Ödenecek Prim
253.54 TL                633.86 TL          16,860.64 TL  ← Bu değer alınmalı
```
- Şu an 13,944.89 alıyor (Net Prim değerini)
- Olması gereken: 16,860.64 (Ödenecek Prim değeri)

**Çözüm**:
- "Ödenecek Prim" başlığını bul
- O satırdan sonraki ilk satırdaki en sağdaki değeri al
- Veya: Tüm değerleri al, Net Prim değerinden büyük olanları filtrele

**Tahmini Süre**: 30 dakika
**Etki**: 12 Allianz Trafik PDF'i düzelecek

---

### 2. 🔴 YÜKSEK ÖNCELİK: Kasko Extraction
**Durum**: %3 başarı (33 PDF'den sadece 1 başarılı)

**Sorunlar**:
- Kasko poliçelerinin formatı farklı
- Şirket tespiti çalışıyor ama poliçe tipi yanlış tespit ediliyor

**Aksiyonlar**:
1. 3-5 farklı şirketten Kasko PDF örneği incele
2. Ortak pattern'leri belirle
3. Kasko-specific keywords ekle:
   - "KASKO SİGORTASI"
   - "MOTORLU ARAÇLAR KASKO"
   - "TAM KASKO"
   - "KASKO POLİÇESİ"
4. PolicyTypeDetector'a Kasko pattern'leri ekle
5. Test ve düzelt

**Tahmini Süre**: 2-3 saat
**Etki**: ~30 PDF düzelecek

---

### 3. 🔴 YÜKSEK ÖNCELİK: TSS Extraction
**Durum**: %0 başarı (23 PDF'nin hiçbiri başarılı değil)

**Sorunlar**:
- TSS (Tamamlayıcı Sağlık Sigortası) patterns eksik
- Allianz TSS'de "Toplam Prim" pattern'i var ama çalışmıyor

**Aksiyonlar**:
1. Allianz TSS PDF'lerini incele (4 adet var)
2. Ankara TSS PDF'lerini incele (15 adet kısmi başarı - şirket doğru ama tip yanlış)
3. TSS keywords ekle:
   - "TAMAMLAYICI SAĞLIK"
   - "SAĞLIK SİGORTASI"
   - "TSS POLİÇESİ"
4. PolicyTypeDetector'a TSS logic ekle
5. Allianz için "Toplam Prim" pattern'ini test et

**Tahmini Süre**: 1.5-2 saat
**Etki**: ~23 PDF düzelecek

---

### 4. 🟡 ORTA ÖNCELİK: Konut Extraction
**Durum**: %0 başarı (12 PDF)

**Sorunlar**:
- Konut poliçeleri DASK'a benzer ama farklı
- Sadece brüt prim var (net prim yok)

**Aksiyonlar**:
1. Konut PDF örneklerini incele (Allianz, Sompo, Ray)
2. DASK pattern'lerinden farkları belirle
3. Konut-specific keywords:
   - "KONUT SİGORTASI"
   - "KONUT POLİÇESİ"
   - "EV SİGORTASI"
4. Net prim warning'ini Konut için de disable et (DASK gibi)

**Tahmini Süre**: 1 saat
**Etki**: ~12 PDF düzelecek

---

### 5. 🟡 ORTA ÖNCELİK: DASK Şirket Tespiti
**Durum**: %30 tam başarı, %68 kısmi başarı (63 PDF)

**Sorun**:
- DASK poliçeleri doğru çıkarılıyor ama şirket ismi yanlış
- Örnek: "Ankara DASK" → "Ankara Sigorta" yerine "Unknown" oluyor

**Aksiyonlar**:
1. DASK PDF'lerinde şirket ismi pattern'lerini incele
2. CompanyDetector'da DASK-specific logic ekle:
   - "X SİGORTA DASK" → "X Sigorta"
   - DASK keyword'ünü bul, öncesindeki şirket ismini al
3. Test et

**Tahmini Süre**: 1 saat
**Etki**: ~40 PDF düzelecek

---

### 6. 🟠 DÜŞÜK ÖNCELİK: Doğa Sigorta
**Durum**: 62 kısmi başarı, 10 tamamen başarısız (72 PDF)

**Sorunlar**:
- Doğa poliçelerinin hepsi Unknown olarak tespit ediliyor
- Format problemi olabilir

**Aksiyonlar**:
1. Doğa PDF örneklerini incele
2. CompanyDetector'a Doğa patterns ekle:
   - "DOĞA SİGORTA"
   - "DOGA SİGORTA"
3. Doğa-specific prim formatlarını kontrol et
4. Test et

**Tahmini Süre**: 1.5 saat
**Etki**: ~72 PDF düzelecek

---

### 7. 🟠 DÜŞÜK ÖNCELİK: Hepiyi Kasko/Trafik
**Durum**: 29 kısmi başarı

**Sorun**:
- Hepiyi şirketi tespit ediliyor ama poliçe tipi yanlış

**Aksiyonlar**:
1. Hepiyi PDF'lerini incele
2. PolicyTypeDetector'a Hepiyi-specific patterns ekle
3. Test et

**Tahmini Süre**: 1 saat
**Etki**: ~29 PDF düzelecek

---

### 8. 🟠 DÜŞÜK ÖNCELİK: Ankara Sigorta TSS
**Durum**: 15 kısmi başarı

**Sorun**:
- Şirket doğru tespit ediliyor ama TSS tipi yanlış

**Aksiyonlar**:
- TSS genel iyileştirmesi (Görev #3) ile birlikte çözülecek

**Tahmini Süre**: Görev #3'e dahil
**Etki**: ~15 PDF düzelecek

---

## 📈 Beklenen Sonuçlar

| Görev | Düzelecek PDF | Yeni Başarı Oranı |
|-------|---------------|-------------------|
| Başlangıç | 203 | %48.6 |
| 1. Allianz Brüt Prim | +12 = 215 | %51.4 |
| 2. Kasko | +30 = 245 | %58.6 |
| 3. TSS | +23 = 268 | %64.1 |
| 4. Konut | +12 = 280 | %67.0 |
| 5. DASK Şirket | +40 = 320 | %76.6 |
| 6. Doğa | +50 = 370 | %88.5 |
| 7. Hepiyi | +20 = 390 | %93.3 |

**Hedef Başarı Oranı**: %85+ ✅

---

## 🔄 Çalışma Sırası

### Faz 1: Hızlı Kazançlar (1-2 saat)
1. Allianz Brüt Prim düzelt
2. Konut pattern'leri ekle
3. DASK şirket tespiti düzelt

### Faz 2: Kritik İyileştirmeler (4-5 saat)
4. Kasko extraction
5. TSS extraction

### Faz 3: Şirket-Specific Düzeltmeler (2-3 saat)
6. Doğa Sigorta
7. Hepiyi düzeltmeleri

### Faz 4: Test ve Optimizasyon (1 saat)
8. Tüm dataset'i tekrar test et
9. Başarı oranını kontrol et
10. Edge case'leri düzelt

**Toplam Tahmini Süre**: 8-11 saat

---

## ✅ Tamamlanma Kriterleri

- [x] Başarısız PDF analizi tamamlandı
- [ ] Allianz Brüt Prim düzeltildi
- [ ] Kasko extraction %80+ başarı
- [ ] TSS extraction %80+ başarı
- [ ] Konut extraction %80+ başarı
- [ ] DASK şirket tespiti %90+ başarı
- [ ] Toplam başarı oranı %85+
- [ ] Tüm dataset yeniden test edildi
- [ ] Test raporu hazırlandı

---

## 📝 Notlar

- Her düzeltmeden sonra mutlaka test et
- Bir düzeltme başka bir şeyi bozabilir (regression)
- Özellikle DASK, TSS, Konut gibi benzer tiplerde dikkatli ol
- Pattern öncelikleri önemli (specific → general)
- Confidence score'ları güncellemeyi unutma

---

## 🎯 Son Durum

**Şu an**: Görev #1 (Allianz Brüt Prim) üzerinde çalışılıyor
**Sonraki**: Görev #4 (Konut - kolay ve hızlı)
**Hedef Tarih**: -

