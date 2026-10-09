# StajDesk - Öğrenim Günlüğü

Bu dosya, staj süresince öğrendiğim konuları, yaptığım uygulamaları ve günlük notlarımı içermektedir.

---

# FAZ 0 · TEMELLER

## HAFTA 1 · Başlangıç, Git ve Web'in Çalışma Mantığı

### Gün 1/48 · Tanışma ve Ortam Kurulumu

**Tarih:** ____ / ____

**Öğrendiklerim**

Stajın amacı, StajDesk projesinin genel yapısı ve çalışma planı hakkında bilgi edindim. Yazılım ekiplerinde görev takibi için Jira, sürüm kontrolü için Git ve kod incelemesi kullanıldığını öğrendim.

Geliştirme araçlarının görevlerini öğrendim:

- VS Code: Kod yazmak için kullanılan geliştirme ortamı.
- Git: Kod değişikliklerini takip eden sürüm kontrol sistemi.
- .NET SDK: C# ve .NET uygulamalarını geliştirmek için kullanılır.
- Node.js: JavaScript tabanlı uygulama ve araçları çalıştırır.
- Docker Desktop: Konteynerleri çalıştırmak ve yönetmek için kullanılır.

**Uygulamalar**

```bash
dotnet --version
node -v
git --version
docker --version
```

StajDesk klasörünü oluşturdum ve öğrendiklerimi kaydetmek için `LOG.md` dosyasını kullanmaya başladım.

### Gün 2/48 · Terminal ve Git Temelleri

**Öğrendiklerim**

Terminalde klasörler arasında geçiş yapmayı ve dosyaları görüntülemeyi öğrendim.

```bash
cd
dir
ls
mkdir
```

`cd` klasör değiştirmek, `dir` ve `ls` dosyaları listelemek, `mkdir` ise yeni klasör oluşturmak için kullanılır.

Git kavramlarını öğrendim:

- Repository: Projenin ve Git geçmişinin tutulduğu depo.
- Commit: Değişiklikleri yerel Git geçmişine kaydetme.
- Branch: Farklı bir çalışma alanı oluşturma.
- Merge: Branch değişikliklerini birleştirme.
- Remote: Uzak Git deposu.
- Push: Commitleri uzak depoya gönderme.

README.md dosyasının projeyi tanıtmak için kullanıldığını öğrendim. Branch oluşturma, değişiklik yapma ve birleştirme işlemleri üzerinde çalıştım.

### Gün 3/48 · Web, HTTP ve JSON

**Öğrendiklerim**

Web uygulamalarında istemci ve sunucu arasında iletişim kurulduğunu öğrendim. İstemci istek gönderir, sunucu ise bu isteği değerlendirerek yanıt verir.

HTTP metotları:

```text
GET     → Veri almak
POST    → Veri oluşturmak veya göndermek
PUT     → Veri güncellemek
DELETE  → Veri silmek
```

Temel HTTP durum kodları:

```text
200 → Başarılı
201 → Oluşturuldu
400 → Hatalı istek
401 → Yetkisiz erişim
404 → Kaynak bulunamadı
500 → Sunucu hatası
```

JSON'ın uygulamalar arasında veri alışverişi yapmak için kullanıldığını öğrendim. Tarayıcının Network sekmesinden HTTP isteklerini inceledim ve Postman veya Bruno ile GET ve POST istekleri üzerinde çalıştım.

**Kavrama Soruları**

**1. Tarayıcıya adres yazıldığında ne olur?**

Tarayıcı istenen kaynağa ulaşmaya çalışır. Sunucuya HTTP isteği gönderilir, sunucu yanıt verir ve tarayıcı gelen veriyi işleyerek sayfayı gösterir.

**2. Commit ile push arasındaki fark nedir?**

Commit değişiklikleri yerel Git geçmişine kaydeder. Push ise bu commitleri uzak depoya gönderir.

**3. 404 ile 500 arasındaki fark nedir?**

404 istenen kaynağın bulunamadığını, 500 ise sunucu tarafında bir hata oluştuğunu belirtir.

---

# HAFTA 2 · Docker ile Tanışma

### Gün 4/48 · Konteyner Nedir, Neden Docker?

**Öğrendiklerim**

Docker'ın uygulamaların farklı bilgisayarlarda daha tutarlı bir ortamda çalışmasına yardımcı olduğunu öğrendim.

- Image: Konteyner oluşturmak için kullanılan şablon.
- Container: Image üzerinden oluşturulan çalışan örnek.
- Port mapping: Konteyner portuna bilgisayar üzerinden erişilmesini sağlar.

Temel komutlar:

```bash
docker run hello-world
docker ps
docker logs <container_id>
docker stop <container_id>
docker rm <container_id>
docker run -d -p 8080:80 nginx
```

Bu komutlarla konteyner çalıştırma, listeleme, logları inceleme, durdurma ve silme işlemlerini öğrendim.

### Gün 5/48 · Dockerfile Yazmak

**Öğrendiklerim**

Dockerfile, bir Docker image'ının nasıl oluşturulacağını tarif eder.

Temel komutlar:

```dockerfile
FROM
WORKDIR
COPY
RUN
EXPOSE
CMD
```

Image katmanlarının ve build önbelleğinin çalışma mantığını öğrendim. Volume kullanarak verilerin konteynerin yaşam döngüsünden bağımsız saklanabileceğini öğrendim.

**Uygulama**

Basit bir HTML sayfasını nginx üzerinden çalıştırma ve Dockerfile ile paketleme mantığı üzerinde çalıştım.

```bash
docker build -t stajdesk-web .
docker run -d -p 8080:80 stajdesk-web
```

### Gün 6/48 · Docker Compose ve Proje Mimarisi

**Öğrendiklerim**

Docker Compose ile birden fazla servisin tek yapılandırma üzerinden yönetilebildiğini öğrendim. Network, environment variable ve volume kavramlarını inceledim.

**Uygulama**

PostgreSQL ve Adminer servislerini Compose üzerinden birlikte çalıştırma ve Adminer ile veritabanına bağlanma mantığını öğrendim.

**Kavrama Soruları**

**1. Image ile container arasındaki fark nedir?**

Image bir şablon, container ise bu şablondan oluşturulan çalışan örnektir.

**2. Volume kullanılmazsa veritabanı verileri kaybolabilir mi?**

Veriler yalnızca konteynerin kendi dosya sisteminde tutuluyorsa konteyner silindiğinde kaybolabilir. Volume, verilerin kalıcı saklanmasına yardımcı olur.

**3. PostgreSQL için neden Docker kullanılabilir?**

Geliştiricilerin benzer sürüm ve yapılandırmalarla çalışmasını sağlayarak ortam farklılıklarını azaltır.

---

# FAZ 1 · C# VE .NET

## HAFTA 3 · C# Temelleri

### Gün 7/48 · Neden .NET? İlk C# Programı

**Öğrendiklerim**

.NET'in C# uygulamaları geliştirmek için kullanılan bir platform olduğunu öğrendim. Proje oluşturma ve çalıştırma komutlarını kullandım.

```bash
dotnet new console
dotnet run
```

Temel veri tipleri:

```text
int       → Tam sayı
decimal   → Ondalıklı ve finansal değer
string    → Metin
bool      → Doğru veya yanlış
DateTime  → Tarih ve saat
```

`.csproj` dosyasının proje yapılandırmasını içerdiğini öğrendim.

**Uygulama**

Kullanıcıdan üç not alan, ortalamayı hesaplayan ve harf notunu belirleyen bir konsol uygulaması üzerinde çalıştım. Hatalı sayısal girişleri kontrol etmek için `int.TryParse` kullanımını öğrendim.

### Gün 8/48 · Kontrol Akışı ve Metotlar

**Öğrendiklerim**

Programın akışını kontrol etmek için kullanılan yapıları öğrendim:

```text
if / else
switch
for
while
foreach
```

Metotların belirli görevleri ayrı bölümlerde gerçekleştirmeye yardımcı olduğunu öğrendim. Parametre, dönüş değeri ve anlamlı isim kullanımı üzerinde çalıştım.

**Uygulamalar**

- Sayı tahmin oyunu.
- `AsalMi(int sayi)` metodu.
- 1 ile 100 arasındaki asal sayıları listeleme.
- FizzBuzz problemi.

### Gün 9/48 · Koleksiyonlar ve LINQ

**Öğrendiklerim**

Birden fazla veriyi bir arada tutan koleksiyonları öğrendim.

```csharp
string[] isimler = { "Ahmet", "Mehmet", "Ayşe" };

List<int> notlar = new List<int>();
```

Dizilerin sabit boyutlu, `List<T>` yapısının ise dinamik olduğunu öğrendim. `Dictionary<TKey, TValue>` yapısının anahtar-değer mantığıyla çalıştığını gördüm.

LINQ metotları:

```text
Where              → Filtreleme
Select             → Veri seçme
OrderBy            → Artan sıralama
OrderByDescending  → Azalan sıralama
Count              → Sayma
FirstOrDefault     → İlk elemanı alma
GroupBy            → Gruplama
Average            → Ortalama hesaplama
```

**Uygulama**

Öğrenci adı, sınıfı ve notunu içeren bir liste üzerinde çalıştım.

```csharp
class Student
{
    public string Ad { get; set; } = "";
    public int Sinif { get; set; }
    public decimal Not { get; set; }
}
```

Notu 70'in üzerinde olan öğrencileri filtreledim:

```csharp
var notuYetmisUstu = ogrenciler
    .Where(o => o.Not > 70);
```

Sınıflara göre ortalama hesapladım:

```csharp
var sinifOrtalamalari =
    ogrenciler.GroupBy(o => o.Sinif);

foreach (var grup in sinifOrtalamalari)
{
    decimal ortalama = grup.Average(o => o.Not);
    Console.WriteLine($"{grup.Key}. sınıf: {ortalama:F2}");
}
```

En yüksek not alan öğrenciyi bulmak için `OrderByDescending` ve `FirstOrDefault` kullandım. Sonucun boş olabileceğini düşünerek `null` kontrolü yaptım.

**Kavrama Soruları**

**1. Para hesaplamalarında neden decimal kullanılır?**

`decimal`, finansal işlemlerde ondalıklı değerlerle çalışmak için uygun bir veri tipidir. `double` ise genel ve bilimsel hesaplamalarda sık kullanılır.

**2. Dizi ile List arasındaki fark nedir?**

Dizinin boyutu sabittir. `List<T>` ise eleman ekleme ve silme işlemlerine uygun, dinamik bir koleksiyondur.

**3. LINQ olmadan filtreleme nasıl yapılır?**

Öğrencileri `foreach` ile dolaşır, `if` ile notlarını kontrol eder ve şartı sağlayanları başka bir listeye eklerdim.

---

# HAFTA 4 · Nesne Yönelimli Programlama (OOP)

Haftanın hedefi; sınıf, nesne, interface ve hata yönetimi kavramlarını öğrenerek StajDesk'in konsol sürümünü geliştirmektir.

### Gün 10/48 · Sınıflar ve Nesneler

**Öğrendiklerim**

- Sınıf: Nesnelerin özelliklerini ve davranışlarını tanımlar.
- Nesne: Bir sınıftan oluşturulan örnektir.
- Property: Nesnenin bilgilerini temsil eder.
- Constructor: Nesne oluşturulurken çalışan özel metottur.
- `public`: Dışarıdan erişime izin verir.
- `private`: Erişimi sınıf içiyle sınırlar.

Kapsülleme ile verilerin kontrollü değiştirilmesini öğrendim.

**Uygulama**

StajDesk için `Ticket` sınıfı oluşturdum:

```csharp
class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Ticket(int id, string title, string description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Başlık boş olamaz.");
        }

        Id = id;
        Title = title;
        Description = description;
        Status = TicketStatus.Open;
        CreatedAt = DateTime.Now;
    }
}
```

Constructor içerisinde başlığın boş olmasını engelledim.

### Gün 11/48 · Kalıtım, Interface, Enum ve Hata Yönetimi

**Öğrendiklerim**

Kalıtım, polimorfizm, interface, enum ve `try/catch/finally` kavramlarını öğrendim.

Interface'in sınıfların uygulaması gereken işlemleri belirleyen bir sözleşme olduğunu öğrendim.

**Uygulama**

Talep durumlarını tanımladım:

```csharp
enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}
```

Repository işlemleri için `ITicketRepository` arayüzünü oluşturdum:

```csharp
interface ITicketRepository
{
    Task AddAsync(Ticket ticket);
    Task<List<Ticket>> GetAllAsync();
    Task<Ticket> GetByIdAsync(int id);
    Task UpdateAsync(Ticket ticket);
    Task DeleteAsync(int id);
}
```

`InMemoryTicketRepository` sınıfıyla talepleri başlangıçta bellekte tuttum. Olmayan bir kayıt istendiğinde anlamlı hata döndürme mantığını öğrendim.

### Gün 12/48 · Async/Await ve Konsol Mini Talep Yöneticisi

**Öğrendiklerim**

Senkron ve asenkron çalışma arasındaki farkı öğrendim. .NET içerisinde `Task`, `async` ve `await` yapılarının asenkron işlemlerde kullanıldığını gördüm.

**Uygulama**

StajDesk için aşağıdaki menüyü geliştirdim:

```text
1 - Talep Ekle
2 - Talepleri Listele
3 - Talep Durumu Güncelle
4 - Talep Sil
5 - Çıkış
```

Kullanıcının seçimine göre ilgili repository işlemlerini gerçekleştirdim. Hatalı ID girişlerinde kullanıcıya anlamlı mesaj gösterme üzerinde çalıştım.

Cuma günü mentor kod incelemesi yapıldı ve alınan geri bildirimlere göre düzenlemeler gerçekleştirildi.

**Kavrama Soruları**

**1. Interface kullanmanın faydası nedir?**

`ITicketRepository`, talep işlemlerinin hangi metotlara sahip olması gerektiğini belirler. Böylece kullanan kod, verilerin nasıl saklandığından daha bağımsız olur.

**2. Kapsülleme neden önemlidir?**

Verilerin kontrolsüz değiştirilmesini önlemeye ve sınıfın kurallarını korumaya yardımcı olur.

**3. Async/await neden kullanılır?**

Bekleme gerektiren işlemlerin daha verimli yönetilmesine yardımcı olur. Özellikle web uygulamalarında gereksiz thread beklemesini azaltabilir.

### Hafta 4 Genel Değerlendirme

Bu hafta sınıf, nesne, property, constructor, interface, enum, repository ve asenkron işlem kavramları üzerinde çalıştım. StajDesk'in konsol sürümünde talep ekleme, listeleme, güncelleme ve silme işlemlerini uyguladım.

---

# FAZ 2 · ASP.NET CORE WEB API

## HAFTA 5 · ASP.NET Core Web API

Haftanın hedefi; HTTP ve REST mantığını kullanarak StajDesk için Web API oluşturmak, katmanları ayırmak ve CRUD işlemlerini HTTP durum kodlarıyla uygulamaktır.

### Gün 13/48 · Web API, REST ve Swagger

**Öğrendiklerim**

ASP.NET Core Web API'nin HTTP isteklerini karşılayarak uygun yanıtlar döndürmek için kullanıldığını öğrendim.

```text
GET     → Veri listeleme
POST    → Yeni kayıt oluşturma
PUT     → Var olan kaydı güncelleme
DELETE  → Kaydı silme
```

Swagger'ın API uç noktalarını görüntülemeye ve istekleri test etmeye yardımcı olduğunu öğrendim. OpenAPI'nin API yapısını tanımlamak için kullanılan bir standart olduğunu gördüm.

**Uygulamalar**

`StajDesk.Api` adında bir ASP.NET Core Web API projesi üzerinde çalıştım. `Program.cs` dosyasındaki servis yapılandırmasını inceledim.

API'nin çalıştığını kontrol etmek için `/api/health` uç noktası üzerinde çalıştım. Bu uç noktanın durum bilgisi ve mesaj içeren yanıt döndürmesini sağlama mantığını öğrendim.

Swagger üzerinden API uç noktalarını görüntüledim ve istekleri denedim.

### Gün 14/48 · Controller, Routing, Dependency Injection ve CRUD

**Öğrendiklerim**

- Controller, HTTP isteklerini karşılar ve yanıtları döndürür.
- Routing, isteğin hangi metoda yönlendirileceğini belirler.
- Model binding, istek verilerini C# nesnelerine aktarır.
- Dependency Injection, sınıfların ihtiyaç duydukları bağımlılıkları dışarıdan almasını sağlar.
- `IActionResult`, farklı HTTP yanıtlarının döndürülmesine yardımcı olur.

Temel HTTP yanıtları:

```text
200 OK          → İstek başarılı
201 Created     → Yeni kayıt oluşturuldu
204 No Content  → İşlem başarılı, gövde yok
404 Not Found   → Kayıt bulunamadı
```

**Uygulama**

`Ticket` modeli ve `TicketsController` üzerinde çalıştım.

```text
GET     /api/Tickets       → Talepleri listeleme
POST    /api/Tickets       → Yeni talep oluşturma
PUT     /api/Tickets/{id}  → Talebi güncelleme
DELETE  /api/Tickets/{id}  → Talebi silme
```

Verileri `InMemoryTicketRepository` içerisinde bir listeyle tuttum. Repository ve Service bağımlılıklarını `Program.cs` üzerinden Dependency Injection ile kaydetme mantığını öğrendim.

Swagger üzerinden GET, POST, PUT ve DELETE isteklerini test ettim. Oluşturma işleminde `201 Created` yanıtının kullanılmasını öğrendim.

Bellekte tutulan verilerin uygulama yeniden başlatıldığında kaybolabileceğini ve kalıcı veri için veritabanı gerektiğini not ettim.

### Gün 15/48 · Katmanlı Mimari ve DTO

**Öğrendiklerim**

Uygulamadaki sorumlulukları ayrı katmanlara bölmenin kodun okunabilirliğini ve test edilebilirliğini artırdığını öğrendim.

- Controller: HTTP istekleri ve yanıtları.
- Service: İş akışı ve iş kuralları.
- Repository: Veri erişimi ve veri işlemleri.

DTO (Data Transfer Object) kullanarak API'ye gelen ve API'den dönen verileri model sınıflarından ayrı tanımlayabildiğimi öğrendim.

**Uygulama**

`ITicketRepository` ve `InMemoryTicketRepository` ile veri işlemlerini Repository katmanında topladım.

`ITicketService` ve `TicketService` ile Controller ve Repository arasına Service katmanını ekledim.

İstek ve yanıt verilerini ayırmak için aşağıdaki DTO yapıları üzerinde çalıştım:

```text
CreateTicketRequest  → Yeni talep oluşturma isteği
UpdateTicketRequest  → Talep güncelleme isteği
TicketResponse       → API yanıtında döndürülen talep bilgileri
```

Controller içerisinde DTO ve model arasında veri aktarımı mantığını uyguladım.

**Kontrol Edilecekler**

- Postman koleksiyonunu hazırlamak.
- Olmayan ID ile PUT ve DELETE isteklerinde `404 Not Found` yanıtını test etmek.
- Mentorun kavrama sorularını kendi cümlelerimle cevaplamak.
- Build ve test sonuçlarını kontrol etmek.
- Git durumunu kontrol etmek.

### Kavrama Soruları

**1. In-memory veriler uygulama yeniden başlatıldığında neden kaybolabilir?**

Veriler yalnızca bellekte tutulduğu için uygulama kapandığında bellekteki liste sıfırlanabilir. Kalıcı saklama için veritabanı gerekir.

**2. Controller, Service ve Repository neden ayrı tutulur?**

Her katman farklı bir sorumluluk üstlenir. Controller HTTP işlemleriyle, Service iş akışıyla, Repository ise veri işlemleriyle ilgilenir.

**3. Yeni kayıt oluşturulunca neden 201 Created döndürülür?**

Bu yanıt, isteğin başarılı olduğunu ve yeni bir kaydın oluşturulduğunu belirtir.

**4. DTO kullanmanın faydası nedir?**

API'ye hangi verilerin gönderileceğini ve hangi bilgilerin yanıt olarak döndürüleceğini belirlemeye yardımcı olur. Böylece veri alışverişi model sınıfından ayrılabilir.

### Hafta 5 Genel Değerlendirme

Bu hafta ASP.NET Core Web API, HTTP metotları, REST yaklaşımı, Swagger, Controller, Routing, Dependency Injection, CRUD işlemleri, katmanlı mimari ve DTO kavramları üzerinde çalıştım.

StajDesk'i konsol uygulamasından HTTP istekleriyle kullanılabilen bir API yapısına taşımaya başladım. API uç noktalarını test etmenin ve uygun HTTP durum kodlarını kullanmanın önemini öğrendim.

---

# GENEL DEĞERLENDİRME

İlk beş haftada geliştirme ortamı, Git, web iletişimi, Docker, C# temelleri, LINQ, nesne yönelimli programlama ve ASP.NET Core Web API konularında çalıştım.

Öğrendiğim konuların StajDesk projesindeki bağlantısını şu şekilde özetleyebilirim:

```text
Git
 ↓
C# / .NET
 ↓
Koleksiyonlar ve LINQ
 ↓
Sınıflar ve Nesneler
 ↓
Interface
 ↓
Repository
 ↓
Service
 ↓
Controller
 ↓
HTTP ve Web API
 ↓
CRUD İşlemleri
 ↓
StajDesk
```

Staj sürecinde yalnızca kod yazmayı değil, kodun neden belirli katmanlara ayrıldığını, verilerin nasıl yönetildiğini ve uygulamaların nasıl test edildiğini de öğrenmeyi hedefliyorum.

Bundan sonraki süreçte öğrendiğim konuları uygulayarak pekiştirmek, kod kalitesini artırmak ve mentor geri bildirimlerine göre geliştirmeler yapmak istiyorum.
