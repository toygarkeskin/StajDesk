# StajDesk — Öğrenim Günlüğü

Bu dosya, staj süresince öğrendiğim konuları, yaptığım uygulamaları ve proje geliştirme sürecindeki notlarımı içerir.

Amacım, öğrendiğim konuları yalnızca teorik olarak bilmek değil, bunları uygulayarak anlamak ve kendi cümlelerimle açıklayabilmektir.

---

## İçindekiler

- [Faz 0 — Temeller](#faz-0--temeller)
  - Hafta 1 — Başlangıç, Git ve Web
  - Hafta 2 — Docker ile Tanışma
- [Faz 1 — C# ve .NET](#faz-1--c-ve-net)
  - Hafta 3 — C# Temelleri
  - Hafta 4 — Nesne Yönelimli Programlama
- [Faz 2 — ASP.NET Core Web API](#faz-2--aspnet-core-web-api)
  - Hafta 5 — Web API ve Katmanlı Mimari
- [Genel Değerlendirme](#genel-değerlendirme)

---

# FAZ 0 — TEMELLER

## Hafta 1 — Başlangıç, Git ve Web'in Çalışma Mantığı

**Haftanın hedefi:** Geliştirme ortamını tanımak, Git ile çalışmak ve web uygulamalarının temel çalışma mantığını anlamak.

### Gün 1/48 — Tanışma ve Ortam Kurulumu

**Öğrendiklerim**

StajDesk projesinin amacını ve staj sürecindeki öğrenme hedeflerini öğrendim. Yazılım ekiplerinde görev takibi, sürüm kontrolü ve kod incelemesinin neden önemli olduğunu gördüm.

Kullandığım araçların temel görevlerini öğrendim:

- **VS Code:** Kod yazmak ve dosyaları düzenlemek için kullanılır.
- **Git:** Kod değişikliklerini takip etmeyi sağlar.
- **.NET SDK:** C# ve .NET uygulamalarını geliştirmek ve çalıştırmak için kullanılır.
- **Node.js:** JavaScript kodlarını çalıştırmayı ve JavaScript tabanlı araçları kullanmayı sağlar.
- **Docker Desktop:** Konteynerleri oluşturmak ve yönetmek için kullanılır.

**Uygulamalar**

Araçların kurulumlarını ve sürüm kontrollerini yaptım.

```bash
dotnet --version
node -v
git --version
docker --version
```

Öğrendiklerimi düzenli tutmak için `LOG.md` dosyasını kullanmaya başladım.

### Gün 2/48 — Terminal ve Git Temelleri

**Öğrendiklerim**

Terminal üzerinden klasörler arasında geçiş yapmayı, dosyaları listelemeyi ve yeni klasör oluşturmayı öğrendim.

```powershell
cd
dir
mkdir
```

Git'in temel kavramlarını öğrendim:

- **Repository:** Proje dosyalarının ve değişiklik geçmişinin tutulduğu depo.
- **Commit:** Değişiklikleri yerel Git geçmişine kaydetme işlemi.
- **Branch:** Ana çalışmayı etkilemeden ayrı bir geliştirme alanında çalışmayı sağlar.
- **Merge:** Bir branch üzerindeki değişiklikleri başka bir branch ile birleştirir.
- **Remote:** Uzak Git deposudur.
- **Push:** Yerel commitleri uzak depoya gönderir.

README dosyasının projenin amacını ve kullanımını açıklamak için kullanıldığını öğrendim. Commit mesajlarında yapılan değişikliği açıkça belirtmeye dikkat ettim.

### Gün 3/48 — Web, HTTP ve JSON

**Öğrendiklerim**

Web uygulamalarında istemci ve sunucunun nasıl iletişim kurduğunu öğrendim. Tarayıcının bir istek gönderdiğini, sunucunun bu isteği işleyerek yanıt verdiğini anladım.

Temel HTTP metotları:

| Metot | Kullanım amacı |
|---|---|
| GET | Veri almak |
| POST | Yeni veri oluşturmak veya veri göndermek |
| PUT | Var olan veriyi güncellemek |
| DELETE | Veri silmek |

Temel HTTP durum kodları:

| Kod | Anlamı |
|---|---|
| 200 | İstek başarılı |
| 201 | Yeni kaynak oluşturuldu |
| 400 | İstek hatalı |
| 401 | Kimlik doğrulaması gerekli veya geçersiz |
| 404 | İstenen kaynak bulunamadı |
| 500 | Sunucu tarafında beklenmeyen hata |

JSON'ın uygulamalar arasında veri alışverişinde kullanılan bir veri formatı olduğunu öğrendim.

**Uygulamalar**

- Tarayıcının Network sekmesinden HTTP isteklerini inceledim.
- Postman veya Bruno üzerinden GET ve POST istekleri üzerinde çalıştım.
- İstek metodu, adres ve durum kodu arasındaki ilişkiyi inceledim.

### Hafta 1 — Kavrama Soruları

**1. Tarayıcıya bir adres yazıp Enter'a bastığımızda ne olur?**

Tarayıcı adresi işler ve ilgili sunucuya ulaşmaya çalışır. Sunucuya HTTP isteği gönderilir. Gelen yanıt tarayıcı tarafından işlenir ve sayfa görüntülenir. Bu süreçte DNS çözümlemesi ve gerekli ağ bağlantıları da gerçekleşebilir.

**2. Commit ile push arasındaki fark nedir?**

Commit, değişiklikleri bilgisayarımdaki Git geçmişine kaydeder. Push ise bu commitleri uzak depoya gönderir.

**3. 404 ile 500 arasındaki fark nedir?**

404, istenen kaynağın bulunamadığını belirtir. 500 ise sunucu isteği işlerken beklenmeyen bir hatayla karşılaştığını gösterir. Her iki durumda da sorunun kesin nedenini anlamak için ilgili kayıtları incelemek gerekir.

---

## Hafta 2 — Docker ile Tanışma

**Haftanın hedefi:** Konteyner mantığını anlamak ve Docker ile uygulamaları daha tutarlı ortamlarda çalıştırmayı öğrenmek.

### Gün 4/48 — Konteyner Nedir, Neden Docker?

**Öğrendiklerim**

Bir uygulamanın farklı bilgisayarlarda farklı sonuçlar vermesinin nedenlerinden birinin ortam farklılıkları olduğunu öğrendim. Docker, uygulamanın ihtiyaç duyduğu bileşenleri daha tutarlı bir ortamda çalıştırmaya yardımcı olur.

- **Image:** Konteyner oluşturmak için kullanılan şablondur.
- **Container:** Bir image üzerinden oluşturulan çalışma ortamıdır.
- **Port mapping:** Konteynerdeki bir porta bilgisayardan erişmeyi sağlar.
- **Docker Hub:** Docker image'larının paylaşılabildiği bir kayıt deposudur.

**Uygulamalar**

```bash
docker run hello-world
docker run -d -p 8080:80 nginx
docker ps
docker logs <container_id>
docker stop <container_id>
docker rm <container_id>
```

Bu komutların konteyner çalıştırma, listeleme, log inceleme, durdurma ve silme işlemlerinde nasıl kullanıldığını öğrendim.

### Gün 5/48 — Dockerfile Yazmak

**Öğrendiklerim**

Dockerfile'ın bir image'ın nasıl oluşturulacağını tanımladığını öğrendim.

Temel Dockerfile komutları:

| Komut | Görevi |
|---|---|
| FROM | Başlangıç image'ını belirler |
| WORKDIR | Çalışma dizinini belirler |
| COPY | Dosyaları image içerisine kopyalar |
| RUN | Image oluşturulurken komut çalıştırır |
| EXPOSE | Uygulamanın kullanması beklenen portu belirtir |
| CMD | Konteyner başlatıldığında çalıştırılacak varsayılan komutu tanımlar |

Image katmanlarını ve build önbelleğini inceledim. Volume kullanmanın, konteynerin yaşam döngüsünden bağımsız veri saklamaya yardımcı olduğunu öğrendim.

**Uygulama**

Basit bir HTML sayfasını Nginx ile sunma ve Dockerfile üzerinden image oluşturma mantığı üzerinde çalıştım.

```bash
docker build -t stajdesk-web .
docker run -d -p 8080:80 stajdesk-web
```

### Gün 6/48 — Docker Compose ve Proje Mimarisi

**Öğrendiklerim**

Docker Compose ile birden fazla servisin tek bir yapılandırma dosyası üzerinden yönetilebildiğini öğrendim.

- **Network:** Konteynerlerin birbiriyle iletişim kurmasına yardımcı olur.
- **Environment variables:** Uygulama yapılandırmalarını ortam değişkenleri üzerinden yönetmeyi sağlar.
- **Volume:** Verilerin kalıcı olarak saklanmasına yardımcı olur.
- **Docker Compose:** Birden fazla servisin birlikte çalıştırılmasını kolaylaştırır.

**Uygulama**

PostgreSQL ve Adminer servislerini birlikte çalıştırma ve Adminer üzerinden veritabanına bağlanma mantığını inceledim.

### Hafta 2 — Kavrama Soruları

**1. Image ile container arasındaki fark nedir?**

Image, bir uygulamanın çalışması için gereken yapıyı tanımlayan şablon gibidir. Container ise bu şablondan oluşturulan çalışan örnektir.

**2. Volume kullanılmazsa veritabanı verileri kaybolabilir mi?**

Veriler yalnızca konteynerin kendi dosya sisteminde tutuluyorsa konteyner silindiğinde kaybolabilir. Volume, verileri konteynerden bağımsız saklamaya yardımcı olur.

**3. PostgreSQL'i doğrudan her bilgisayara kurmak yerine neden Docker kullanabiliriz?**

Docker, ekip üyelerinin aynı sürüm ve benzer yapılandırmalarla çalışmasını kolaylaştırır. Böylece ortam farklılıklarından kaynaklanan sorunlar azaltılabilir.

---

# FAZ 1 — C# VE .NET

## Hafta 3 — C# Temelleri

**Haftanın hedefi:** C# dilinin temel yapılarını öğrenmek ve konsol uygulamaları geliştirmek.

### Gün 7/48 — Neden .NET? İlk C# Programı

**Öğrendiklerim**

.NET'in C# uygulamalarını geliştirmek ve çalıştırmak için kullanılan bir platform olduğunu öğrendim.

Bir konsol projesi oluşturmak ve çalıştırmak için aşağıdaki komutları kullandım:

```bash
dotnet new console
dotnet run
```

Temel veri tiplerini inceledim:

| Tip | Kullanım |
|---|---|
| `int` | Tam sayılar |
| `decimal` | Hassas ondalıklı ve finansal hesaplamalar |
| `string` | Metin |
| `bool` | Doğru veya yanlış değerleri |
| `DateTime` | Tarih ve saat |

`.csproj` dosyasının projenin yapılandırmasını ve bağımlılıklarını tanımlamada kullanıldığını öğrendim.

**Uygulama**

Kullanıcıdan üç not alıp ortalamasını hesaplayan bir konsol uygulaması üzerinde çalıştım. Harf girilmesi gibi hatalı girişleri kontrol etmek için `int.TryParse` kullanımını öğrendim.

### Gün 8/48 — Kontrol Akışı ve Metotlar

**Öğrendiklerim**

Programın hangi koşulda hangi işlemi yapacağını belirlemek için kontrol yapılarını öğrendim.

```csharp
if
else
switch
for
while
foreach
```

Metotların belirli görevleri ayrı bölümlerde toplamaya yardımcı olduğunu öğrendim. Parametre, dönüş değeri ve anlamlı isimlendirme konularını inceledim.

**Uygulamalar**

- 1 ile 100 arasında sayı tahmin oyunu.
- `AsalMi(int sayi)` metodu.
- 1 ile 100 arasındaki asal sayıları listeleme.
- FizzBuzz problemi.

Bu alıştırmalarla koşulları, döngüleri ve metot kullanımını pekiştirdim.

### Gün 9/48 — Koleksiyonlar ve LINQ

**Öğrendiklerim**

Birden fazla veriyi birlikte tutmak ve işlemek için kullanılan koleksiyonları öğrendim.

- Dizi: Boyutu oluşturulurken belirlenir.
- `List<T>`: Eleman ekleme ve silme işlemlerine uygun dinamik koleksiyondur.
- `Dictionary<TKey, TValue>`: Anahtar-değer ilişkisiyle veri tutar.

LINQ metotlarını inceledim:

| Metot | Görevi |
|---|---|
| `Where` | Koşula göre filtreleme |
| `Select` | Veriyi seçme veya dönüştürme |
| `OrderBy` | Artan sıralama |
| `Count` | Eleman sayısını bulma |
| `FirstOrDefault` | İlk uygun sonucu veya varsayılan değeri alma |
| `GroupBy` | Verileri gruplama |
| `Average` | Ortalama hesaplama |

**Uygulama**

Öğrenci adı, sınıfı ve notundan oluşan bir liste üzerinde çalıştım.

```csharp
class Student
{
    public string Ad { get; set; } = "";
    public int Sinif { get; set; }
    public decimal Not { get; set; }
}
```

Notu 70'in üzerinde olan öğrencileri filtrelemek için:

```csharp
var notuYetmisUstu = ogrenciler
    .Where(o => o.Not > 70);
```

Sınıflara göre ortalama hesaplamak için `GroupBy` ve `Average` metotlarını kullandım. En yüksek notu alan öğrenciyi bulmak için `OrderByDescending` ve `FirstOrDefault` üzerinde çalıştım.

Sonuç bulunamayabileceği durumlarda `null` kontrolünün önemini öğrendim.

### Hafta 3 — Kavrama Soruları

**1. Para hesaplamalarında neden decimal kullanılır?**

`decimal`, ondalıklı finansal hesaplamalarda uygun hassasiyeti sağlamak için tercih edilir. `double` ise yaklaşık kayan noktalı hesaplamalarda sık kullanılır.

**2. Dizi ile List arasındaki fark nedir?**

Dizinin boyutu sabittir. `List<T>` ise ihtiyaç oldukça eleman eklemeye ve silmeye olanak sağlar.

**3. LINQ olmadan notu 70'in üzerinde olan öğrencileri nasıl bulurdum?**

Bir `foreach` döngüsüyle öğrencileri tek tek dolaşır, `if` ile notlarını kontrol eder ve koşulu sağlayanları ayrı bir listeye eklerdim.

---

## Hafta 4 — Nesne Yönelimli Programlama (OOP)

**Haftanın hedefi:** Sınıf, nesne, kapsülleme, interface ve repository kavramlarını öğrenerek StajDesk'in konsol sürümünü geliştirmek.

### Gün 10/48 — Sınıflar ve Nesneler

**Öğrendiklerim**

- **Sınıf:** Nesnelerin özelliklerini ve davranışlarını tanımlar.
- **Nesne:** Bir sınıftan oluşturulan örnektir.
- **Property:** Nesnenin bilgilerini temsil eder.
- **Constructor:** Nesne oluşturulurken çalışan özel metottur.
- **public:** Bir üyeye dışarıdan erişilmesine izin verir.
- **private:** Bir üyeye erişimi sınırlar.
- **Encapsulation:** Verilerin ve işlemlerin kontrollü şekilde yönetilmesini sağlar.

**Uygulama**

StajDesk'teki talepleri temsil etmek için `Ticket` sınıfı üzerinde çalıştım.

Sınıfta aşağıdaki bilgileri kullandım:

- `Id`
- `Title`
- `Description`
- `Status`
- `CreatedAt`

Constructor içerisinde başlığın boş veya yalnızca boşluklardan oluşmasını engelleme mantığını uyguladım.

### Gün 11/48 — Kalıtım, Interface, Enum ve Hata Yönetimi

**Öğrendiklerim**

Kalıtım, polimorfizm, interface, enum ve hata yönetimi kavramlarını inceledim.

`enum`, sabit durumları anlamlı isimlerle ifade etmeyi sağlar. `try/catch/finally` yapısı ise hata yönetimi ve gerekli temizlik işlemleri için kullanılır.

**Uygulama**

Talep durumlarını tanımlamak için aşağıdaki enum üzerinde çalıştım:

```csharp
enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}
```

Repository işlemlerinin hangi metotları içermesi gerektiğini belirlemek için `ITicketRepository` arayüzünü kullandım.

`InMemoryTicketRepository` ile talepleri bellekte tutma mantığını öğrendim. İstenen ID bulunamadığında anlamlı bir hata üretmenin önemini inceledim.

### Gün 12/48 — Async/Await ve Konsol Mini Talep Yöneticisi

**Öğrendiklerim**

Senkron ve asenkron çalışma arasındaki farkı öğrendim.

- `Task`: Asenkron bir işlemin sonucunu temsil edebilir.
- `async`: Bir metodun asenkron işlem akışını desteklemesini sağlar.
- `await`: Asenkron bir işlemin sonucunu beklerken akışın uygun şekilde devam etmesine olanak tanır.

**Uygulama**

StajDesk'in konsol sürümünde aşağıdaki işlemler üzerinde çalıştım:

```text
1 - Talep Ekle
2 - Talepleri Listele
3 - Talep Durumu Güncelle
4 - Talep Sil
5 - Çıkış
```

Kullanıcının seçimine göre ilgili işlemin yapılması, hatalı ID girişlerinin kontrol edilmesi ve repository metotlarının asenkron tasarlanması konularını inceledim.

### Hafta 4 — Kavrama Soruları

**1. Interface kullanmanın faydası nedir?**

`ITicketRepository`, talep işlemleri için gerekli metotları tanımlar. Bu sayede uygulamanın diğer bölümleri, verilerin tam olarak nasıl saklandığına bağımlı olmadan bu arayüz üzerinden çalışabilir.

**2. Kapsülleme neden önemlidir?**

Verilerin kontrolsüz değiştirilmesini önlemeye ve sınıfın belirlediği kuralları korumaya yardımcı olur.

**3. Async/await neden kullanılır?**

Özellikle ağ ve dosya işlemleri gibi bekleme gerektiren işlemlerin daha verimli yönetilmesine yardımcı olur. Asenkron çalışma, bekleme sırasında ilgili iş parçacığının gereksiz yere meşgul edilmesini azaltabilir.

---

# FAZ 2 — ASP.NET CORE WEB API

## Hafta 5 — ASP.NET Core Web API

**Haftanın hedefi:** Konsol uygulamasında öğrenilen yapıları HTTP üzerinden erişilebilen bir Web API'ye taşımak ve katmanlı mimariyi anlamak.

### Gün 13/48 — Web API, REST ve Swagger

**Öğrendiklerim**

Web API'nin uygulamalar arasında HTTP üzerinden veri alışverişi yapılmasını sağladığını öğrendim.

REST yaklaşımında kaynakların URL'lerle temsil edilmesini ve HTTP metotlarının bu kaynaklar üzerindeki işlemleri belirtmesini inceledim.

```text
GET     → Veri alma
POST    → Yeni kayıt oluşturma
PUT     → Kayıt güncelleme
DELETE  → Kayıt silme
```

Swagger arayüzünün API uç noktalarını görüntülemeye ve test etmeye yardımcı olduğunu öğrendim. OpenAPI'nin API'lerin tanımlanması için kullanılan bir standart olduğunu inceledim.

**Uygulama**

`StajDesk.Api` adlı ASP.NET Core Web API projesi üzerinde çalıştım.

`Program.cs` dosyasının servis yapılandırması ve uygulamanın başlangıç ayarları açısından görevini inceledim. `/api/health` uç noktasının sunucu durumunu ve mesajını döndürmesi üzerinde çalıştım.

### Gün 14/48 — Controller, Routing, Dependency Injection ve CRUD

**Öğrendiklerim**

- **Controller:** HTTP isteklerini karşılar ve uygun yanıtları döndürür.
- **Routing:** İsteklerin hangi uç noktaya yönlendirileceğini belirler.
- **Model binding:** İstek verilerini parametrelere ve nesnelere aktarır.
- **Dependency Injection (DI):** Sınıfların ihtiyaç duyduğu bağımlılıkların dışarıdan sağlanmasını kolaylaştırır.
- **IActionResult:** Controller üzerinden farklı HTTP yanıtlarının döndürülmesine yardımcı olur.

DI kayıtlarında kullanılan yaşam sürelerini de inceledim:

- `Transient`: Her istendiğinde yeni örnek oluşturulur.
- `Scoped`: Aynı kapsam içerisinde aynı örnek kullanılır.
- `Singleton`: Uygulama boyunca aynı örnek kullanılır.

**Uygulama**

`TicketsController` üzerinden temel CRUD işlemleri üzerinde çalıştım.

| Metot | Uç nokta | Amaç |
|---|---|---|
| GET | `/api/Tickets` | Talepleri listelemek |
| GET | `/api/Tickets/{id}` | Tek talep getirmek |
| POST | `/api/Tickets` | Yeni talep oluşturmak |
| PUT | `/api/Tickets/{id}` | Talebi güncellemek |
| DELETE | `/api/Tickets/{id}` | Talebi silmek |

Repository ve Service bağımlılıklarının Dependency Injection ile kaydedilmesi mantığını öğrendim.

Ayrıca aşağıdaki HTTP yanıtlarını inceledim:

| Kod | Anlamı |
|---|---|
| 200 OK | İstek başarılı |
| 201 Created | Yeni kaynak oluşturuldu |
| 204 No Content | İşlem başarılı, yanıt gövdesi yok |
| 404 Not Found | İstenen kaynak bulunamadı |

Bellekte tutulan verilerin uygulama yeniden başlatıldığında kaybolabileceğini ve kalıcı saklama için veritabanına ihtiyaç duyulabileceğini öğrendim.

### Gün 15/48 — Katmanlı Mimari ve DTO

**Öğrendiklerim**

Uygulamanın sorumluluklarını farklı katmanlara ayırmanın kodun okunabilirliğini, bakımını ve test edilebilirliğini kolaylaştırdığını öğrendim.

- **Controller:** HTTP isteklerini ve yanıtlarını yönetir.
- **Service:** İş kurallarını ve uygulama akışını yönetir.
- **Repository:** Verilerin saklanması ve erişimiyle ilgili işlemleri yürütür.

DTO'nun (Data Transfer Object), API'ye gönderilen veya API'den döndürülen verileri taşımak için kullanılan bir nesne olduğunu öğrendim.

**Uygulama**

StajDesk'te aşağıdaki yapıların görevlerini inceledim:

- `ITicketRepository`: Veri işlemleri için sözleşme.
- `InMemoryTicketRepository`: Verileri bellekte tutan repository uygulaması.
- `ITicketService`: İş mantığı için sözleşme.
- `TicketService`: Talep işlemlerini yöneten servis.
- `CreateTicketRequest`: Yeni talep oluşturma isteği.
- `UpdateTicketRequest`: Talep güncelleme isteği.
- `TicketResponse`: API yanıtında döndürülen talep bilgileri.

Controller, Service ve Repository arasındaki veri akışını ve DTO ile model arasındaki dönüşüm mantığını inceledim.

### Hafta 5 — Kavrama Soruları

**1. In-memory veriler uygulama yeniden başlatıldığında neden kaybolabilir?**

Veriler yalnızca bellekte tutulduğu için uygulama kapandığında bellekteki veriler kalıcı olarak saklanmaz. Kalıcı saklama için bir veritabanı veya uygun bir kalıcı depolama çözümü gerekir.

**2. Controller, Service ve Repository neden ayrı tutulur?**

Her katmanın farklı bir sorumluluğu vardır. Controller HTTP iletişimini, Service iş kurallarını, Repository ise veri işlemlerini yönetir. Bu ayrım kodun bakımını ve test edilmesini kolaylaştırır.

**3. Yeni kayıt oluşturulunca neden 201 Created döndürülür?**

201 Created, isteğin başarılı olduğunu ve yeni bir kaynağın oluşturulduğunu belirtir. Böylece istemciye yapılan işlem hakkında daha doğru bilgi verilir.

**4. DTO kullanmanın faydası nedir?**

DTO, API üzerinden hangi verilerin alınacağını ve hangi verilerin döndürüleceğini belirlemeye yardımcı olur. Böylece dışarıya sunulan veri yapısı, uygulamanın iç modellerinden ayrılabilir.

---

# GENEL DEĞERLENDİRME

İlk beş haftada Git, web iletişimi, Docker, C# temelleri, LINQ, nesne yönelimli programlama ve ASP.NET Core Web API konularını ele aldım.

Öğrendiğim kavramların StajDesk projesindeki bağlantısını şu şekilde özetleyebilirim:

```text
Git ve Geliştirme Araçları
          |
          v
     C# / .NET
          |
          v
 Koleksiyonlar ve LINQ
          |
          v
 Sınıflar ve Nesneler
          |
          v
       Interface
          |
          v
      Repository
          |
          v
        Service
          |
          v
      Controller
          |
          v
      Web API
          |
          v
     CRUD İşlemleri
          |
          v
       StajDesk
```

Bu süreçte amacım yalnızca çalışan kod yazmak değil, yazdığım kodun neden o şekilde tasarlandığını anlamak ve karşılaştığım sorunları kendi başıma çözme becerimi geliştirmektir.

Bundan sonraki süreçte öğrendiğim konuları uygulamalarla pekiştirmeyi, kodumu daha okunabilir hâle getirmeyi ve mentor geri bildirimlerini dikkate alarak geliştirmeler yapmayı hedefliyorum.

---

**Not:** Bu günlük, öğrenme sürecimi ve üzerinde çalıştığım konuları takip etmek için hazırlanmıştır. Uygulama sonuçları, testler ve mentor geri bildirimleri ilerledikçe güncellenecektir.