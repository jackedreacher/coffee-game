# Cooked Fast — Devralma Sonrası Yapılan Değişiklikler

Bu belge, projenin geliştirme desteği için devralındığı noktadan bugüne kadar
yapılan önemli değişiklikleri, değişikliklerin gerekçelerini ve ilgili dosyaları
tek yerde toplar.

Belgenin başlangıç noktası Git geçmişindeki **`51cfc9e` — 9 Ağustos 2026**
commit'idir. Güncel durum **20 Ağustos 2026** çalışma ağacıdır.

> Önemli durum ayrımı: `1c6f161` commit'ine kadarki bölümler Git'e kaydedilip
> `origin/main` dalına gönderildi. “Henüz commit edilmemiş güncel çalışmalar”
> bölümündeki değişiklikler proje klasöründe bulunuyor fakat henüz Git'e commit
> edilmedi ve push edilmedi.

---

## Değiştirilemez çalışma kuralları

1. **ASLA `.unity`, `.prefab` veya `.asset` dosyalarında `git checkout`, reset
   ya da benzeri geri alma komutu kullanılmayacak.** Bu dosyalarda Unity
   Editor'da elle yapılan ve Git'in anlamadığı önemli düzenlemeler bulunabilir.
2. Commit ve push yalnızca açıkça istendiğinde yapılacak.
3. `Kitchen.unity` binary kaydediliyor. Sahne içeriği metin aramasıyla güvenilir
   biçimde okunamaz; sahneyle ilgili doğrulama Unity Editor içinde yapılmalıdır.
4. Karakter veya tepsi dönüştürme araçları eski görseli doğrudan silmek yerine
   kapatıp adını değiştirir. Yeni sonuç kontrol edilmeden eski gövde silinmez.

---

## Kısa zaman çizelgesi

| Tarih | Commit / durum | Ana çalışma |
|---|---|---|
| 9 Ağustos | `51cfc9e` | Yeni mutfak sahnesi, tıklayarak hareket, elle servis, sipariş tezgâhı ve karakterler |
| 14 Ağustos | `510e44c` | Tepsi düzenleme araçları, salata istasyonu ve karakter istatistikleri |
| 16 Ağustos | `a538997` | Sipariş balonları, yemek ikonları, yanma sistemi ve istasyon bağlantı onarımları |
| 17 Ağustos | `5aa15ce` | 50 raund, can sistemi, sabır sayacı, oyun ekranları ve para uçuşu |
| 18 Ağustos | `7d22817` | iOS/Xcode üretim yolu, telefon görünümü, doğru hazır tikleri ve yürünebilir alan teşhisleri |
| 18 Ağustos | `1c6f161` | Kapsül hayvan retarget sistemi, klip tarayıcı, tepsi soketi ve bilinen iyi tepsi ayarı |
| 19–20 Ağustos | **commit edilmedi** | Sincap oyuncu, rastgele hayvan müşteriler, aksiyon animasyonları, ses sistemi, müzik, müşteri reaksiyonları, tepsi stabilizasyonu ve dönüş/drift düzeltmeleri |

---

## 1. Mutfak, tıklayarak hareket ve elle servis

İlk büyük değişiklikte oyun, yalnızca eğitim projesi akışından çıkarılıp
portrait mobil oynanışa uygun tıkla-yürü ve tıkla-etkileşim yapısına getirildi.

### Yapılanlar

- Aktif oynanış sahnesi `Kitchen.unity` olacak şekilde mutfak yerleşimi,
  NavMesh ve mobil render ayarları kuruldu.
- `ClickToMovePlayerController` ile ekrana tıklanan yürünebilir konuma gitme
  sistemi eklendi.
- `TapToServe` ile dokunulan nesnenin türü belirlenip uygun istasyona yürüme,
  doğru durma noktasında bekleme ve varışta etkileşme sistemi eklendi.
- Raycast yalnızca ilk çarpanı kabul etmek yerine bütün adayları inceliyor;
  oyuncunun kendi collider'ı ve duvar arkasındaki geçersiz hedefler eleniyor.
- Müşteriye eldeki doğru yemeği elle verme ve yanlış yemeği reddetme akışı
  kuruldu.
- Pizza/Cashier gibi müşteri tezgâhlarının kendisi tap hedefi değildir. Bu
  hiyerarşideki collider ve trigger'lar çalışmaya devam eder fakat
  `TapToServe` seçiminde atlanır; servis edilecek kişi doğrudan müşteriye
  dokunularak seçilir.
- Sipariş tezgâhı, kasa/pickup noktaları ve müşteri kuyruğu yeni mutfak
  düzenine bağlandı.
- Panda oyuncu ve tavşan müşteri görselleri için controller üretimi ve sahne
  bağlantı araçları eklendi.

### Başlıca dosyalar

- `Assets/Tiny Coffee Shop/Scripts/Player/ClickToMovePlayerController.cs`
- `Assets/Tiny Coffee Shop/Scripts/Player/TapToServe.cs`
- `Assets/Tiny Coffee Shop/Scripts/Abilities/HoldFoodAbility.cs`
- `Assets/Tiny Coffee Shop/Scripts/Stations/OrderCounter.cs`
- `Assets/Tiny Coffee Shop/Scripts/Stations/FoodServingCustomerManager.cs`
- `Assets/Editor/KitchenSetup.cs`
- `Assets/Editor/CharacterSetup.cs`

---

## 2. Tepsi, salata ve karakter istatistikleri

Tepsinin yalnızca bir prefab bağlantısı olmaktan çıkıp Editor içinde güvenle
ayarlanabilmesi için ayrı araçlar oluşturuldu.

### Yapılanlar

- Tepsiyi karakter eline bağlayan `PlateauAttach` geliştirildi.
- Konum, açı, boyut ve parent kemiğini düzenlemek için `Plateau Hand Adjuster`
  ve `Plateau Adjuster` pencereleri oluşturuldu.
- Elle ayarlanan iyi müşteri tepsisini diğer müşterilere kopyalama imkânı
  eklendi.
- `Plateau` üzerindeki yemek slotlarının yerleşimi ve tek yemek türü kuralı
  sağlamlaştırıldı.
- Salata istasyonu ve salata yemeği eklendi.
- Oyuncu ve çalışan hız/kapasite değerleri `CharacterStats` ve
  `PlayerStatsHandler` üzerinden gerçek oynanışa bağlandı.
- Kurulumların elle prefab bozmasını azaltmak için rapor ve onarım komutları
  eklendi.

### Başlıca dosyalar

- `Assets/Editor/PlateauAttach.cs`
- `Assets/Editor/PlateauHandWindow.cs`
- `Assets/Editor/PlateauAdjusterWindow.cs`
- `Assets/Tiny Coffee Shop/Scripts/Gameplay/Plateau.cs`
- `Assets/Tiny Coffee Shop/Scripts/Gameplay/FoodPosition.cs`
- `Assets/Tiny Coffee Shop/Scripts/Gameplay/Salad.cs`
- `Assets/Tiny Coffee Shop/Scripts/Gameplay/CharacterStats.cs`

---

## 3. Sipariş balonları, yemekler ve yanma sistemi

Sipariş balonları tek ikonluk geçici gösterimden, aynı üründen birden fazla
istenmesini ve farklı ürün satırlarını gösterebilen gerçek sipariş arayüzüne
dönüştürüldü.

### Sipariş balonu

- Siparişler `OrderLine[]` ile ürün türü + adet şeklinde tutuluyor.
- Balonda her ürün için ayrı satır, ikon ve tamamlanma tiki oluşturuluyor.
- İki ürün varsa ikonlar artık küçültülmüyor; yalnızca üç veya daha fazla
  satırda sıkıştırılıyor.
- Verilen ürünün ikonu kararırken tik kararmıyor.
- Tikler opaque yemek modelinin arkasında kalmasın diye kamera ekseninde öne
  taşınıyor ve yüksek sorting order kullanıyor.
- Tiklerde sekiz kopyadan oluşan beyaz `SpriteOutline` kenarı bulunuyor.
- Yemek ikonu ve tik görünür olduğunda `PopIn` efekti oynuyor.

### Pişirme ve yanma

- Ocak slotları ayrı ayrı ham → pişmiş → yanmış durumlarını takip ediyor.
- Fritöz `Idle`, `Frying`, `Ready`, `Burnt` durum makinesiyle çalışıyor.
- Pişmiş yemek alınabilir; yanmış yemek de istasyonu boşaltmak için alınabilir
  fakat “iyi pişmiş” kabul edilmez.
- Ocak hazır tiki yalnızca en az bir **yanmamış pişmiş** ürün varsa görünür.
  Yanmış ürün alınabilirlik bilgisini korur ama hazır tikini açık tutmaz.
- Ocak ve fritöz sayaçları, ateş/yanma uyarıları ve hazır tikleri için Editor
  kurulum komutları eklendi.
- Ekmek, peynir, et, pişmiş et, patates, içecek, salata ve burger bağlantıları
  yeni mutfak akışına göre onarıldı.

### Başlıca dosyalar

- `Assets/Tiny Coffee Shop/Scripts/Customer/CustomerOrder.cs`
- `Assets/Tiny Coffee Shop/Scripts/Gameplay/OrderLine.cs`
- `Assets/Tiny Coffee Shop/Scripts/Stations/CookingStation.cs`
- `Assets/Tiny Coffee Shop/Scripts/Stations/FryerStation.cs`
- `Assets/Tiny Coffee Shop/Scripts/Utilities/SpriteOutline.cs`
- `Assets/Tiny Coffee Shop/Scripts/Utilities/PopIn.cs`
- `Assets/Editor/OrderBubbleSetup.cs`
- `Assets/Editor/OvenFire.cs`
- `Assets/Editor/ReadyTickSetup.cs`
- `Assets/Editor/FriesSetup.cs`

---

## 4. Raund, can, sabır ve ödeme akışı

Oyuna başı ve sonu olan raund yapısı, müşteri sabrı ve kaybetme durumu eklendi.

### Yapılanlar

- `RoundManager` ve `RoundData` ile 50 raund üretildi.
- Her raund müşteri sayısı, geliş aralığı ve sipariş çeşitliliği taşıyor.
- `Lives` ve `LivesHud` eklendi. Sabırsız müşteri can düşürüyor; can bitince
  ölüm ekranı açılıyor.
- Sipariş balonuna gerçek zamanlı radial sabır sayacı eklendi.
- Sabır çarpanı servis edilen ürünlerle doğru zamanda durduruluyor.
- Başlangıç, raund duyurusu ve ölüm ekranları eklendi.
- Kazanılan para için ekrandan kasaya giden sayı animasyonu oluşturuldu.
- Ürün başına oluşan fiziksel `CashFile` ile sipariş tamamlandığında hesaplanan
  müşteri ödemesi birbirinden ayrıldı.

### Güncel ödeme kuralı

- Ürün tikinin gelmesi para sesi çalmaz.
- Her ürün teslimi para sesi çalmaz.
- Müşterinin bütün siparişi tamamlandığında `Customer.RingUp()` içinde **bir
  kez** cash sesi çalar.
- Para sayısı daha sonra `MoneyCounter` ile kasaya uçar ve bakiye eklenir;
  burada ikinci bir cash sesi üretilmez.

### Başlıca dosyalar

- `Assets/Tiny Coffee Shop/Scripts/Managers/RoundManager.cs`
- `Assets/Tiny Coffee Shop/Scripts/Data/RoundData.cs`
- `Assets/Tiny Coffee Shop/Scripts/Gameplay/Lives.cs`
- `Assets/Tiny Coffee Shop/Scripts/UI/LivesHud.cs`
- `Assets/Tiny Coffee Shop/Scripts/UI/RadialTimer.cs`
- `Assets/Tiny Coffee Shop/Scripts/UI/MoneyCounter.cs`
- `Assets/Tiny Coffee Shop/Scripts/Managers/CurrencyManager.cs`
- `Assets/Editor/RoundSetup.cs`
- `Assets/Editor/LivesSetup.cs`
- `Assets/Editor/GameScreensSetup.cs`

---

## 5. iOS, Android ve telefon görünümü

Windows'ta iOS için uygulama üretilemeyeceği dikkate alınarak, Windows Unity'de
Xcode projesi üretip Mac'e taşıma yolu otomatikleştirildi.

### iOS

`Assets/Editor/IosBuild.cs` üç komut ekledi:

1. `Cooked Fast > iOS > 1 - Ayarlari Kontrol Et`
2. `Cooked Fast > iOS > 2 - Ayarlari Duzelt`
3. `Cooked Fast > iOS > 3 - Xcode Projesi Olustur`

Kontrol edilenler: iOS build modülü, aktif sahne, IL2CPP, cihaz/simülatör hedefi,
bundle identifier, signing, orientation ve telefon görünümü. Xcode çıktısı
`Builds/iOS` klasörüne yazılıyor; Mac adımları aynı klasördeki
`MAC-ADIMLAR.txt` dosyasına ekleniyor.

Windows zip'i shell script çalıştırma bitini korumadığı için Mac'te Xcode build
öncesi ilgili script'lere `chmod +x` verilmesi gerekiyor. Xcode'daki
`PhaseScriptExecution ... Operation not permitted` hatası C# derleme hatası
değil; script dosyasının macOS çalıştırma izni/TCC erişimi problemidir.

### Android ve görünüm

- APK ayar denetimi ve development APK üretimi mevcut.
- Portrait ekran, URP render scale ve telefon kamera görünümü için
  `PhoneLookFix` eklendi.
- Android bundle identifier hâlâ şablon değerindeyse mağaza yayını öncesi
  değiştirilmelidir.

---

## 6. Kapsül hayvan karakter sistemi

DGN kapsül hayvanları ve Waiter animasyonlarını güvenli biçimde deneyebilmek
için kapsamlı bir Editor araç grubu oluşturuldu.

### Araçlar

- `0 - Klip Tarayici`: Hayvan ve animasyon seçip sahnede geçici önizleme yapar.
- `1 / 1b`: Tepsili veya boş elli retarget testi yapar.
- `1c`: Test nesnesini siler.
- `1d`: Unity'nin gerçekten eşlediği humanoid kemikleri raporlar.
- `1e / 1f`: Rig T-pose düzeltmesini dener veya fabrika ayarına döndürür.
- `2`: Customer, Worker ve Player animator controller'larını üretir.
- `3`: Karakterleri kapsül hayvanlara dönüştürür.
- `3c`: Kapsül gövdeyi geri alır.
- `3d`: Sincap hariç rastgele hayvan müşteri prefablarını hazırlar.
- `4 / 5`: Player prefabına veya sahnedeki Player'a sincap koyar.
- `5c`: Sahnedeki sincabı yeniden üretmeden yerinde onarır.
- `5b`: Kafa aksesuarını kaldırır.

### Retarget araştırmasında bulunan gerçekler

- Bütün DGN hayvanları kendi avatarını değil, paketin çalışan örneğinde olduğu
  gibi `DGN_Bear_Outline` avatarını paylaşmalıdır.
- Capsule karakterlerde `AnimatorState.iKOnFeet` açık olduğunda kısa bacaklar
  IK hedeflerine ulaşmaya çalışıp içeri katlanıyor. Bu nedenle Foot IK kapalıdır.
- DGN paketinin `Test_Walking` gibi kendi klipleri import ayarları nedeniyle
  oyundaki Humanoid retarget akışında düzgün sonuç vermedi. Waiter ve mevcut
  Hypercasual Humanoid klipleri daha güvenilir çıktı.
- Panda'nın ayrı animasyon iskeletinde zorunlu `LowerArm`, `Hand` kemikleri ve
  bacak zincirine bağlı `Foot` kemikleri yok. Bu kaynak Humanoid yapılamaz;
  otomatik eşleme hatasını zorlayarak geçmek mümkün değildir.
- Yeni model koyma işlemleri eski gövdeyi silmez; kapatıp
  `(ESKI - kontrol et, sonra sil)` benzeri bir adla saklar.

### Güncel karakter kullanımı

- Player görseli sincap olarak hazırlanmıştır.
- Sincap müşteri havuzuna katılmaz; şef/oyuncu olarak ayrılmıştır.
- Müşteriler için diğer kapsül hayvanlardan rastgele prefab havuzu üretilebilir.
- Normal yürüyüşte Hypercasual `Walk`, boş beklemede `Waiter_Pitcher_Idle`,
  tepsili beklemede `IdleWithPlateau` kullanılır.
- Hızlı `Waiter_Tray_Walk_Forward_Hurry` tepsi ve kol uyumsuzluğunu büyüttüğü
  için normal tepsili yürüyüş tercih edilmiştir.

### Başlıca dosyalar

- `Assets/Editor/CapsuleCharacterSetup.cs`
- `Assets/Editor/CapsuleClipBrowser.cs`
- `Assets/Editor/CapsuleRigFix.cs`
- `Assets/Tiny Coffee Shop/Animations/Capsule/`
- `Assets/Tiny Coffee Shop/Prefabs/Characters/Customers/Capsule Random/`

---

## 7. Tepsi bağlantısı ve stabilizasyonu

Tepsi sistemi birkaç farklı yaklaşım denendikten sonra şu sorumluluk ayrımına
getirildi:

- **Yerleşim**: `Plateau Hand Adjuster` ile tepsinin ele göre local position,
  rotation, scale ve parent ayarı.
- **Animasyon sırasında düz tutma**: `PlateauLevel`.
- **Yemek alma/verme görünürlüğü**: `HoldFoodAbility`, `TapToServe` ve `PopIn`.

### Çözülen problemler

- `Yenile` artık elle yapılan sahne ayarını panel değerleriyle ezmiyor; sahne
  görünümü kaynak kabul ediliyor.
- Model prefab instance'ına eklenen override'ların reimport sırasında kaybolması
  önlendi; gerektiğinde prefab instance güvenli biçimde unpack ediliyor.
- Tepsi ile el arasına isteğe bağlı `PLATEAU SOKET` katmanı eklendi.
- Bilinen iyi müşteri tepsisi ayarı `Assets/Editor/PlateauKnownGood.json`
  dosyasında saklanabiliyor.
- Play Mode'da yapılan hiyerarşi değişikliğinin Play kapanınca kaybolduğu açıkça
  kontrol ediliyor; snapshot anahtarı parent yoluna bağlı olmadığı için mount
  değişince eski ayara dönmüyor.
- Parmak kemiği veya üst kol altına yanlış bağlanan tepsi çalışma anında Humanoid
  `Hand` kemiğine dünya pozu korunarak taşınıyor. Parmakların bükülmesi artık
  tepsiyi elin içine sürüklemiyor.
- Tepsi transformunu animasyondan bağımsız world position ile takip ettiren eski
  yaklaşım kaldırıldı. Bu yaklaşım karakter hareket ettikçe gecikme, drift ve
  her al-bırak döngüsünde biriken konum hatası üretiyordu.

### Güncel `PlateauLevel` yaklaşımı

Tepsi doğrudan ele bağlı kalır ve script tepsinin konumunu **hiç yazmaz**.
Animator kemikleri işledikten sonra `LateUpdate` içinde tepsinin bağlı olduğu
bilek/hand mount kemiğine geçici bir rotasyon düzeltmesi uygulanır. Böylece:

- Tepsi elden kopmuş gibi geride kalmaz.
- Koşarken dünya koordinatında takip gecikmesi oluşmaz.
- Her frame biriken feedback-loop konum hatası oluşmaz.
- `level`, `maxBend` ve `release` değerleri bileği kırmadan tepsiyi mümkün
  olduğunca düz tutar.
- Chef's Kiss gibi elin ağza gittiği ve düzeltmenin fiziksel olarak imkânsız
  olduğu pozlarda düzeltme zorlanmak yerine yumuşakça bırakılır.
- Koşu eğimi `runTilt`, `runSpeed`, `bounce` ve `smoothing` ile bilek üzerinden
  verilir; tepsi hâlâ elin çocuğudur.

### Yemek alma davranışı

- PickUp ve PickUpCooked için uzun el alma klibi oynatılmaz.
- Gerçek etkileşim gerçekleştiği frame'de tepsi ve yemek birlikte ele gelir ve
  kısa `PopIn` efekti yapar.
- Oyuncunun eli doluyken başka yemeğe tıklaması bir pickup denemesidir; Drop veya
  yanlış bir alma animasyonu başlatmaz.
- Tepsiye yemek ekleme tamamlanmadan ölçek animasyonu başlatılmadığı için yemek
  arkadan ele uçuyormuş gibi görünmez.

---

## 8. Oyuncu aksiyon animasyonları

`PlayerAnimator` tek bir aksiyon arayüzüne getirildi:

| Oyun olayı | Animator aksiyonu | Davranış |
|---|---|---|
| Normal malzeme alma | `PickUp` | El klibi yok; tepsi + yemek pop yapar |
| İyi pişmiş et/patates alma | `PickUpCooked` | El klibi yok; tepsi + yemek pop yapar |
| Yanmış ürün alma | `PickUp` | Chef's Kiss yok |
| Tezgâha/çöpe/fritöze bırakma | `Drop` | `Waiter_Tray_BarTop_DropOff` kısa bölüm + transfer pop |
| Müşteriye servis | `Drop` | Diğer bırakma işlemiyle aynı klip ve zamanlama |
| Boş elle müşteriye dokunma | `Greet` | Oyuncu bulunduğu yerde müşteriye döner ve kısa selam/eğilme yapar |

### Zamanlama düzeltmeleri

- Drop klibinin tamamı çok uzun olduğu için yalnızca gerekli ilk bölümü
  oynatılıyor.
- Servis sırasında animasyon müşteri yaklaşımında, yemek hâlâ eldeyken başlıyor;
  gerçek transfer oyuncu tamamen durunca yapılıyor.
- Fritöze patates bırakma animasyonu yaklaşımda değil, `FryerStation.Result.Started`
  oluştuğu gerçek frame'de başlıyor.
- Buzdolabı ve fritöz pop geri bildirimi artık tek `popTarget` ile sınırlı
  değildir. Buzdolabında gövde ve kapak, fritözde sepet/içerik yerine bütün
  görünür makine parçaları ortak merkezden birlikte büyüyüp küçülür. Collider,
  timer, sprite işaretleri ve NavMeshObstacle bu gruba alınmaz.
- Oyuncu fritöze yandan geldiyse eğilme efekti vücudun yanlış yönüne gitmesin
  diye etkileşimden önce görsel gövdenin hedefe dönmesi bekleniyor.
- Yan yana iki fritöze hızlı tıklamada aynı `Drop_Start` state'i yeniden
  başlatılabiliyor; önceki animasyon yarıda iptal olmuş olsa bile ikinci işlem
  animasyonsuz kalmıyor.

---

## 9. Müşteri reaksiyonları ve kutlama

- Her başarılı ürün tesliminde müşteri `React_ChefsKiss` oynatır.
- Reaksiyon boş elle oynar: müşterinin dolu tepsisi geçici olarak gizlenir,
  klip bitince aynı parent, ayar ve yemeklerle geri açılır.
- İkinci ürün ilk Kiss bitmeden verilirse tepsi arada yeniden görünmez; reaksiyon
  yeniden başlasa da tepsi gizli kalır.
- Müşteri sipariş tamamlandığında reaksiyonun tamamını bitirmeden yürümeye
  başlayamaz. Bu kural yalnızca tıklayarak serviste değil, müşteri sınıfının
  merkezi `GoToThen()` geçidinde uygulanır; otomatik servis ve OrderCounter da
  aynı güvenceyi alır.
- Giderken reaksiyon oynatılmaz; reaksiyon biter, sonra Walk state'i başlar.
- Sipariş kutlaması/sunburst toplam 2 saniyedir.
- Kutlama sırasında radial timer kapatılır; emoji erken küçülürken arkasından
  timer halkası görünmez.
- Kiss eklendikten sonra eski spawn sayacı slotun boşalmasını yanlış
  yorumluyordu: dolu kuyrukta interval çoktan bitmiş olduğu için yer açılan ilk
  frame yeni müşteri üretiliyordu. Artık müşteri Kiss'i bitirip servis yerinden
  en az `1.1` birim uzaklaşana kadar slotu rezerve eder. Dolu kuyrukta yer
  gerçekten açıldıktan sonra da yeni spawn için `1` saniyelik ayrı nefes payı
  başlar. Bu kural elle servis, OrderCounter, worker servisi ve sabrı bitip
  ayrılma yollarının tamamında `FoodServingCustomerManager` tarafından merkezi
  uygulanır.

### Başlıca dosyalar

- `Assets/Tiny Coffee Shop/Scripts/Customer/Customer.cs`
- `Assets/Tiny Coffee Shop/Scripts/Customer/CustomerAnimator.cs`
- `Assets/Tiny Coffee Shop/Scripts/Customer/CustomerOrder.cs`
- `Assets/Tiny Coffee Shop/Scripts/Player/TapToServe.cs`

---

## 10. Ses efektleri ve arka plan müziği

Dağınık `AudioSource` çağrıları yerine olay ismiyle çalışan merkezi,
null-safe `SoundManager` eklendi.

### Ses olayları

| Olay | Kullanım |
|---|---|
| `CustomerArrives` | Müşteri prefabı spawn edildiği anda; klibin başındaki yaklaşık 1 saniye sessizlik atlanır |
| `OrderBubbleOpened` | Sipariş balonu pop animasyonu başladığında |
| `ItemTaken` | İçecek hariç gerçekten alınan malzeme |
| `DrinkTaken` | Buzdolabından içecek alma |
| `ItemGiven` | Tezgâha/rafa/çöpe gerçekten bırakılan ürün |
| `FoodReady` | Ocak veya fritöz hazır tiki |
| `Money` | Yalnızca bütün müşteri siparişi tamamlandığında |

Boş zemine, müşteriye veya geçersiz hedefe yalnızca tıklamak alma sesi çalmaz.
Ses artık tap frame'ine değil gerçek transfer sonucuna bağlıdır. Aynı işlemde
hem alma hem verme sesi çıkmaması için `HoldFoodAbility` sahiplik değişiminin
yönünü ayrı değerlendirir. Fritöze ürün koyarken yalnızca fritöz/pişirme sesi
duyulur; ekstra pop/whoosh sesi bindirilmez.

### Pişirme ve müzik

- Pişirme sesi bool değil sayaç kullanır. İki istasyon aynı anda pişirirken
  birinin bitmesi diğerinin loop sesini kesmez.
- Pişirme loop'u açılıp kapanırken fade yapar.
- `Assets/Cyberleaf Music - The 8-bit Jukebox Lite` içindeki 18 parça alfabetik
  sırayla çalar; son parça bitince ilk parçaya döner.
- Müzik için efektlerden ayrı `MUZIK` AudioSource kullanılır.
- Varsayılan müzik seviyesi `0.18`'dir.
- Müzik import ayarları mobil kullanım için Streaming, Vorbis ve yaklaşık
  `0.55` kaliteye ayarlanır.

Kurulum komutu: `Cooked Fast > Ses > 1 - Ses Sistemini Kur`.

### Başlıca dosyalar

- `Assets/Tiny Coffee Shop/Scripts/Managers/SoundManager.cs`
- `Assets/Editor/SoundSetup.cs`
- `Assets/Tiny Coffee Shop/Scripts/Abilities/HoldFoodAbility.cs`
- `Assets/Tiny Coffee Shop/Scripts/UI/MoneyCounter.cs`

---

## 11. Oyuncu dönüşü, 180/360 derece kararsızlığı ve drift

Oyuncunun dönen kısmı NavMesh kökü değil, Animator'un bulunduğu görsel gövdedir.
Bu nedenle fiziksel yönlendirme ve görsel yönlendirme ayrı ele alındı.

### Görsel dönüş

- Görsel gövde dönüş hızı en az `720°/sn` yapıldı.
- Etkileşim hedefini yüzleme hızı en az `16 rad/sn` yapıldı.
- Dönüş yönü `agent.velocity` yerine `agent.steeringTarget` üzerinden okunuyor.
  Local avoidance'ın her frame sağ-sol oynattığı velocity artık görsel gövdeyi
  titreştirmiyor.
- Görsel yaw `LateUpdate` içinde tek noktadan uygulanıyor.
- Tam 180° dönüşte matematiksel olarak sağ ve sol eşit olduğu için önceki
  dönüş tarafı hatırlanıyor; karakter frame'ler arasında karar değiştirmiyor.

### Fiziksel drift

Eski ayarda `agent.acceleration = speed * 2` idi. Karakter bir yöne tam hızla
giderken karşı yöne tıklanınca eski hızı kesip ters hıza ulaşması yaklaşık bir
saniye sürüyordu. Görsel hemen döndüğü hâlde root eski yöne kaymaya devam ettiği
için drift hissi oluşuyordu.

Güncel ayar:

```csharp
agent.acceleration = Mathf.Max(40f, speed * 10f);
agent.angularSpeed = Mathf.Max(agent.angularSpeed, 1440f);
```

Böylece tam yön değişimi yaklaşık `0.2 sn` düzeyine indi. Hareket tamamen
teleport/snap yapılmadı; küçük bir fiziksel yumuşaklık bırakıldı.

### Başlıca dosyalar

- `Assets/Tiny Coffee Shop/Scripts/Player/ClickToMovePlayerController.cs`
- `Assets/Tiny Coffee Shop/Scripts/Player/PlayerAnimator.cs`
- `Assets/Tiny Coffee Shop/Scripts/Player/TapToServe.cs`

---

## 12. Fritöz görselleri

Fritözlerin sahnede sağdan sola alınması ve eksenlerinin çevrilmesinden sonra
timer ve hazır tiklerinin modelin içinde/yanlış tarafında kalması için
`FriesSetup` genişletildi.

- Fritözün gerçek renderer bounds'u ölçülerek tik ve timer world position'ı
  yeniden hesaplanıyor.
- Rotasyon değişmiş olsa bile konum, sabit bir global eksen varsayımı yerine
  fritözün yerel yönlerine göre kuruluyor.
- Timer modelin içinde kalmayacak şekilde fritözün üstüne taşınıyor.
- Hazır tikine beyaz outline ve `PopIn` ekleniyor.

Komut: `Cooked Fast > Istasyon > Patates > 2 - Tik ve Sayaci Hizala`.

---

## 13. Eklenen önemli Editor araçları

Bu liste bütün menülerin dökümü değildir; devralma sonrası en önemli güvenlik ve
teşhis araçlarını gösterir.

| Alan | Komut / araç | Amaç |
|---|---|---|
| Etkileşim | `Neye Tiklanabilir` | Sahnedeki tıklanabilir hedefleri raporlar |
| Etkileşim | `Yurunebilir Zemin Yap/Kaldir/Listele` | Tıklanabilir NavMesh zeminini yönetir |
| Müşteri | `Kuyruk Yolunu Denetle` | Spawn, sıra, servis ve çıkış NavMesh yollarını kontrol eder |
| Müşteri | `Siparis Balonunu Kur` | Balon, timer, ikon, tik ve outline bağlantılarını kurar |
| Müşteri | `Siparis Durumunu Soyle` | Aktif müşteri siparişlerini raporlar |
| İstasyon | Ocak/fritöz kurulum araçları | Collider, slot, sayaç, ateş ve tikleri onarır |
| İstasyon | `Cop: Geri Donusum Ikonunu Kur` | Projedeki çift oklu Refresh ikonunu bütün çöplerin üstüne kamera-dönük ve beyaz kenarlı yerleştirir |
| Tepsi | `Plateau Hand Adjuster` | Parent kemiği, position, rotation ve scale ayarı |
| Karakter | `Klip Tarayici` | Hayvan/klip kombinasyonunu sahnede test eder |
| Karakter | `Avatar Eslemesini Denetle` | Unity'nin gerçek Humanoid bone mapping'ini gösterir |
| Ses | `Ses Sistemini Kur` | SFX, pişirme loop'u ve müzik listesini sahneye bağlar |
| Build | iOS 1/2/3 | Denetle, düzelt ve Xcode projesi oluştur |
| Build | APK 1/2/3 | Android ayarlarını denetle, build al, telefon görünümünü düzelt |

---

## 14. Özellikle denenip terk edilen yaklaşımlar

Bu bölüm önemlidir; aynı hataların ileride “düzeltme” diye yeniden eklenmesini
önler.

- **Capsule Foot IK açık olsun:** kısa kapsül bacakları hedefe ulaşmaya çalışıp
  katlandı. Kapalı kalmalıdır.
- **Her hayvana kendi Avatar'ını ver:** DGN paketinin çalışan prefabı bütün
  hayvanlarda Bear avatarını kullanıyor. Paylaşılan Bear avatarı korunmalıdır.
- **Panda animasyon FBX'ini Humanoid'e zorla:** zorunlu kemikler yok; mümkün
  değil.
- **Tepsiyi world position ile yumuşak takip ettir:** karakter hareketinde lag,
  koşuda geride kalma ve biriken drift üretti. Tepsi ele bağlı kalmalı, gerekirse
  bilek düzeltilmelidir.
- **Tepsiyi parmak kemiğine bağla:** parmak animasyonu tepsiyi elin içine
  sürükledi. Stable mount Humanoid Hand veya onun altındaki bilinçli socket'tir.
- **Her tap'te alma sesi çal:** boş zemin ve müşteri tap'lerinde bile aynı sesi
  çaldı. Ses gerçek işlem sonucuna bağlanmalıdır.
- **Her ürün tikinde cash sesi çal:** ödeme hissini ürün işaretlemeyle karıştırdı.
  Cash yalnızca tamamlanmış müşteri hesabında bir kez çalmalıdır.
- **Hurry yürüyüşünü tepsili ana yürüyüş yap:** gövde ve kol aşırı öne eğildiği
  için kapsül karakterde el/tepsi uyumsuzluğunu büyüttü. Normal yürüyüş daha
  güvenlidir.
- **Sadece TapToServe içinde müşteriyi beklet:** otomatik servis veya
  OrderCounter müşteriyi reaksiyon sırasında yürütebiliyordu. Bekleme kuralı
  `Customer.GoToThen()` içinde merkezi olmalıdır.
- **NavMesh velocity ile görsel yön seç:** avoidance jitter'ı görseli sağa sola
  kararsız döndürdü. Steering target yönü daha kararlıdır.

---

## 15. Güncel Git durumu

Son commit ve remote durumu:

```text
1c6f161 (HEAD -> main, origin/main)
Capsule bodies, a socket for the tray, and a baseline that can be rewritten
```

Bu commit'ten sonra karakter, tepsi, ses, müşteri reaksiyonu, animasyon,
fritöz görseli ve hareket hissi üzerinde çok sayıda değişiklik yapıldı. Bunlar
şu anda **çalışma ağacında; commit/push edilmedi**.

Özellikle çalışma ağacında bulunan yeni dosyalar:

- `Assets/Tiny Coffee Shop/Scripts/Gameplay/PlateauLevel.cs`
- `Assets/Tiny Coffee Shop/Scripts/Managers/SoundManager.cs`
- `Assets/Tiny Coffee Shop/Scripts/Utilities/PopIn.cs`
- `Assets/Editor/SoundSetup.cs`
- `Assets/Editor/PlateauKnownGood.json`
- `Assets/Tiny Coffee Shop/Animations/Capsule/Capsule Player.controller`
- `Assets/Tiny Coffee Shop/Prefabs/Characters/Customers/Capsule Random/`

`Kitchen.unity` ve `Player.prefab` da değiştirilmiş durumdadır. Bu dosyalar
Unity Editor'da elle yapılan ayarları içerdiği için otomatik geri alınmamalıdır.

---

## 16. Devralacak kişi için Unity kontrol listesi

1. Projeyi **Unity 6000.3.16f1** ile aç.
2. Console'da compile hatası olmadığını doğrula.
3. Aktif sahnenin `Assets/Tiny Coffee Shop/Game Scenes/Kitchen.unity` olduğunu
   kontrol et.
4. Sahnede `SoundManager` bağlantıları yoksa bir kez
   `Cooked Fast > Ses > 1 - Ses Sistemini Kur` çalıştır ve `Ctrl+S` yap.
5. Capsule controller'ları yeniden üretmek gerekirse önce çalışma ağacını
   yedekle; `2 - Animator Controllerlarini Uret` bazı controller assetlerini
   mekanik olarak yeniden yazar.
6. Sincap yalnızca bozulmuşsa yeniden model koymak yerine önce
   `5c - Sincabi Yerinde Onar` kullan.
7. Tepsi ayarını `Plateau Hand Adjuster` ile yap. Tepsiyi Finger veya UpperArm
   kemiğine bağlama; RightHand/LeftHand ya da Hand altındaki socket'i kullan.
8. Play Mode'da yapılan sahne hiyerarşisi değişikliklerinin Play kapanınca
   Unity tarafından geri alınacağını unutma. Kalıcı değişiklikleri Edit Mode'da
   kaydet.
9. Aşağıdaki senaryoları gerçek Play Mode'da test et:
   - Art arda sağ-sol ve 180° yön değişimi; drift veya kararsız dönüş olmamalı.
   - Boş elle müşteriye tap; yerinde selam vermeli, müşteriye yürümemeli.
   - Normal malzeme alma; el klibi olmadan tepsi+yemek birlikte pop yapmalı.
   - El doluyken başka ürüne tap; yanlış pickup/drop animasyonu başlamamalı.
   - İki yan yana fritöze sırayla patates bırakma; iki işlemde de Drop başlamalı.
   - Pişmiş ürün alma; yanmış üründe Chef's Kiss olmamalı.
   - Her ürün tesliminde tik+ikon pop yapmalı, cash sesi çıkmamalı.
   - Son ürün tesliminde müşteri Kiss'i bitirmeli, cash bir kez çalmalı, sonra
     yürümeli.
   - Reaksiyon sırasında müşteri tepsisi görünmemeli; reaksiyon bitince doğru
     yemeklerle geri gelmeli.
   - Ocak/fritöz hazır tikinde beyaz outline görünmeli.
   - Müzikler sırayla düşük sesle çalmalı ve liste sonunda başa dönmeli.
10. Sonuçlar onaylanmadan `(ESKI...)` veya `(KAPSUL - kapali)` gövdelerini silme.

---

## 17. Bilinen takip konuları

- Son geniş değişiklik grubu henüz commit edilmedi; onaylı bir oyun testi
  sonrasında tek veya birkaç anlamlı commit'e ayrılmalıdır.
- `Assets/GameData.txt` Git tarafından izleniyor. Bu dosyanın kullanıcıya özel
  kayıt mı yoksa proje başlangıç verisi mi olduğu kararlaştırılmalıdır.
- `Worker.prefab` üzerinde daha önce çift `Worker (Script)` ihtimali görülmüştü;
  Unity Inspector'da yeniden denetlenmelidir.
- `Retarget Test.controller` klip tarayıcı kullanıldıkça değişebilir; bunun
  kalıcı asset mi yoksa üretilebilir test çıktısı mı olacağı belirlenmelidir.
- Android mağaza yayını öncesinde şablon bundle identifier değiştirilmelidir.
- iOS Xcode çıktısında Windows'tan gelen script çalıştırma izinleri Mac'te
  kontrol edilmelidir.

---

## 18. Hyper Casual UI ana menü, pause ve ses ayarları

İlk FCCartoonGUI denemesi, paketin eksik demo bağımlılıkları yüzünden hazır
görseller yerine kendi panel ve yazılarını üretmişti. Bu sistem kaldırıldı.
Yeni menü yalnızca `Assets/Hyper_Casual_UI` paketinin tamamlanmış ekran PNG'leri,
buton sprite'ları, ikonları ve ON/OFF toggle'larını kullanır; kod yeni bir
panel, renk düzeni veya yazı tasarlamaz.

Yeni runtime dosyası:

- `Assets/Tiny Coffee Shop/Scripts/UI/HyperCasualGameMenu.cs`

Yeni Editor kurulum aracı:

- `Assets/Editor/HyperCasualGuiSetup.cs`

Unity'de kurulum komutu:

```text
Cooked Fast > GUI > Hyper Casual GUI Kur
```

Komut şunları üretir:

- Ana menüde Cooked Fast için üretilmiş 9:16 sincap-şef mutfak görselini tam
  ekran kullanır. Üstte `COOKED FAST` başlığı, sağda Settings/Shop, altta
  `HIZLI SERVİS` oyun modu kartı ve büyük Play düğmesi bulunur.
- `Pause`, `Settings`, `Shop Panel` ve `EXIT GAME` ekranlarını paketten doğrudan
  sahneye yerleştirir.
- Hazır `Play`, `Setting`, `Shop`, `Retry`, `Resume`, `Home`, `Pause` ve `Back`
  sprite'larını kendi işlevlerine bağlar. `Close` düğmesi kullanılmaz; paketin
  boş kırmızı düğmesi Baloo yazı tipiyle `QUIT` olarak gösterilir.
- Ana menünün başlık ve açıklamalarında paketin `Baloo2-ExtraBold.ttf` yazı tipi
  kullanılır. Üretilen arka plan asseti
  `Assets/Tiny Coffee Shop/Sprites/UI/CookedFast_MainMenu_Background_NoCharacter.png`dir.
- Ayarlar ekranındaki ses ve müzik alanları paketin hazır `Toggle_ON` /
  `Toggle_Off` sprite'larıyla çalışır ve seçimleri kaydeder.
- Oyun içi HUD'da Pause ve QUIT düğmeleri bulunur.
- Ana menüde fotoğraf karakter kullanılmaz. Ayrı kamera ve RenderTexture 15 DGN
  hayvan modelini gerçek zamanlı gösterir; oklarla veya model üzerinde sağa/sola
  kaydırarak skin seçilir. Seçim `PlayerPrefs` ile saklanır ve Play'e basarken
  yalnız Player'ın görsel `Body` nesnesine uygulanır. Sincap varsayılan skindir;
  seçiliyse elle ayarlanmış mevcut Body ve plateau hiç değiştirilmez.
- Menü ekranlarının arkasında paketin hazır boş `Settings pannel` görselini
  taşıyan ayrı bir `Opaque Background` çocuğu dört kenara sıfır offset ile
  gerilir. Böylece ortadaki panelin oranından bağımsız olarak GUI açıkken canlı
  mutfak üstten/alttan görünmez.
- Canvas kökü aktif kalır fakat bütün menü panelleri Edit Mode'da kapalıdır;
  Device Simulator oyun çalışmıyorken GUI göstermez. Play başladığında ana
  menü otomatik açılır.
- Shop ekranında paketin hazır karakter ikonu mevcut sincap şef seçimine
  bağlanır; Player modeli veya rig üzerinde değişiklik yapılmaz.

Karakter ekranı şu anda yalnızca mevcut ve elle ayarlanmış sincap şefi seçer.
Yeni oynanabilir karakterler hazırlandığında güvenli skin değiştirme sistemi
ayrıca genişletilmelidir.

`SoundManager` müzik ve efekt seviyelerini ayrı `PlayerPrefs` anahtarlarıyla
saklar. Toggle'lar efektleri/pişirme döngüsünü ve müzik listesini birbirinden
bağımsız açıp kapatır.

Kurulum eski `Cartoon GUI` kökünü ve varsa önceki `Hyper Casual GUI` kökünü
kaldırıp yalnızca yeni `Hyper Casual GUI` kökünü oluşturur. Aktif Input System
için `InputSystemUIInputModule` kullanır; eski `UnityEngine.Input` hatasına yol
açan `StandaloneInputModule` bağlantısını kaldırır. `Player`, capsule gövde,
plateau, istasyon prefabları ve diğer elle ayarlanmış sahne nesnelerine dokunmaz.
Komuttan sonra sahne otomatik kaydedilmez; Game View kontrol edilip `Ctrl+S`
yapılmalıdır.

---

## 19. Müşteri çıkış emote ve 180 derece dönüşü

Müşterilerin çıkışı normal bir `GoTo` çağrısı olmaktan çıkarılıp tek bir
`Customer.Leave` akışında toplandı.

- Başarılı siparişte Chef's Kiss bittikten sonra
  `Waiter_Pitcher_Turn_180` tam bir kez oynar, sonra müşteri yürür.
- Sabır bittiğinde önce `Waiter_Idle_TakeOrder_NoGesture`, ardından aynı
  `Waiter_Pitcher_Turn_180` oynar ve müşteri çıkar.
- Bu performans sırasında NavMesh hareketi ve kodun `RotateTowards` dönüşü
  başlamaz. Turn klibinin sonunda mantıksal yön çıkışa eşitlenir; böylece
  yürüyüşe geçerken eski yöne sıçramaz.
- Plateau iki gesture boyunca yalnızca görünmez yapılır; yemekler, parent ve
  el ayarları değiştirilmez, yürüyüş başlamadan önce eski görünürlüğüne döner.
- Elle servis, OrderCounter, PickupStation, masadan kalkış ve süre dolması aynı
  çıkış kapısını kullanır.

Controller'a eklenen state adları:

```text
React_NoGesture
Leave_Turn180
```

Bu state'lerin controller assetine yazılması için bir kez yalnızca
`Cooked Fast > Karakter > 2 - Animator Controllerlarini Uret` çalıştırılır.
Bu komut Player gövdesini veya plateau hiyerarşisini değiştirmez; `5` numaralı
sahneye sincap koyma komutu bu işlem için gerekli değildir.

---

## 20. JSON tabanlı çoklu dil sistemi

Menüdeki dil seçimi yalnızca bir `PlayerPrefs` değeri yazmaktan çıkarılıp
oyunun canlı metinlerine bağlandı. Tek kaynak
`Assets/Resources/Localization/localization.json` dosyasıdır.

- İngilizce, Türkçe, Çince, Hintçe, İspanyolca, Fransızca, Arapça, Bengalce,
  Portekizce, Rusça ve Urduca olmak üzere 11 dil bulunur.
- Seçim `CookedFast.Settings.Language` anahtarında saklanır ve oyun yeniden
  açıldığında korunur.
- Aktif veya kapalı panellerdeki hem klasik `UnityEngine.UI.Text` hem de
  TextMesh Pro yazıları aynı anda yenilenir. Sonradan üretilen istasyon ve
  geliştirme kartları da düzenli taramayla dile alınır.
- Raund numarası, hazırlan ekranı, oyun sonu, kazanç, geliştirme istatistikleri,
  ücretsiz/maksimum etiketleri ve karakter adları dinamik olarak çevrilir.
- Ana menüdeki Play, Settings ve Shop düğmeleri artık üzerinde İngilizce yazı
  basılı sprite kullanmaz. Paketin boş renkli düğmeleri, paket ikonları ve canlı
  çevrilebilir metin çocukları birlikte kullanılır.

Başlıca dosyalar:

- `Assets/Tiny Coffee Shop/Scripts/UI/GameLocalization.cs`
- `Assets/Resources/Localization/localization.json`
- `Assets/Tiny Coffee Shop/Scripts/UI/HyperCasualGameMenu.cs`
- `Assets/Tiny Coffee Shop/Scripts/UI/GameScreens.cs`

---

## İlgili ana doküman

Projenin güncel mimarisi ve oyun akışı için
`Documentation/PROJECT_OVERVIEW.md` okunmalıdır. Bu dosya “ne var ve nasıl
çalışıyor” sorusuna, elinizdeki belge ise “devralındıktan sonra ne değişti ve
neden” sorusuna cevap verir.
