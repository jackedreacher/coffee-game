# Cooked Fast — Proje Haritası

Dikey (portrait) mobil, izometrik bir mutfak/servis oyunu.

Bu dosyadaki her teknik iddia kaynak dosyadan okunarak yazıldı. Doğrulanamayan
şeyler **"DOĞRULANMADI"** etiketiyle ayrıca işaretlendi — sahne dosyası binary
olduğu için sahne içeriğine dair bazı şeyler ancak Unity'de açılarak görülebilir
(bkz. `SAFE_WORKING_RULES.md`).

---

## Unity sürümü ve önemli paketler

| | |
|---|---|
| Unity | **6000.3.16f1** (`ProjectSettings/ProjectVersion.txt`) |
| Render pipeline | **URP 17.3.0** (`com.unity.render-pipelines.universal`) |
| Ürün adı | `Cooked-Fast` (`ProjectSettings/ProjectSettings.asset`) |
| Şirket adı | `DefaultCompany` — **değiştirilmedi** |

Oyun mantığı için önemli paketler (`Packages/manifest.json`):

| Paket | Sürüm | Ne için |
|---|---|---|
| `com.unity.ai.navigation` | 2.0.12 | NavMesh — oyuncu ve müşteri hareketinin tamamı |
| `com.unity.inputsystem` | 1.19.0 | Dokunma girdisi. `Pointer.current` fare ve dokunmayı birlikte kapsıyor |
| `com.unity.cinemachine` | 3.1.6 | Kamera |
| `com.unity.ugui` | 2.0.0 | HUD, sipariş balonları, TextMeshPro |
| `com.unity.postprocessing` | 3.5.4 | Kurulu, ama URP kendi post-process'ini kullanıyor |

Asset Store paketleri `Assets/` altında: `DGN_15_CapsuleAnimals` (karakterler),
`VFXPACK_FIRE_WALLCOEUR/Waiter_Anims` (65 animasyon klibi),
`Tiny Coffee Shop` (oyunun kendi kodu ve sahnesi), `LeanTween`,
`NaughtyAttributes`, `TotalJSON`.

---

## Aktif sahne ve başlangıç akışı

`ProjectSettings/EditorBuildSettings.asset`:

| Sahne | Build'de |
|---|---|
| `Assets/Scenes/SampleScene.unity` | **kapalı** (`enabled: 0`) |
| `Assets/Tiny Coffee Shop/Game Scenes/Kitchen.unity` | **açık** (`enabled: 1`) |

Yani **build'e giren tek sahne `Kitchen.unity`**. Oyun tek sahnede çalışıyor;
sahne yükleme akışı yok.

`Assets/Tiny Coffee Shop/Game Scenes/Joystick Controller.unity` diye ikinci bir
sahne var ama build listesinde değil — alternatif kontrol şemasının denendiği
sahne.

**Başlangıç akışı** (`Assets/Tiny Coffee Shop/Scripts/Managers/RoundManager.cs`):

1. `RoundManager.Awake` → `Instance` kurulur.
2. `RoundManager.Start` → `rounds` dizisi boşsa uyarı verir, doluysa
   `startRound` ile sınırlanmış turdan `Begin(...)` çağrılır.
3. Tur olayları `RoundAnnounced` → `RoundStarted` → `RoundFinished` →
   `AllRoundsFinished` sırasıyla yayınlanır (`RoundManager.cs:50-53`).
4. `Lives` (`Scripts/Gameplay/Lives.cs`) canları tutar; `Emptied` olayı
   ölüm ekranını tetikler.

Menü/başla ekranı `Cooked Fast > Oyun > Ekran: Basla ve Oldun Ekranlarini Kur`
komutuyla kurulmuş sahne objeleridir. **DOĞRULANMADI:** sahnedeki gerçek obje
hiyerarşisi binary sahne içinde, Unity'de açılmadan okunamıyor.

---

## Oyuncunun temel oyun döngüsü

```
Müşteri gelir  →  sipariş balonu açılır  →  oyuncu malzemeyi toplar
      ↑                                                    ↓
      │                                            pişirmesi gerekiyorsa
      │                                              ocak / fritöz
      │                                                    ↓
  para kasaya uçar  ←  sipariş tamamlanır  ←  müşteriye servis
```

Sabır süresi dolarsa müşteri gider ve `Lives.Lose()` çağrılır. Canlar biterse
tur biter.

---

## Ana etkileşim akış diyagramı

Bu, oyunun tek en önemli akışı. Her adım gerçek sınıfa karşılık geliyor.

```
┌──────────────────────────────────────────────────────────────────┐
│  EKRANA DOKUN                                                    │
│  Pointer.current.press.wasPressedThisFrame                       │
│  TapToServe.HandleTap()                                          │
└───────────────────────────────┬──────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  TapToServe HEDEFİ BULUR                                         │
│  • UI üstündeyse iptal (IsPointerOverBlockingUI)                 │
│  • Physics.RaycastAll — ilk çarpan değil, HEPSİ                   │
│  • Oyuncunun kendi kapsülü elenir                                │
│  • Duvar arkasındaki aday elenir                                 │
│  → Interactable  ya da  yürünebilir zemin                        │
└───────────────────────────────┬──────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  NavMeshAgent HEDEFE YÜRÜR                                       │
│  ClickToMovePlayerController.UpdateMovement()                    │
│  • agent.SetDestination(Interactable.StandPoint)                 │
│  • agent.updateRotation = FALSE  ← kök dönmez                    │
│  • PlayerAnimator.ManageAnimations(velocity, speed)              │
│  • TapToServe.FaceTarget() gövdeyi hedefe çevirir                │
└───────────────────────────────┬──────────────────────────────────┘
                                │  varış
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  HoldFoodAbility İŞLEMİ YAPAR                                    │
│  TapToServe.HandlePendingInteractable() istasyona dağıtır:       │
│    CookingStation → HandleCookingStation (önce al, sonra koy)    │
│    FoodSpawnerStation → HandleFoodSpawnerStation                 │
│    FoodDropZone → HandleFoodDropZone(byTap: true)                │
│    HoldingShelf → Swap  •  FridgeDoor → Tap  •  Trash → Dump     │
│    Customer → TryServe                                           │
│  canGrabFoodDelay kadar bekler, sonra eşya el değiştirir         │
└───────────────────────────────┬──────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  İSTASYON / MÜŞTERİ DURUMU DEĞİŞİR                               │
│  • CookingStation.PutIn / TakeCooked                             │
│  • CustomerOrder satırı dolar, borç azalır                       │
│  • Plateau.Push / Pop — tepsi içeriği                            │
└───────────────────────────────┬──────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  ANİMASYON + SES + UI GERİ BİLDİRİMİ                             │
│  ANİM : PlayerAnimator.PlayAction(PickUp / PickUpCooked /        │
│         Drop / Serve)  →  animator.Play(state)                   │
│  SES  : HoldFoodAbility.Took() / Gave()                          │
│         → SoundManager.Play(ItemTaken / ItemGiven)               │
│  UI   : CustomerOrder tik + PopIn,  SpriteOutline beyaz kenar,   │
│         MoneyCounter.FlyText → Paid() → cash sesi                │
└──────────────────────────────────────────────────────────────────┘
```

**Diyagramdaki en kolay gözden kaçan iki gerçek:**

1. **Dokunmanın kendisi hiçbir şey yapmaz.** Sadece yürünecek yeri seçer.
   Bütün iş, varışta olur. Bu yüzden dokunuşun kendisine ses bağlamak yanlıştı
   (bkz. `KNOWN_ISSUES_AND_DECISIONS.md`).
2. **Kök (root) hiç dönmez.** `agent.updateRotation = false`
   (`ClickToMovePlayerController.cs:87`) ve rotasyonu yazan tek şey
   `PlayerAnimator` içindeki `animator.transform.forward`. Karakterin dönen
   parçası **görsel**, kök değil.

---

## Raund, müşteri, sipariş ve ödeme akışı

**Raund** — `Scripts/Managers/RoundManager.cs`
50 raund var (`Cooked Fast > Oyun > Raund: 50 Raundu Uret` ile üretiliyor).
Her `RoundData` bir sayı ve bir tempo taşır: kaç müşteri, kaç saniyede bir,
kaç çeşit sipariş.

**Müşteri üretimi** — `Scripts/Stations/FoodServingCustomerManager.cs`
`Spawning(interval)` korutini `SpawnNewCustomer()` çağırır, o da
`CustomerManager.Instance.Pop(spawnPoint.position)` ile örneği yaratır
(`CustomerManager.cs:25`). Müşteri gelme sesi tam burada çalar — oyunda müşteri
yaratan **tek** yer burası.

**Sipariş** — `Scripts/Customer/CustomerOrder.cs`
- Balon `iconAnchor` altında satır satır kurulur (`BuildRows`).
- Her satır: yemek ikonu (`rowIcons`) + kapalı bir tik (`rowTicks`).
- Sipariş verildikçe satır dolar: tik `SetActive(true)` olur, ikon griye çekilir
  (`Darken`), ikisi de `PopIn` ile zıplar.
- Sabır sayacı biterse `GiveUp` → müşteri gider, can gider.

**Ödeme** — bu akışta **iki ayrı para yolu** var ve karıştırmak kolay:

| Yol | Ne zaman | Kaç kez | Ses |
|---|---|---|---|
| `CashFile` | her servis edilen **ürün** için | sipariş satırı kadar | **yok** |
| `CustomerOrder.SendMoney` | müşteri hesabı kapatınca | sipariş başına 1 | **var** |

`SendMoney` → `MoneyCounter.FlyText(earningsText, ...)` → rakam kasaya uçar →
`Flying` korutini sonunda `MoneyCounter.Paid(amount)` → cash sesi +
`Deposit` → `CurrencyManager.AddCurrency` + kart zıplar.

---

## Yemek türleri ve istasyonlar

**Yemekler** — `Scripts/Gameplay/`, hepsi `SpawnableFood` türevi:

`Bread`, `Burger`, `Cheese`, `CoffeeCup`, `CookedMeat`, `Drink`, `Fries`,
`Meat`, `Pizza`, `Salad`.

`Burger` bir **tarif** — parçaları elde birleşir
(`HoldFoodAbility.AbsorbIntoBurger`), ayrı bir istasyonda değil.

**İstasyonlar** — `Scripts/Stations/`:

| Sınıf | Ne yapar |
|---|---|
| `FoodSpawnerStation` | Zamanla ham malzeme üretir, oyuncu alır |
| `CookingStation` | Ocak. Çok slotlu, her slot ayrı sayaç, pişer → yanar |
| `FryerStation` | Fritöz. Tek porsiyon, yağ seviyesi görseli, pişer → yanar |
| `FridgeDoor` | Kapı açılır, içecek verir |
| `HoldingShelf` | Park rafı. Bırak / al / takas |
| `FoodDropZone` | Tezgah. Bırak / al / takas |
| `Trash` | Çöp. Tepsinin tamamını boşaltır |
| `FoodServingCustomerManager` | Müşteri üretir, servis noktalarını tanımlar |
| `OrderCounter`, `CashierStation`, `DeskStation`, `UpgradeDeskStation` | Sipariş/kasa/yükseltme tezgahları |
| `Interactable` | Ortak taban: `StandPoint`, `Label` |

Ocak ve fritöz **yanma** mekaniğini paylaşır ama ayrı sınıflar: ocağın slot
başına sayacı var, fritözün tek durum makinesi (`Idle/Frying/Ready/Burnt`).

---

## Mobil kontroller

İki şema var, ikisi de `PlayerController` tabanından türüyor ve birbirinin
yerine geçebiliyor:

| Sınıf | Nasıl |
|---|---|
| `ClickToMovePlayerController` | **Aktif olan.** Ekrana dokun → oraya yürü |
| `JoystickPlayerController` | Ekran joystick'i |

Geçiş menüden: `Cooked Fast > Arac > Switch Player To Click To Move` /
`Switch Player To Joystick`.

Girdi `Pointer.current` üzerinden okunuyor (`TapToServe.cs`) — Input System'in
`Pointer` sınıfı editörde fareyi, telefonda dokunmayı aynı API ile veriyor,
yani ayrı bir dokunma kodu yok.

Ekran yönü ve telefon görünümü: `Cooked Fast > APK > 3 - Telefon Gorunumunu
Duzelt` (`Assets/Editor/PhoneLookFix.cs`).

---

## Android / iOS build süreci

### Android — `Assets/Editor/ApkBuild.cs`

| Komut | Ne yapar |
|---|---|
| `Cooked Fast > APK > 1 - Ayarlari Kontrol Et` | Android modülü kurulu mu, mimari, imza, min SDK — rapor verir, **hiçbir şeyi değiştirmez** |
| `Cooked Fast > APK > 2 - Derle` | Gerekirse platformu Android'e çevirir, `Kitchen.unity`'yi derler |
| `Cooked Fast > APK > 3 - Telefon Gorunumunu Duzelt` | Ekran yönü / görünüm ayarları |

Doğrulanmış ayarlar (`ProjectSettings/ProjectSettings.asset`):

- `AndroidMinSdkVersion: 25`
- `AndroidTargetSdkVersion: 0` → "Automatic (highest installed)"
- `AndroidTargetArchitectures: 2`
- Android bundle id: `com.UnityTechnologies.com.unity.template.urpblank`
  — **hâlâ şablon kimliği, mağazaya çıkmadan değiştirilmeli**

Build **development build** olarak yapılıyor; `ApkBuild.cs:94` civarındaki
yorum bunun sebebini söylüyor: projedeki `Debug.Log` teşhis satırları logcat'e
düşsün diye.

### iOS — `Assets/Editor/IosBuild.cs`

| Komut | Ne yapar |
|---|---|
| `Cooked Fast > iOS > 1 - Ayarlari Kontrol Et` | Rapor, değiştirmez |
| `Cooked Fast > iOS > 2 - Ayarlari Duzelt` | Eksik ayarları yazar |
| `Cooked Fast > iOS > 3 - Xcode Projesi Olustur` | `Builds/iOS` altına **Xcode projesi** üretir |

iOS bundle id: `com.jackedreacher.cookedfast` — bu ayarlanmış.

**Önemli fark:** Android'de çıktı telefona kopyalanabilir bir `.apk`; iOS'ta
çıktı bir **Xcode projesi**, yani derlemenin ikinci yarısı Mac'te Xcode ile
yapılır. `IosBuild.cs:14` bunu açıkça yazıyor. iOS'ta scripting backend her
zaman IL2CPP, seçenek yok.

---

## Nereden devam edilir

| Soru | Dosya |
|---|---|
| Devralındıktan sonra ne değişti, neden değişti | `ASSISTANT_CHANGES.md` |
| Hangi sınıf neyden sorumlu, sınırlar ne | `ARCHITECTURE.md` |
| Adım adım gerçek senaryolar ve başarısız yollar | `GAMEPLAY_FLOWS.md` |
| Animasyon/retarget kuralları | `ANIMATION_GUIDE.md` |
| Tepsi sistemi | `PLATEAU_SYSTEM.md` |
| Menü komutları | `EDITOR_TOOLS.md` |
| Neden böyle yapıldı, ne denendi de olmadı | `KNOWN_ISSUES_AND_DECISIONS.md` |
| Projeye zarar vermeden çalışma kuralları | `SAFE_WORKING_RULES.md` |
| Değişiklik sonrası kontrol listesi | `TEST_CHECKLIST.md` |
