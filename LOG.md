# StajDesk - Öğrenim Günlüğü

Bu dosya, staj süresince öğrendiğim konuları, yaptığım uygulamaları ve günlük notlarımı içermektedir.

---

# FAZ 0 · TEMELLER

## HAFTA 1 · Başlangıç, Git ve Web'in Çalışma Mantığı

### Gün 1/48 · Tanışma ve Ortam Kurulumu

**Tarih:** ____ / ____

### Öğrendiklerim

Stajın amacı, StajDesk projesinin genel yapısı ve 16 haftalık çalışma planı hakkında bilgi edindim.

Bir yazılım ekibinin birlikte çalışırken kullandığı bazı araçları öğrendim:

- Jira: Görevlerin takip edilmesi ve planlanması için kullanılır.
- Git: Kodların sürümlerini takip etmek için kullanılır.
- Kod incelemesi: Yazılan kodun ekip içerisindeki diğer geliştiriciler tarafından kontrol edilmesini sağlar.

Ayrıca geliştirme sırasında kullanılacak araçların görevlerini öğrendim:

- VS Code: Kod yazmak için kullandığım geliştirme ortamı.
- Git: Sürüm kontrol sistemi.
- .NET SDK: C# ve .NET uygulamalarını geliştirmek ve çalıştırmak için kullanılır.
- Node.js: JavaScript tabanlı araçları ve uygulamaları çalıştırmak için kullanılır.
- Docker Desktop: Konteyner tabanlı uygulamaları çalıştırmak ve yönetmek için kullanılır.

### Uygulamalar

Kullandığım araçların kurulu olup olmadığını kontrol etmek için aşağıdaki komutları öğrendim:

```bash
dotnet --version
node -v
git --version
docker --version
```

### Not

Masaüstünde `StajDesk` klasörü oluşturuldu ve öğrenme günlüğü olarak `LOG.md` dosyası kullanılmaya başlandı.

---

## Gün 2/48 · Terminal ve Git Temelleri

**Tarih:** ____ / ____

### Öğrendiklerim

Terminal üzerinden klasörler arasında geçiş yapmayı ve dosyaların bulunduğu konumları anlamayı öğrendim.

Temel terminal komutları:

```bash
cd
ls
dir
mkdir
```

`cd` klasör değiştirmek için, `dir` ve `ls` bulunduğum klasördeki dosya ve klasörleri görmek için, `mkdir` ise yeni klasör oluşturmak için kullanılır.

Dosya yollarında iki farklı yaklaşım olduğunu öğrendim:

- Mutlak yol: Dosyanın tam konumunu belirtir.
- Göreli yol: Bulunduğum klasöre göre dosyanın konumunu belirtir.

### Git

Git'in yazılım projelerinde sürüm kontrolü için kullanıldığını öğrendim.

Öğrendiğim temel kavramlar:

- Repository (repo): Projenin ve Git geçmişinin tutulduğu alan.
- Commit: Yapılan değişikliklerin Git geçmişine kaydedilmesi.
- Branch: Ana koddan ayrılarak farklı bir çalışma alanı oluşturulması.
- Merge: Bir branch'teki değişikliklerin başka bir branch ile birleştirilmesi.
- Remote: Uzak Git deposu.

İyi bir commit mesajının yapılan değişikliği açık ve anlaşılır şekilde ifade etmesi gerektiğini öğrendim.

### Uygulamalar

StajDesk deposunun oluşturulması ve bilgisayara klonlanması üzerinde çalıştım.

README.md dosyasının amacı ve projeyi tanıtmak için nasıl kullanılabileceğini öğrendim.

Ayrıca branch oluşturma, değişiklik yapma ve merge işlemlerini öğrendim.

### Not

Commit geçmişinin düzenli tutulmasının proje takibi açısından önemli olduğunu öğrendim.

---

## Gün 3/48 · Web Nasıl Çalışır? HTTP ve JSON

**Tarih:** ____ / ____

### Öğrendiklerim

Bir web uygulamasında istemci ve sunucu arasında iletişim olduğunu öğrendim.

- İstemci: İsteği yapan taraftır.
- Sunucu: İsteği karşılayan ve cevap veren taraftır.

URL yapısının web üzerindeki kaynağın adresini belirtmek için kullanıldığını öğrendim.

DNS'in alan adlarının ilgili ağ adresleriyle eşleşmesini sağlayan yapıyla ilişkili olduğunu öğrendim.

### HTTP Metotları

Temel HTTP metotlarını öğrendim:

```text
GET     → Veri almak
POST    → Veri göndermek/oluşturmak
PUT     → Veri güncellemek
DELETE  → Veri silmek
```

### Durum Kodları

Bazı HTTP durum kodlarının anlamlarını öğrendim:

```text
200 → Başarılı
201 → Oluşturuldu
400 → Hatalı istek
401 → Yetkisiz erişim
404 → Bulunamadı
500 → Sunucu hatası
```

### JSON

JSON'ın veri alışverişinde kullanılan bir veri formatı olduğunu öğrendim.

Web isteklerini tarayıcının geliştirici araçlarındaki **Network** sekmesinden inceleyebileceğimi öğrendim.

### Uygulamalar

Farklı sitelerin Network sekmesindeki istekleri inceleyerek HTTP metodunu, adresini ve durum kodunu gözlemledim.

Postman veya Bruno kullanarak `jsonplaceholder.typicode.com` üzerinde GET ve POST istekleri göndermeyi öğrendim.

### Kavrama Soruları

**1. Tarayıcıya bir adres yazıp Enter'a bastığınızda neler olur?**

Tarayıcı önce yazdığım adresi kullanarak istenen web kaynağına ulaşmaya çalışır. İstemci ve sunucu arasında bir HTTP isteği oluşur. Sunucu bu isteğe bir yanıt gönderir ve tarayıcı gelen yanıtı işleyerek sayfayı kullanıcıya gösterir.

**2. Commit ile push arasındaki fark nedir?**

Commit, yaptığım değişikliği kendi bilgisayarımdaki Git geçmişine kaydetmektir. Push ise bu commitleri uzak Git deposuna göndermektir.

**3. 404 ile 500 arasındaki fark nedir?**

404, istenen kaynağın bulunamadığını gösterir. 500 ise sunucu tarafında bir hata oluştuğunu gösterir. 404 genellikle istenen kaynak veya adresle ilgiliyken, 500 sunucu tarafındaki problemle ilgilidir.

---

# HAFTA 2 · Docker ile Tanışma

## Gün 4/48 · Konteyner Nedir, Neden Docker?

**Tarih:** ____ / ____

### Öğrendiklerim

Docker'ın temel amacının uygulamanın çalıştığı ortamı daha taşınabilir ve tutarlı hale getirmek olduğunu öğrendim.

"Benim bilgisayarımda çalışıyordu" probleminin farklı bilgisayarlardaki ortam ve bağımlılık farklılıklarından kaynaklanabileceğini öğrendim.

### Image ve Container

Image ile container arasındaki farkı öğrendim.

- Image: Uygulamanın çalışması için kullanılacak hazır paket/şablondur.
- Container: Bu image'ın çalıştırılan örneğidir.

### Port Mapping

Konteyner içindeki bir portun bilgisayardaki farklı bir port üzerinden erişilebilir hale getirilebileceğini öğrendim.

### Docker Komutları

Aşağıdaki komutları öğrendim:

```bash
docker run
docker ps
docker logs
docker stop
docker rm
```

`docker run` ile konteyner çalıştırılabilir, `docker ps` ile çalışan konteynerler görülebilir. `docker logs` konteyner çıktılarının incelenmesinde, `docker stop` durdurmada ve `docker rm` silmede kullanılır.

### Uygulamalar

```bash
docker run hello-world
```

komutunu kullanarak Docker'ın temel çalışma mantığını gözlemledim.

Ayrıca:

```bash
docker run -d -p 8080:80 nginx
```

komutu ile nginx tabanlı bir web sunucusu çalıştırmayı öğrendim.

---

## Gün 5/48 · Dockerfile Yazmak

**Tarih:** ____ / ____

### Öğrendiklerim

Dockerfile'ın bir Docker image'ının nasıl oluşturulacağını tarif eden dosya olduğunu öğrendim.

Temel Dockerfile komutları:

```text
FROM
WORKDIR
COPY
RUN
EXPOSE
CMD
```

Bu komutların image oluşturma sürecindeki görevlerini öğrendim.

Ayrıca image'ların katmanlardan oluştuğunu ve Docker'ın build işlemi sırasında önbellekten yararlanabildiğini öğrendim.

### Volume

Volume kavramını öğrendim.

Konteyner silindiğinde konteynerin kendi dosya sistemi içerisindeki verilerin kaybolabileceğini, kalıcı veriler için volume kullanılabileceğini öğrendim.

### Uygulama

Kendimi tanıtan basit bir HTML sayfası hazırlayıp nginx tabanlı bir Dockerfile ile paketleme ve çalıştırma işlemi üzerinde çalıştım.

Temel olarak:

```bash
docker build
docker run
```

komutlarını öğrendim.

---

## Gün 6/48 · Docker Compose ve Proje Mimarisi

**Tarih:** ____ / ____

### Öğrendiklerim

Docker Compose ile birden fazla servisin birlikte yönetilebileceğini öğrendim.

Servislerin birbirleriyle iletişim kurabilmesi için network yapısının kullanılabildiğini öğrendim.

Ayrıca ortam değişkenleri ve volume kavramlarını Docker Compose içerisinde kullanmayı öğrendim.

### Uygulama

PostgreSQL ve Adminer servislerini compose dosyası üzerinden birlikte çalıştırma mantığını öğrendim.

Adminer üzerinden veritabanına bağlanmayı ve volume kullanmanın verinin korunması açısından neden önemli olduğunu öğrendim.

Ayrıca StajDesk mimarisindeki parçaların hangi görevleri üstlendiğini incelemeye başladım.

### Kavrama Soruları

**1. Image ile container arasındaki farkı bir benzetmeyle açıklayınız.**

Image'ı bir yemek tarifi veya kalıp gibi düşünebilirim. Container ise bu tarif kullanılarak hazırlanmış çalışan örnektir. Yani image kaynak/şablon, container ise çalışan örnektir.

**2. Volume tanımlamasaydık veritabanı konteyneri silindiğinde ne olurdu?**

Veritabanı verileri konteynerin kendi depolama alanında tutuluyorsa konteyner silindiğinde bu veriler de kaybolabilirdi. Volume kullanarak verileri konteynerden bağımsız ve kalıcı hale getirebiliriz.

**3. Neden her geliştiricinin bilgisayarına PostgreSQL kurmak yerine Docker kullanıyoruz?**

Docker kullanarak PostgreSQL'in aynı yapı ve sürümde, daha kontrollü ve taşınabilir bir ortamda çalıştırılması sağlanabilir. Böylece geliştiricilerin bilgisayarlarındaki ortam farklılıkları azaltılabilir.

---

# FAZ 1 · C# VE .NET

## HAFTA 3 · C# Temelleri

## Gün 7/48 · Neden .NET? İlk C# Programı

**Tarih:** ____ / ____

### Öğrendiklerim

.NET'in güçlü tip sistemi, performansı, kurumsal ekosistemi ve farklı platformlarda çalışabilmesi gibi özelliklerini öğrendim.

Bir Console uygulaması oluşturmak ve çalıştırmak için temel olarak:

```bash
dotnet new console
dotnet run
```

komutlarını öğrendim.

Ayrıca `.csproj` dosyasının .NET projesinin yapılandırma dosyası olduğunu öğrendim.

### Temel Veri Tipleri

C# içinde kullanılan bazı temel veri tiplerini öğrendim:

```text
int       → Tam sayılar
decimal   → Ondalıklı ve finansal işlemler
string    → Metin
bool      → Doğru / yanlış
DateTime  → Tarih ve saat
```

Tip dönüşümleri ve kullanıcıdan alınan verilerin uygun türlere dönüştürülmesi hakkında bilgi edindim.

### Uygulama

Kullanıcıdan 3 not alıp:

1. Notların ortalamasını hesaplayan
2. Harf notunu belirleyen
3. Hatalı girişleri kontrol eden

bir konsol uygulaması hazırlama mantığını öğrendim.

Hatalı sayısal girişlerde programın çökmemesi için `int.TryParse` kullanılması gerektiğini öğrendim.

---

## Gün 8/48 · Kontrol Akışı ve Metotlar

**Tarih:** ____ / ____

### Öğrendiklerim

Programın çalışma akışını kontrol etmek için kullanılan yapıları öğrendim:

```text
if / else
switch
for
while
foreach
```

Ayrıca kendi metotlarımı tanımlamayı, parametre göndermeyi ve dönüş değeri kullanmayı öğrendim.

Bir metodun belirli bir işi tek başına yapmasının kodun daha okunabilir ve düzenli olmasına yardımcı olduğunu öğrendim.

### Uygulamalar

Aşağıdaki alıştırmalar üzerinde çalıştım:

- 1 ile 100 arasında sayı tahmin oyunu
- `AsalMi(int sayi)` metodu
- 1 ile 100 arasındaki asal sayıları listeleme
- FizzBuzz problemi

Ayrıca kod içerisinde anlamlı değişken ve metod isimleri kullanmanın okunabilirlik açısından önemli olduğunu öğrendim.

---

## Gün 9/48 · Koleksiyonlar ve LINQ

**Tarih:** ____ / ____

### Öğrendiklerim

Birden fazla veriyi bir arada tutmak için kullanılan koleksiyon yapılarını öğrendim.

### Dizi

Dizinin boyutunun oluşturulduktan sonra sabit olduğunu öğrendim.

Örneğin:

```csharp
string[] isimler = { "Ahmet", "Mehmet", "Ayşe" };
```

### List<T>

`List<T>` yapısının dinamik bir koleksiyon olduğunu öğrendim. Eleman ekleme ve silme işlemlerinin kolay olduğunu öğrendim.

Örneğin:

```csharp
List<int> notlar = new List<int>();
```

Buradaki `T`, listenin hangi tür veriyi tutacağını belirtir.

### Dictionary<TKey, TValue>

Dictionary yapısının anahtar ve değer mantığıyla çalıştığını öğrendim.

Örneğin:

```text
Öğrenci Adı → Not
```

şeklinde bir ilişki kurulabilir.

### LINQ

LINQ'in koleksiyonlar üzerinde daha okunabilir ve kolay sorgular yapmamı sağladığını öğrendim.

Önemli metotlar:

```text
Where             → Filtreleme
Select            → İstenen bilgiyi seçme
OrderBy           → Artan sırada sıralama
OrderByDescending → Azalan sırada sıralama
Count             → Sayma
FirstOrDefault    → İlk elemanı alma
```

Ek olarak `GroupBy` ile gruplama ve `Average` ile ortalama hesaplamayı kullandım.

### Uygulama

10 öğrenciden oluşan bir liste oluşturdum.

Her öğrencinin:

- Adı
- Sınıfı
- Notu

bulunmaktadır.

Örnek öğrenci yapısı:

```csharp
class Student
{
    public string Ad { get; set; } = "";
    public int Sinif { get; set; }
    public decimal Not { get; set; }
}
```

10 öğrencilik liste üzerinde LINQ sorguları uyguladım.

### 1. Notu 70 üstü öğrenciler

```csharp
var notuYetmisUstu = ogrenciler.Where(o => o.Not > 70);
```

Burada `Where` kullanarak öğrencileri notlarına göre filtreledim.

`o`, listedeki her öğrenciyi temsil ediyor.

`o.Not > 70` ise öğrencinin notunun 70'ten büyük olup olmadığını kontrol ediyor.

### 2. Sınıfa göre ortalama

```csharp
var sinifOrtalamalari = ogrenciler.GroupBy(o => o.Sinif);

foreach (var grup in sinifOrtalamalari)
{
    decimal ortalama = grup.Average(o => o.Not);

    Console.WriteLine($"{grup.Key}. sınıf: {ortalama:F2}");
}
```

Burada:

- `GroupBy` öğrencileri sınıflarına göre grupluyor.
- `grup.Key` grubun sınıf bilgisini veriyor.
- `Average` o sınıftaki notların ortalamasını hesaplıyor.

### 3. En yüksek not alan öğrenci

```csharp
var enYuksek = ogrenciler
    .OrderByDescending(o => o.Not)
    .FirstOrDefault();
```

Burada önce öğrencileri notlarına göre büyükten küçüğe sıraladım. Daha sonra ilk öğrenciyi aldım. Böylece en yüksek not alan öğrenciyi buldum.

Ayrıca `FirstOrDefault()` sonucunun `null` olabileceğini düşünerek `null` kontrolü kullandım.

```csharp
if (enYuksek != null)
{
    Console.WriteLine($"{enYuksek.Ad} - {enYuksek.Not}");
}
```

### Kavrama Soruları

**1. decimal ile double arasındaki fark nedir? Para için hangisi kullanılır, neden?**

`double` ve `decimal` ondalıklı sayılarla çalışmak için kullanılır. `double` daha çok genel teknik ve bilimsel hesaplamalarda kullanılır. `decimal` ise özellikle para ve finansal işlemler için tercih edilir. Para hesaplarında `decimal` kullanmak daha uygun olduğu için fiyat ve benzeri değerlerde `decimal` tercih edilir.

**Para için: `decimal`**

---

**2. List<T> ile dizi arasındaki fark nedir?**

Dizinin boyutu oluşturulduktan sonra sabittir. `List<T>` ise dinamik olarak büyüyüp küçülebilir. Listeye eleman eklemek ve silmek daha kolaydır. Bu nedenle eleman sayısının değişebildiği durumlarda `List<T>` daha kullanışlıdır.

---

**3. LINQ olmasaydı "notu 70 üstü öğrenciler" sorgusunu nasıl yazardınız?**

LINQ kullanmadan bütün öğrencileri `foreach` ile tek tek dolaşırdım. Daha sonra `if` ile öğrencinin notunun 70'ten büyük olup olmadığını kontrol ederdim. Şartı sağlayan öğrencileri başka bir listeye eklerdim.

```csharp
List<Student> notuYetmisUstu = new List<Student>();

foreach (Student ogrenci in ogrenciler)
{
    if (ogrenci.Not > 70)
    {
        notuYetmisUstu.Add(ogrenci);
    }
}
```

### Gün 9 Sonucu

LINQ kullanarak:

- Öğrencileri filtrelemeyi
- Belirli bilgileri seçmeyi
- Verileri sıralamayı
- Öğrenci sayılarını bulmayı
- Öğrencileri sınıflarına göre gruplamayı
- Sınıf ortalamalarını hesaplamayı
- En yüksek not alan öğrenciyi bulmayı
- `null` kontrolü yapmayı

öğrendim.

---

# Genel Değerlendirme

İlk 3 haftada temel geliştirme ortamını, Git'i, web isteklerinin mantığını, Docker'ı ve C#/.NET temellerini öğrenmeye başladım.

Şu ana kadar öğrendiğim yapıların birbirleriyle bağlantısını görmeye başladım:

```text
Git
 ↓
Kod geliştirme
 ↓
C# / .NET
 ↓
Koleksiyonlar ve LINQ
 ↓
Docker
 ↓
Web ve HTTP
```

Staj boyunca öğrendiğim konuları uygulamalı olarak geliştirerek ilerletmeyi hedefliyorum.