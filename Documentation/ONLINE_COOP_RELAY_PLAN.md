# Cooked Fast — 2 Kişilik Online Co-op ve Unity Relay Planı

> Durum: Teknik tasarım ve uygulama yol haritası  
> Tarih: 20 Ağustos 2026  
> Hedef Unity sürümü: `6000.3.16f1`  
> Kapsam: Bir oyuncu **host**, ikinci oyuncu internet üzerinden **Unity Relay** ile bağlanır. En fazla iki oyuncu aynı mutfakta birlikte oynar.

## 1. Kısa karar

Cooked Fast için önerilen ilk online mimari:

- **Netcode for GameObjects (NGO):** GameObject/MonoBehaviour tabanlı mevcut oyunun ağ katmanı.
- **Unity Multiplayer Services SDK:** Session, Relay ve Lobby işlerini tek API altında yönetir.
- **Unity Transport:** NGO paketlerinin ağ taşıma katmanı.
- **Unity Authentication:** Oyuncuları ilk sürümde kullanıcı adı/şifre istemeden anonim olarak tanımlar.
- **Host-authoritative oyun:** Müşteriler, istasyonlar, yemekler, raund, para ve canların gerçek durumunu host hesaplar.
- **İki kişilik özel session:** Host bir katılma kodu üretir; ikinci oyuncu bu kodu yazarak bağlanır.
- **MVP'de host migration yok:** Host çıkarsa oyun oturumu kapanır ve misafir ana menüye döner.

Unity 6 için eski bağımsız Relay paketini yeni projeye kurmak yerine `com.unity.services.multiplayer` kullanılması öneriliyor; bağımsız Relay paketi Unity 6 akışında kullanım dışına alınıyor. Resmî kurulum ayrıca `com.unity.netcode.gameobjects` paketini ister: [Relay + NGO kurulumu](https://docs.unity.com/en-us/relay/relay-and-ngo), [Multiplayer Services SDK](https://docs.unity.com/en-us/mps-sdk).

Faz 1 (bağlantı prototipi) kodlandı; paketler henüz kurulu değil. Ne yapıldığı ve editörde sırayla ne yapılacağı için bkz. **21. Uygulama günlüğü**.

---

## 2. Projenin bugünkü durumu

`Packages/manifest.json` içinde şu anda:

- `com.unity.multiplayer.center: 1.0.1` var.
- `com.unity.services.multiplayer` yok.
- `com.unity.netcode.gameobjects` yok.
- `com.unity.multiplayer.playmode` yok.
- Relay/Authentication kullanan gameplay kodu yok.

Aktif sahne:

`Assets/Tiny Coffee Shop/Game Scenes/Kitchen.unity`

Mevcut oyun tek makinenin tek doğru oyun durumu olduğu varsayımıyla çalışıyor:

- `RoundManager` raundu başlatıyor ve bitiriyor.
- `FoodServingCustomerManager` müşterileri üretip sırayı yönetiyor.
- `CustomerManager` müşteri prefablarını yaratıyor.
- `TapToServe` oyuncunun dokunmasını, hedef seçimini ve etkileşimi yürütüyor.
- `HoldFoodAbility` oyuncunun elindeki plateau/yemek durumunu tutuyor.
- `CookingStation`, `FryerStation`, `FridgeDoor`, `HoldingShelf` ve diğer istasyonlar doğrudan yerel durumu değiştiriyor.
- `Lives`, `MoneyCounter` ve para yöneticileri tek ortak değeri yönetiyor.

Olumlu taraf: `HoldFoodAbility` ve `TapToServe` oyuncunun kendi GameObject'inde. Ayrıca `FoodServingCustomerManager`, sahnedeki birden fazla `TapToServe` bileşenini şimdiden bulabiliyor. Yani iki oyuncuya geçiş için bazı sınırlar zaten doğru yerde.

Asıl sorun: Bir istemci kendi `TapToServe` kodunu doğrudan çalıştırırsa istasyon ve yemek yalnızca o telefonda değişir. Host aynı işlemi bilmez. Bu nedenle mevcut metotlar ağ üzerinden doğrudan çağrılmayacak; önce hosta bir **etkileşim isteği** gönderilecek.

---

## 3. Ağ topolojisi

```text
┌───────────────────────────────┐
│ Oyuncu 1 — HOST              │
│                               │
│ NGO Server + Local Client     │
│ Raund / müşteri / istasyon    │
│ yemek / para / can otoritesi  │
└───────────────┬───────────────┘
                │
                │ şifreli DTLS trafiği
                ▼
┌───────────────────────────────┐
│ Unity Relay                   │
│ NAT/port açmadan paket aktarır│
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│ Oyuncu 2 — CLIENT            │
│                               │
│ Girdi/RPC gönderir            │
│ Host durumunu görüntüler      │
└───────────────────────────────┘
```

Relay bir oyun sunucusu değildir. Host hâlâ bir oyuncunun telefonu/bilgisayarıdır; Relay yalnızca iki taraf arasındaki paketleri yönlendirir. Relay akışında host allocation oluşturur, bir join code alır, misafir bu kodla allocation'a katılır ve NGO/Unity Transport bağlantıyı kurar: [Relay allocation ve join akışı](https://docs.unity.com/relay/allocating-binding-joining).

### Neden dedicated server değil?

- Oyun yalnızca iki kişilik ve co-op.
- Dedicated server ayrı build, deployment, gözlem ve sürekli sunucu maliyeti getirir.
- Host-authoritative model ilk sürüm için daha hızlı ve yeterince güvenlidir.
- Relay sayesinde router portu açmak veya host IP adresi paylaşmak gerekmez.

### Bilinen bedel

- Hostun cihazı kapanırsa veya interneti giderse oturum biter.
- Host, teorik olarak kendi istemcisini değiştirerek hile yapabilir.
- İki oyuncu farklı kıtalardaysa trafik hostun seçilen Relay bölgesinden geçtiği için gecikme artabilir. Relay tek bir host bölgesi kullanır: [Relay sınırlamaları](https://docs.unity.com/en-us/mps-sdk/relay-limitations).

Bu oyun rekabetçi olmadığı için host güveni kabul edilebilir. Host migration, ilk oynanabilir sürümün kapsamını gereksiz yere büyütmemelidir.

---

## 4. Session ve menü akışı

Ana menüye `ONLINE CO-OP` girişi eklenir.

```text
ONLINE CO-OP
    ├── ODA KUR
    │     ├── UGS başlat
    │     ├── anonim giriş yap
    │     ├── MaxPlayers = 2 özel Relay session oluştur
    │     ├── join code göster + kopyala/paylaş
    │     └── 2. oyuncuyu bekle
    │
    └── KODLA KATIL
          ├── UGS başlat
          ├── anonim giriş yap
          ├── 6–12 karakterlik kodu doğrula
          ├── session'a katıl
          └── hazır odasına geç

İki oyuncu hazır
    └── Host Kitchen sahnesini NGO SceneManager ile başlatır
```

Önerilen ekranlar:

1. `Online Co-op`: Oda Kur / Kodla Katıl / Geri.
2. `Host Lobby`: Büyük join code, Kopyala, Paylaş, `1/2 Oyuncu` bilgisi, İptal.
3. `Join Lobby`: Kod alanı, Katıl, hata mesajı, İptal.
4. `Ready Room`: İki oyuncunun adı/karakteri ve Hazır durumu.
5. `Disconnected`: Host ayrıldı / bağlantı koptu / tekrar dene / ana menü.

Unity'nin güncel Sessions API'si host için `SessionOptions { MaxPlayers = 2 }.WithRelayNetwork()` ve session oluşturma; misafir için `JoinSessionByCodeAsync(joinCode)` akışını sunuyor: [Session oluşturma](https://docs.unity.com/en-us/mps-sdk/create-session), [kodla katılma](https://docs.unity.com/en-us/mps-sdk/join-session).

Kavramsal iskelet:

```csharp
// Gerçek paket sürümü kurulduktan sonra API imzaları o sürümün
// dokümantasyonuna göre doğrulanacak.
await UnityServices.InitializeAsync();

if (!AuthenticationService.Instance.IsSignedIn)
    await AuthenticationService.Instance.SignInAnonymouslyAsync();

// Host
var options = new SessionOptions
{
    MaxPlayers = 2,
    IsPrivate = true,
    Name = "Cooked Fast Co-op"
}.WithRelayNetwork();

ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);
string joinCode = session.Code;

// Misafir
ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
```

Anonim giriş kullanıcıdan form istemez. Ancak oyuncu uygulamayı siler ve anonim hesabı haricî bir hesaba bağlanmamışsa hesap kurtarılamaz: [Unity anonim giriş notları](https://docs.unity.com/en-us/authentication/use-anon-sign-in). İlk co-op sürümünde anonim giriş yeterlidir; ileride Apple Game Center / Google Play Games bağlantısı eklenebilir.

---

## 5. Otorite kuralları

Online oyunun en önemli kuralı: **İstemci sonucu değil, niyetini gönderir. Host sonucu hesaplar.**

Yanlış:

```text
Client: “Patatesi aldım; elimde patates var.”
```

Doğru:

```text
Client: “Fryer #2 içindeki slot #0'a dokundum.”
Host: Mesafeyi, slotu, oyuncunun elini ve istasyon durumunu kontrol eder.
Host: İşlem geçerliyse patatesi oyuncuya verir ve yeni durumu iki tarafa yollar.
```

| Sistem | Yazma otoritesi | Ağda taşınan bilgi |
|---|---|---|
| Oyuncu dokunması | İlgili oyuncu ister, host doğrular | Hedef NetworkObject ID, tıklama/komut türü |
| Oyuncu hareketi | MVP'de host | Hedef nokta, pozisyon, rotasyon, hareket durumu |
| Oyuncunun elindeki yemek | Host | Yemek ID/listesi, plateau doluluğu |
| Ocak/fritöz zamanları | Host | Durum + host başlangıç zamanı veya kalan süre |
| Müşteri spawn/sıra/yürüme | Host | NetworkObject spawn, hedef slot, durum |
| Sipariş içeriği ve tikler | Host | Food ID + gerekli/alınan adet |
| Raund | Host | Raund no, durum, host başlangıç zamanı |
| Para | Host | Ortak para NetworkVariable |
| Can | Host | Ortak can NetworkVariable |
| Animasyon/VFX/SFX | Her cihaz yerel oynatır | Yalnızca güvenilir gameplay olayı/RPC |
| Kamera ve input UI | Her cihaz kendi yerel durumu | Ağda taşınmaz |

NGO'da kalıcı oyun durumları `NetworkVariable`, tek seferlik olaylar RPC ile taşınır. NetworkVariable, sonradan bağlanan istemciye de güncel değeri verir; RPC geçmişi saklamaz: [NetworkVariable davranışı](https://docs-multiplayer.unity3d.com/netcode/current/basics/networkvariable/).

---

## 6. Cooked Fast için gerekli kod ayrımı

### 6.1 Oyuncu

Her bağlı oyuncu için ayrı network player prefabı oluşmalı. Prefab üzerinde en az:

- `NetworkObject`
- `NetworkTransform`
- `NetworkCookedFastPlayer : NetworkBehaviour`
- `ClickToMovePlayerController`
- `TapToServe`
- `HoldFoodAbility`
- `HoldDishAbility`
- `PlayerAnimator`

Yerel olmayan oyuncuda şu parçalar kapalı olmalı:

- Ekran dokunmasını okuyan input.
- Yerel hedef seçimi.
- Yerel kamera takibi.
- Yalnızca sahibine ait UI.

`NetworkCookedFastPlayer.OnNetworkSpawn()` içinde `IsOwner` kontrolüyle bunlar açılıp kapatılır.

### 6.2 Hareket

Mevcut sistem dokunulan hedefe NavMesh üzerinden yürüyor. MVP akışı:

1. Sahip oyuncu ekrana dokunur.
2. Yerelde raycast ile istenen hedef bulunur.
3. `RequestMoveRpc(Vector3 destination)` hosta gider.
4. Host `NavMesh.SamplePosition`, mutfak sınırı ve bloklu alan kontrolü yapar.
5. Hostun `NavMeshAgent` bileşeni hedefe yürür.
6. `NetworkTransform` sonucu diğer oyuncuya interpolate eder.

Bu modelde misafir oyuncunun ilk hareket cevabı Relay gecikmesi kadar geç hissedilebilir. Önce güvenilir sürüm yapılmalı; gerekirse ikinci aşamada sahibi için görsel client prediction eklenmelidir. Client-authoritative `NetworkTransform` daha akıcıdır ama duvar/mesafe hilesine ve host ile pozisyon ayrışmasına daha açıktır.

### 6.3 Etkileşim

`TapToServe` bugün işlemi doğrudan yapıyor. İki parçaya ayrılmalı:

- **Yerel katman:** Dokunma, hedef bulma, hareket hedefi ve kullanıcı geri bildirimi.
- **Host gameplay katmanı:** Gerçek `Take`, `Drop`, `Serve`, `Trash`, `Cook`, `Fry` işlemi.

Önerilen arayüz:

```csharp
public interface IHostInteractable
{
    InteractionResult TryInteract(NetworkCookedFastPlayer player, InteractionRequest request);
}
```

İstemci `NetworkObjectReference` ile hedefi hosta gönderir. Host şunları doğrular:

- Hedef hâlâ var mı?
- Oyuncu doğru stand point'e yeterince yakın mı?
- Oyuncunun eli bu işlem için uygun mu?
- Yemek hâlâ istasyonda mı?
- İstasyon başka oyuncu tarafından aynı anda kullanılmakta mı?
- Müşteri hâlâ o ürünü istiyor mu?
- Oyun pause/game-over durumunda mı?

### 6.4 Aynı şeye aynı anda dokunma

İki oyunculu oyunun en kritik yarışı budur. Her ağ etkileşimi hostta tek sırada işlenir ve ilk geçerli istek kazanır.

Her istasyon/slot için kısa bir reservation gerekir:

```text
StationSlot
    state: Free / Reserved / Processing
    reservedByClientId
    reservationExpiresAt
```

Örnek: İki oyuncu aynı patatese aynı karede dokunursa host ilk aldığı geçerli isteğe verir. İkinci oyuncuya `AlreadyTaken` döner; onun pickup animasyonu ve sesi oynatılmaz.

İşlemler **idempotent** olmalı: Aynı request yeniden ulaşırsa ikinci yemek üretmemeli, ikinci para vermemeli ve ikinci tik oluşturmamalıdır. Her istek oyuncu başına artan `requestSequence` taşımalıdır.

---

## 7. NetworkObject ve senkronizasyon haritası

Her görsel nesneye NetworkObject eklemek doğru değildir. Ağda görsel mesh değil, gameplay kimliği ve durumu taşınmalı.

### NetworkObject olması gerekenler

- İki player prefabı.
- Dinamik müşteriler.
- Ağ üzerinden ayrı yaşam döngüsü gereken dinamik gameplay nesneleri.
- Ortak `NetworkKitchenState` sahne nesnesi.

### Sahne NetworkObject'i olarak kalabilecekler

- Her CookingStation/FryerStation/PickupStation için küçük network bridge.
- OrderCounter ve FoodServingCustomerManager bridge'leri.
- Ortak raund/para/can state nesnesi.

### NetworkObject olmaması gerekenler

- Yemek meshinin her alt parçası.
- Plateau içindeki yalnızca görsel pozisyonlar.
- Sunburst, tick, pop, ses AudioSource'ları.
- Kamera ve canvaslar.
- Karakterin kemikleri ve Animator child objeleri.

Önerilen kompakt veri:

```csharp
public struct NetworkFoodState : INetworkSerializable
{
    public ushort FoodId;
    public byte State;       // Raw, Cooking, Ready, Burnt, Held, Served
    public byte Slot;
    public ulong OwnerId;    // eldeyse oyuncu; değilse 0
}
```

FBX/prefab adı göndermek yerine sabit `FoodId` tablosu kullanılır. İki build aynı ID tablosuna sahip olmalıdır.

---

## 8. Sistem bazında dönüşüm

### Raund ve müşteri

- `RoundManager` yalnızca hostta raund ilerletir.
- Client tarafındaki `RoundManager` spawn veya sayaç kararı vermez; NetworkVariable'ları izler.
- `CustomerManager.Pop` yalnızca host tarafından çağrılır.
- Müşteri prefabında `NetworkObject` bulunur; host `Spawn()` yapar.
- NavMesh hedefleri host belirler.
- Sipariş listesi hostta üretilir ve kompakt ID/adet olarak senkronize edilir.
- Sabır host zamanı üzerinden yürür. İki cihazın ayrı `Time.time` sayaçları kullanılmaz.

### İstasyonlar ve pişirme

- Ocak/fritöz timerı hostta başlar.
- Ağda her kare kalan süre gönderilmez.
- `state`, `startedAtServerTime`, `duration` gönderilir; client görsel timerı bunlardan hesaplar.
- Ready/Burnt geçişini yalnızca host yapar.
- Pop, tick, border ve ses her cihazda durum değişikliği geldiğinde yerel oynar.

### Yemek ve plateau

- Her oyuncunun kendi `HoldFoodAbility` durumu hostta tutulur.
- Plateau animasyonu ve yemeğin eldeki fiziksel pozisyonu yerel görseldir.
- Ağ yalnızca plateau içindeki yemek ID'lerini ve sırasını taşır.
- Pickup/Drop animasyonu host onayından önce kesin sonuç gibi oynatılmaz.
- İstenirse dokunma anında küçük bir bekleme göstergesi, onaydan sonra pop/pickup animasyonu oynatılır.

### Müşteriye servis

- Host müşterinin hâlâ hangi ürüne ihtiyacı olduğunu kontrol eder.
- Başarılı teslim tek kez müşteri sayacını artırır.
- Para yalnızca son ürün host tarafından kabul edildiğinde bir kez eklenir.
- Chef's Kiss, drop-off, cash sesi ve sunburst RPC ile iki cihazda aynı gameplay olayından türetilir.

### Para, can ve upgrade

- Para ve can host yazarlı NetworkVariable olmalı.
- Client doğrudan para ekleyemez veya can düşüremez.
- Upgrade satın alma hostta doğrulanır.
- Save sistemi için net karar gerekir:
  - Öneri: Co-op oturumunun kalıcı ilerlemesi hostun save dosyasına yazılır.
  - Misafir yalnızca kendi karakter tercihi/ayarlarını yerelde saklar.
  - “İki oyuncuya da kalıcı para” istenirse Cloud Save ve ödül politikası ayrıca tasarlanmalıdır.

---

## 9. Oluşturulacak sınıflar

Önerilen klasör:

`Assets/Tiny Coffee Shop/Scripts/Online/`

| Dosya | Sorumluluk |
|---|---|
| `CoopServices.cs` | UGS initialize ve anonim Authentication |
| `CoopSession.cs` | Host oluşturma, kodla katılma, leave/disconnect |
| `CoopConnectionState.cs` | Idle/SigningIn/Hosting/Joining/Waiting/InGame/Error state machine |
| `CoopMenu.cs` | Hazır Hyper_Casual_UI panellerini session akışına bağlama |
| `NetworkCookedFastPlayer.cs` | Ownership, yerel input/kamera, hareket ve etkileşim RPC'leri |
| `NetworkKitchenState.cs` | Raund, para, can ve ortak oyun durumu |
| `NetworkStationBridge.cs` | İstasyon state/reservation ve host doğrulaması |
| `NetworkCustomer.cs` | Spawn, sıra hedefi, sipariş ve reaksiyon durumu |
| `NetworkFoodCatalog.cs` | FoodId ↔ SpawnableFood eşlemesi ve build doğrulaması |
| `NetworkEventEffects.cs` | Onaylı olayları yerel animasyon/VFX/SFX'e çevirme |

Mevcut büyük sınıfları doğrudan `NetworkBehaviour` yapmak yerine bridge/adaptör kullanmak daha güvenlidir. Böylece tek kişilik gameplay kodu tamamen NGO'ya kilitlenmez ve test edilebilir kalır.

---

## 10. Unity Editor kurulumu

### Cloud bağlantısı

1. Projeyi Unity Dashboard'daki doğru Cloud Project'e bağla.
2. Development ve Production environment'larını ayır.
3. Authentication ve Multiplayer/Relay kullanımını Dashboard'dan doğrula.
4. iOS ve Android buildlerinin aynı Unity Project ID ve environment ile çıktığını kontrol et.

### Paketler

Unity Registry üzerinden uyumlu kararlı sürümler kurulmalı:

```text
com.unity.services.multiplayer
com.unity.netcode.gameobjects
com.unity.multiplayer.playmode
```

`Unity Transport`, Multiplayer Services/NGO bağımlılığı olarak gelir; Package Manager'da sürümü ayrıca doğrulanmalıdır.

### NetworkManager

Kitchen'dan ayrı bir bootstrap sahnesi veya kalıcı başlangıç objesi önerilir:

```text
ONLINE
    NetworkManager
        NetworkManager
        UnityTransport (Relay)
    CoopServices
    CoopSession
```

`NetworkManager` ayarları:

- Transport: `UnityTransport`
- Protocol/connection: Relay, mobil için varsayılan güvenli protokol (genellikle DTLS)
- Max Players: Session tarafında 2
- Player Prefab: Ağ için hazırlanmış Player prefabı
- Enable Scene Management: açık
- Kitchen sahnesi Build Profiles listesinde açık

Unity'nin resmî NGO session eğitimi NetworkManager, UnityTransport, NetworkObject player prefabı ve Multiplayer Play Mode ile ikinci Editor penceresini kullanıyor: [NGO ile ilk session](https://docs.unity.com/en-us/mps-sdk/build-your-first-session).

---

## 11. Uygulama sırası

### Faz 0 — Tek kişilik davranışı sabitle

- Mevcut sahnenin smoke test listesini çıkar.
- Yemek alma/bırakma, pişirme, servis, müşteri kaçışı, para ve can testlerini kaydet.
- Online dönüşüm sırasında her fazdan sonra bu testleri tekrar çalıştır.

**Çıkış koşulu:** Multiplayer paketi kurulduğu hâlde Single Player aynı çalışır.

### Faz 1 — Bağlantı prototipi

- Paketleri kur.
- UGS initialize + anonim giriş.
- Host session oluşturup join code göster.
- İkinci Editor penceresini kodla bağla.
- İki basit network capsule hareket ettir.

**Çıkış koşulu:** İki farklı süreç internet/Relay üzerinden aynı session'a girebilir.

### Faz 2 — İki gerçek oyuncu

- Squirrel/player prefabını NetworkObject yap.
- Ownership'e göre input/kamera aç.
- İki spawn noktası ekle.
- NavMesh hareketini host-authoritative yap.
- Karakter seçimini NetworkVariable ile göster.

**Çıkış koşulu:** İki oyuncu aynı mutfakta bağımsız hareket eder ve birbirini doğru görür.

### Faz 3 — Tek istasyon dikey dilimi

- Önce yalnızca Fridge/PickupStation seç.
- Host doğrulamalı pickup/drop.
- İki ayrı HoldFoodAbility state'i.
- Aynı item yarışını reservation ile çöz.

**Çıkış koşulu:** İki oyuncu aynı ürünü çoğaltamaz; el durumları iki tarafta aynıdır.

### Faz 4 — Tüm mutfak

- CookingStation, FryerStation, HoldingShelf, Trash, DropZone.
- Timerların host zamanı ile senkronu.
- Animasyon, pop, tick ve ses olayları.

**Çıkış koşulu:** Host ve client tüm yemek döngüsünü birlikte tamamlar.

### Faz 5 — Müşteriler ve raund

- Host-only spawn ve AI.
- Sipariş/tik/sabır sync.
- Servis, kiss, para, can ve game over.
- İki oyuncunun aynı müşteriye/ürüne eşzamanlı servis yarışı.

**Çıkış koşulu:** Baştan sona bir raund iki kişiyle oynanır ve iki ekranda sonuç aynıdır.

### Faz 6 — Ürünleştirme

- Online menü ve hata mesajları.
- Reconnect denemesi.
- App background/foreground davranışı.
- Development/Production environment.
- iOS ve Android gerçek cihaz testi.
- UGS kullanım ve maliyet uyarıları.

---

## 12. Disconnect politikası

MVP için açık davranış:

| Olay | Davranış |
|---|---|
| Misafir çıkar | Host isterse tek başına devam eder; boş oyuncu despawn olur |
| Misafir kısa süre kopar | 10–20 saniye reconnect ekranı; session hâlâ üyeyse reconnect dene |
| Host çıkar/kapanır | Misafir “Host bağlantısı kesildi” ekranıyla ana menüye döner |
| Relay/session hatası | Gameplay durur, tek anlaşılır hata + tekrar dene/menü seçenekleri |
| Uygulama arka plana gider | Oyuncu “reconnecting” kabul edilir; host sonsuza kadar beklemez |

Relay kendi başına host migration sağlamaz. Güncel Sessions host seçimini desteklese de NGO için varsayılan network state migration yok; resmî doküman NGO kullanırken Distributed Authority yaklaşımını öneriyor: [Session host migration](https://docs.unity.com/en-us/mps-sdk/session-host-migration), [Relay disconnect davranışı](https://docs.unity.com/ugs/en-us/manual/relay/manual/disconnection).

Bu yüzden ilk sürümde host migration eklemek yerine host ayrılınca oturumu temiz kapatmak daha güvenlidir.

---

## 13. Test planı

### Editor

- Multiplayer Play Mode: Player 1 + Player 2.
- Host ve client penceresinde yalnızca kendi oyuncusunun input alması.
- Join code küçük/büyük harf, boşluk, yanlış kod ve dolu oda testleri.
- Host Play'e basmadan clientın mutfağa girememesi.

### Oyun durumu yarışları

- İki oyuncu aynı çiğ ürüne aynı anda dokunur.
- İki oyuncu aynı hazır ürünü aynı anda alır.
- İki oyuncu son ürünü aynı müşteriye aynı anda verir.
- Biri ürünü çöpe atarken diğeri bırakmaya çalışır.
- Biri pause/menü açarken diğerinin gameplay'i devam eder.
- Son can kaybı ve son para ödülü iki ekranda tam bir kez oluşur.

### Bağlantı

- Aynı Wi-Fi ama yine Relay.
- Farklı internetler: ev Wi-Fi + mobil veri.
- 100/200/350 ms gecikme.
- Paket kaybı ve kısa bağlantı kesintisi.
- Client uygulamasını zorla kapatma.
- Host uygulamasını zorla kapatma.
- iOS/Android background'a alma ve geri dönme.

### Build uyumluluğu

- Windows host ↔ Windows client.
- Windows host ↔ Android/iOS client.
- Android host ↔ iOS client.
- İki cihazın aynı oyun sürümünde olmadığı durumda katılımı reddetme.

Session'a `buildVersion` property yazılmalı ve farklı sürümler daha bağlanmadan anlaşılır bir mesajla ayrılmalıdır.

---

## 14. Performans ve bant genişliği kuralları

- Her kare tüm mutfağı serialize etme.
- Timer için her kare değer yollamak yerine host timestamp + duration gönder.
- Kemik/Animator transformlarını senkronize etme; animator state/speed gibi küçük değerlerden yerelde üret.
- Yemek GameObject hiyerarşisi yerine `FoodId` gönder.
- Pozisyon güncellemelerine interpolation ve makul send rate uygula.
- Güvenilir RPC'yi sürekli hareket için kullanma; yalnızca önemli gameplay komutlarında kullan.
- VFX/SFX ağ objesi olmasın; doğrulanmış gameplay olayından yerelde çıksın.
- Sipariş, para ve can gibi geç katılan oyuncunun bilmesi gereken durumları NetworkVariable/NetworkList'te tut.

---

## 15. Güvenlik ve doğrulama

Co-op oyun olsa bile host şu kontrolleri yapmalı:

- Oyuncu hedefe fiziksel olarak yakın mı?
- İstek sırası tekrar mı?
- Saniyede kabul edilen istek sayısı normal mi?
- FoodId katalogda var mı?
- Oyuncunun eli doluyken ikinci pickup isteniyor mu?
- İstasyon o state geçişine gerçekten izin veriyor mu?
- Para/can/raund değerini istemci yazmaya çalışıyor mu?

Join code parola değildir; kısa süreli özel oda anahtarıdır. UI'da paylaşılabilir ama log/analytics içine gereksiz yere yazılmamalıdır.

---

## 16. Ücret ve servis gerçeği

Paketleri kurmak için ayrıca ücretli bir Unity eklentisi satın almak gerekmez. UGS servisleri free tier sınırına kadar kullanılabilir; sınır sonrası model kullanım başına ödemedir. Unity, sınır aşılır ve ödeme bilgisi yoksa servis/API erişiminin engellenebileceğini belirtiyor. Kesin limit ve fiyatlar değişebildiği için Dashboard `Service Usage` ve güncel fiyat sayfası esas alınmalıdır: [UGS pricing and billing](https://docs.unity.com/en-us/services/pricing-and-billing).

Geliştirme sırasında:

- Development environment kullan.
- Dashboard kullanımını haftalık kontrol et.
- Production'a geçmeden önce Relay trafik tahmini yap.
- Hata nedeniyle sonsuz reconnect/allocation döngüsü oluşturma.

---

## 17. MVP kabul kriterleri

İlk online sürüm “tamam” sayılabilmek için:

- [ ] Host tek tuşla iki kişilik private Relay session oluşturabiliyor.
- [ ] Join code ekranda görülebiliyor, kopyalanabiliyor ve paylaşılabiliyor.
- [ ] İkinci oyuncu farklı internetten kodla bağlanabiliyor.
- [ ] Yalnızca iki oyuncu kabul ediliyor.
- [ ] Her oyuncu yalnızca kendi karakterini kontrol ediyor.
- [ ] İki oyuncu aynı istasyonda veri çoğaltamıyor.
- [ ] El/plateau/yemek durumları iki ekranda aynı.
- [ ] Pişirme, yanma ve hazır timerları iki ekranda aynı sonuca ulaşıyor.
- [ ] Müşteri siparişleri, tikler ve sabır iki ekranda aynı.
- [ ] Para ve can yalnızca bir kez değişiyor.
- [ ] Misafir çıkınca host kontrollü şekilde devam edebiliyor.
- [ ] Host çıkınca misafir temiz hata ekranına dönüyor.
- [ ] Single Player modu bozulmadan çalışıyor.
- [ ] Windows + en az iki gerçek mobil cihazla internet testi geçiyor.

---

## 18. Özellikle yapılmaması gerekenler

- Tüm mevcut scriptlere gelişigüzel `NetworkBehaviour` çevrimi yapmak.
- Clientta gameplay sonucunu uygulayıp sonra hosta haber vermek.
- Her mesh, yemek parçası, kemik veya UI elemanına NetworkObject eklemek.
- İki telefonda ayrı müşteri RNG'si çalıştırıp aynı sonucu beklemek.
- Pişirme timerını iki cihazda bağımsız başlatmak.
- Para/can/save değerlerini client yazarlı yapmak.
- Host migration'ı ilk prototipin içine sokmak.
- Relay'i dedicated server sanmak.
- Tek Editor penceresindeki testle internet co-op'un hazır olduğuna karar vermek.

---

## 19. İlk uygulanacak somut iş paketi

İlk kodlama turunun kapsamı yalnızca şunlar olmalı:

1. Paketleri kur ve Cloud Project bağlantısını doğrula.
2. `CoopServices`, `CoopSession`, `CoopConnectionState` sınıflarını yaz.
3. Hazır `Hyper_Casual_UI` kullanarak Oda Kur / Kodla Katıl panellerini bağla.
4. Host join code üretsin, ikinci Editor oyuncusu bağlansın.
5. Geçici iki capsule ile Relay bağlantısını kanıtla.
6. Disconnect/error state'lerini kanıtla.

Bu bağlantı katmanı kanıtlanmadan gerçek sincabı, plateauyu, müşteri sistemini veya istasyonları networke çevirmemek gerekir. Aksi hâlde “bağlantı mı bozuk, gameplay sync mi bozuk?” sorusunun cevabı ayırt edilemez.

Bağlantı prototipi geçtikten sonra ikinci iş paketi gerçek iki player hareketi; üçüncü iş paketi tek bir fridge/pickup dikey dilimidir.

---

## 20. Resmî kaynaklar

- [Unity Multiplayer Services SDK genel bakış](https://docs.unity.com/en-us/mps-sdk)
- [Multiplayer Services başlangıç](https://docs.unity.com/en-us/mps-sdk/get-started)
- [NGO ile ilk session](https://docs.unity.com/en-us/mps-sdk/build-your-first-session)
- [Session oluşturma ve Relay network](https://docs.unity.com/en-us/mps-sdk/create-session)
- [Join code ile session'a katılma](https://docs.unity.com/en-us/mps-sdk/join-session)
- [Relay'i NGO ile yapılandırma](https://docs.unity.com/en-us/relay/relay-and-ngo)
- [Relay allocation/bind/join kavramları](https://docs.unity.com/relay/allocating-binding-joining)
- [Anonymous Authentication](https://docs.unity.com/en-us/authentication/use-anon-sign-in)
- [Session host migration sınırları](https://docs.unity.com/en-us/mps-sdk/session-host-migration)
- [UGS fiyatlandırma ve kullanım](https://docs.unity.com/en-us/services/pricing-and-billing)

---

## 21. Uygulama günlüğü

> Son güncelleme: 22 Ağustos 2026

### Faz 1 — bağlantı prototipi: kod yazıldı, kurulum bekliyor

Bölüm 19'daki ilk iş paketi kodlandı. Paketler **henüz kurulu değil**; kurulumu başlatan komut da bu turda yazıldı.

#### Yazılan dosyalar

| Dosya | Ne yapıyor |
|---|---|
| `Assets/Tiny Coffee Shop/Scripts/Online/Coop.cs` | Durum makinesi ve menünün gördüğü tek yüz. Paketlerden **bağımsız derlenir** |
| `Assets/Tiny Coffee Shop/Scripts/Online/CoopSession.cs` | UGS init, anonim giriş, Relay session kur/kodla katıl/ayrıl |
| `Assets/Tiny Coffee Shop/Scripts/Online/CoopBootstrap.cs` | NetworkManager + UnityTransport'u çalışırken üretir |
| `Assets/Tiny Coffee Shop/Scripts/Online/CoopCapsule.cs` | Geçici test oyuncusu. Tıklama hosta **istek** olarak gider, hareketi host yapar |
| `Assets/Tiny Coffee Shop/Scripts/Online/CoopMenu.cs` | Altı ekran: seçim, oda kodu, kod girişi, bekleme, hata, oyun |
| `Assets/Editor/OnlineDefines.cs` | Paketler gelince `COOP_ONLINE` tanımını açar, gidince kapatır |
| `Assets/Editor/OnlineSetup.cs` | Paketleri kurar ve kurulumu denetler |
| `Assets/Editor/CoopTestSetup.cs` | Kapsül prefabını, test sahnesini ve online ekranları üretir |

#### Neden `#if COOP_ONLINE`

Online kodu doğrudan Netcode/Relay'e yazılsaydı, paketler gelene kadar `Assembly-CSharp` derlenmezdi. `Assembly-CSharp` derlenmediğinde oyun da, Play modu da, `Cooked Fast` altındaki kırk küsur setup komutu da çalışmaz — paketleri kuracak komut dahil. Bu yüzden her online dosyası tanımın arkasında; tanımı da paketlerin *tiplerini* arayan bir editör betiği açıp kapatıyor. Paketler yokken proje eskisiyle bire bir aynı.

Ters yöne sıkışırsa (tanım açık, paket silinmiş) çıkış yolu:
`Project Settings > Player > Other Settings > Scripting Define Symbols` içinden `COOP_ONLINE` elle silinir.

#### Plandan sapmalar

- **NGO scene management kapalı.** Oyun tek sahne: menü zaten mutfağın üstüne çiziliyor. İki oyuncu da butona basmadan önce mutfaktalar, host'un sahne yükletmesi sadece siyah ekran ve ikinci kez uyanan yöneticiler demek olurdu.
- **NetworkManager sahnede değil,** çalışırken kuruluyor. Elle yerleştirilseydi eklenen her yeni sahneye tekrar yerleştirilmesi gerekirdi; bu hâliyle tek kişilik oyun hiçbir bedel ödemiyor, buton basılmadan session açılmıyor.
- **Kapsül prefabı `Resources/Online/` altında.** Atanacak sahne olmadığı için.

#### Editörde yapılacaklar, sırayla

1. `Cooked Fast > Online > 1 - Paketleri Kur` — sürüm numarası verilmiyor, bu Unity sürümüne uyan en yenisi kuruluyor.
2. Derleme bitsin. `COOP_ONLINE` kendiliğinden açılır.
3. `Cooked Fast > Online > 2 - Kurulumu Kontrol Et` — paket sürümleri, tanım, Cloud Project ID.
4. Unity Dashboard'da **Authentication** ve **Multiplayer/Relay** servislerini aç.
5. `Cooked Fast > Online > 3 - Test Sahnesini Kur` — kapsül prefabı + `Assets/Scenes/Coop Test.unity`.
6. `Window > Multiplayer > Multiplayer Play Mode` ile Player 2'yi aç, Play'e bas, birinde **ODA KUR**, diğerinde **KODLA KATIL**.

### Ana menüde ikinci oyuncu

Faz 1 host tarafı çalıştıktan sonra eklendi: ana menüde karakterinin sağında ikinci bir kare var. Boşken içinde **+** ve `ARKADAS EKLE` yazıyor, basınca online ekranları açılıyor. İkinci oyuncu bağlanınca ekran kendiliğinden kapanıyor ve karenin içinde **onun seçtiği hayvan** duruyor — ikon değil, gerçek 3B model, senin karakterinin çizildiği vitrinin aynısıyla.

Yeni dosyalar:

| Dosya | Ne yapıyor |
|---|---|
| `Scripts/Online/CoopPlayerBadge.cs` | Oyuncu objesinde taşınan tek sayı: hangi hayvan seçildi |
| `Scripts/Online/CoopMateSlot.cs` | Menüdeki kare — artı, portre ve kendi mini sahnesi |
| `Scripts/Online/CoopTestRoom.cs` | Test odası işareti |
| `Editor/CoopPanels.cs` | Online ekranları (test sahnesinden ortak yere alındı) |
| `Editor/CoopCanvas.cs` | Ortak portre canvas'ı |
| `Editor/CoopMenuSetup.cs` | `4 - Ana Menuye Ekle` |

Kararlar:

- **Karakter seçimi `NetworkVariable`, RPC değil.** Zaten odada duran birinin üstündekini sonradan giren oyuncunun öğrenmesi gerekiyor; o oyuncu gelmeden önce atılmış bir RPC hiç duyulmaz. Değişkenler geç gelene de veriliyor, olaylar verilmiyor.
- **İsim değil indeks gönderiliyor.** İki build aynı gardırobu aynı sırada taşıyor, yani indeks karakterin kendisi — ve prefab adı değişince bozulmuyor.
- **Hayvan listesi vitrinden ödünç alınıyor.** İkinci bir liste, yeni hayvan eklendiğinde yarı yarıya güncellenen bir liste demek.
- **İki 3B sahne 60 birim arayla.** Projenin URP renderer'ı sadece belirli katmanları çiziyor ve o katman sıfır, yani iki vitrin katmanla ayrılamıyor; mesafeyle ayrıldı (her kamera 20 birim görüyor).
- **Kapsül mutfakta gizleniyor.** Oyuncu prefabı hâlâ gri kapsül; menüden bağlanınca mutfağın ortasına iki tane düşerdi. Yok edilmiyor, sadece görünmüyor — üstündeki rozet menünün ihtiyacı olan şey.
- **`4 - Ana Menuye Ekle` toplama çalışıyor.** Sahnedeki hiçbir şeye dokunmuyor, iki obje ekliyor. Tekrar çalıştırınca yalnız o ikisini yeniden kuruyor.

Dikkat: `Cooked Fast > GUI > Hyper Casual GUI Kur` canvas'ı sıfırdan kuruyor, yani bu iki objeyi de siler. O komuttan sonra `4 - Ana Menuye Ekle` tekrar çalıştırılmalı.

#### Faz 1 geçti sayılması için

- [ ] İki süreç aynı session'a giriyor.
- [ ] Kapsüller çakışmadan, farklı yerlerde doğuyor.
- [ ] Zemine tıklayınca kapsül **iki ekranda da** aynı yere gidiyor.
- [ ] Misafir çıkınca host `1/2` ekranına dönüyor.
- [ ] Host çıkınca misafir "Odayı kuran oyuncu ayrıldı" ekranına düşüyor.
- [ ] Single Player mutfak eskisi gibi çalışıyor.

Bu liste tamamlanmadan sincap, plateau, müşteri ve istasyonlar networke çevrilmeyecek — bölüm 19'un son paragrafı.
