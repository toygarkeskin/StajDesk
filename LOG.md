# StajDesk Öğrenme Günlüğü

## Çarşamba — Gün 1/48

**Tarih: /**

### Bugün Öğrendiklerim

Bugün stajın nasıl ilerleyeceği, StajDesk projesinin amacı ve önümüzdeki 16 haftalık süreç hakkında genel bir bilgilendirme aldım.

Yazılım geliştirme sürecinde sadece kod yazmanın yeterli olmadığını, yapılan işlerin düzenli şekilde takip edilmesinin de önemli olduğunu öğrendim. Bu kapsamda Jira'nın görev takibi, Git'in kod ve değişiklik takibi, code review'nun ise yazılan kodun kontrol edilmesi ve geliştirilmesi amacıyla kullanıldığını öğrendim.

Staj boyunca kullanacağım temel geliştirme araçlarını da tanıdım. VS Code'un kod geliştirmek için, .NET SDK'nın .NET uygulamaları geliştirmek için, Node.js'in JavaScript tabanlı geliştirme ortamları için ve Docker Desktop'ın container işlemleri için kullanıldığını öğrendim.

### Kurulum Kontrolleri

Kurulumlardan sonra terminal üzerinden gerekli araçların doğru şekilde çalıştığını kontrol ettim.

`dotnet --version` → 10.0.401

`node -v` → v26.10.0

`git --version` → git version 2.55.0.windows.5

`docker --version` → Docker version 29.8.0, build 88096ef

### Gün Sonu

Bugün daha çok staj ortamına ve kullanılacak araçlara alışmaya çalıştım. Gerekli geliştirme araçlarını kurup çalıştıklarını kontrol ettim. Ayrıca `stajdesk` klasörünü ve `LOG.md` dosyasını oluşturarak öğrendiklerimi düzenli şekilde kayıt altına almaya başladım.

---

## Perşembe — Gün 2/48

### Git ve Terminal Temelleri

**Tarih: /**

### Bugün Öğrendiklerim

Bugün terminal ve Git'in temel kullanımını öğrendim.

Terminal üzerinde `cd`, `ls`, `dir` ve `mkdir` gibi komutları kullanarak klasörler arasında dolaşmayı ve yeni klasörler oluşturmayı denedim. Göreli ve mutlak dosya yollarının arasındaki farkı uygulama yaparak daha iyi anlamaya çalıştım.

Git tarafında repository, commit, branch, merge ve remote gibi temel kavramları öğrendim. Özellikle commit ile push arasındaki farkı uygulamalı olarak görmek benim için faydalı oldu. Commit'in değişiklikleri kendi bilgisayarımızdaki Git geçmişine kaydettiğini, push işleminin ise bu commitleri uzak repository'ye gönderdiğini öğrendim.

Ayrıca commit mesajlarının sadece değişikliği kaydetmek için değil, yapılan değişikliği sonradan anlayabilmek için de önemli olduğunu öğrendim.

### Yaptıklarım

Şirket Git sunucusunda `stajdesk` repository'si oluşturdum ve repository'yi bilgisayarıma klonladım.

Projeye bir `README.md` dosyası ekleyerek proje hakkında temel bilgileri yazdım.

Farklı değişiklikler yaparak birkaç commit oluşturdum ve commit mesajlarını mümkün olduğunca yaptığım işlemi anlatacak şekilde yazmaya çalıştım.

Ayrıca deneme amaçlı bir branch oluşturdum. Branch üzerinde değişiklik yaptıktan sonra bu değişiklikleri ana branch'e merge ettim.

### Gün Sonu

Bugün Git'in sadece dosyaları saklamak için kullanılan bir araç olmadığını, ekip içerisinde yapılan değişiklikleri düzenli takip etmek için önemli olduğunu daha iyi anladım.

Özellikle branch ve merge işlemlerini kendim uygulayınca konular daha anlaşılır hale geldi.

---

## Cuma — Gün 3/48

### Web Temelleri ve HTTP

**Tarih: /**

### Bugün Öğrendiklerim

Bugün web uygulamalarının temel çalışma mantığını inceledim.

Bir web uygulamasında istemci ve sunucunun nasıl iletişim kurduğunu öğrendim. URL, DNS ve HTTP'nin bu iletişimdeki görevlerini inceledim.

GET, POST, PUT ve DELETE metotlarının farklı amaçlarla kullanıldığını öğrendim. Ayrıca HTTP durum kodlarını inceleyerek sunucudan gelen cevabın başarılı mı yoksa hatalı mı olduğunu anlamanın mümkün olduğunu gördüm.

JSON formatını ve tarayıcıdaki Network sekmesini de inceledim. Network sekmesinin gönderilen istekleri ve sunucudan gelen cevapları incelemek için oldukça faydalı olduğunu gördüm.

### Yaptıklarım

Farklı web sitelerinin Network sekmesindeki HTTP isteklerini inceledim.

| Site      | Metot | Durum Kodu |
| --------- | ----- | ---------: |
| Google    | GET   |        200 |
| GitHub    | GET   |        200 |
| Wikipedia | GET   |        200 |

Daha sonra `jsonplaceholder.typicode.com` üzerinden GET ve POST istekleri gönderdim.

POST isteğinde JSON formatında örnek veri göndererek istemciden sunucuya nasıl veri gönderildiğini uygulamalı olarak gördüm.

### Kavrama Soruları

**1. Tarayıcıya bir adres yazıp Enter'a bastığımızda neler olur?**

Öncelikle URL işlenir ve DNS üzerinden alan adının IP adresi bulunur. Daha sonra tarayıcı sunucuya HTTP/HTTPS isteği gönderir. Sunucudan gelen cevap tarayıcı tarafından işlenerek sayfa kullanıcıya gösterilir.

**2. Commit ile push arasındaki fark nedir?**

Commit yaptığım değişiklikleri yerel Git geçmişine kaydeder. Push ise bu commitleri uzak repository'ye gönderir.

**3. 404 ile 500 arasındaki fark nedir?**

404 istenen kaynağın bulunamadığını gösterir. 500 ise sunucu tarafında beklenmeyen bir hata oluştuğunu gösterir.

### Gün Sonu

Bugün web tarafındaki istek-cevap mantığını daha iyi anlamaya başladım. Özellikle Network sekmesinde gerçek istekleri görmek konunun sadece teorik olmadığını anlamamı sağladı.

Postman/Bruno ve tarayıcı Network sekmesini kullanarak birkaç farklı HTTP isteği gönderip inceleme yaptım.

---

# Hafta 2 | Docker ile Tanışma

## Çarşamba — Gün 4/48

### Konteyner Nedir, Neden Docker?

**Tarih: /**

### Bugün Öğrendiklerim

Bugün Docker'ın temel çalışma mantığını öğrenmeye başladım.

Bir uygulamanın farklı bilgisayarlarda çalışırken işletim sistemi, kütüphaneler veya kurulumlardan dolayı farklı sonuçlar verebileceğini öğrendim. Docker'ın uygulamayı ihtiyaç duyduğu ortamla birlikte container içerisinde çalıştırarak bu tür ortam farklılıklarını azaltmaya yardımcı olduğunu öğrendim.

Image ve container kavramlarının birbirinden farklı olduğunu öğrendim. Image'ı bir uygulamanın çalıştırılabilmesi için kullanılan hazır yapı, container'ı ise bu yapıdan oluşturulan çalışan örnek olarak düşünmeye başladım.

Ayrıca Docker Hub, port mapping ve container yaşam döngüsü hakkında temel bilgiler öğrendim.

### Yaptıklarım

İlk olarak:

`docker run hello-world`

komutunu çalıştırarak Docker kurulumumu test ettim.

Daha sonra:

`docker run -d -p 8080:80 nginx`

komutuyla Nginx container'ı çalıştırdım.

Tarayıcıdan `localhost:8080` adresine giderek Nginx'in çalıştığını kontrol ettim.

Container'ı incelemek için `docker ps` ve `docker logs` komutlarını kullandım. Daha sonra `docker stop` ve `docker rm` ile container'ı durdurup kaldırdım.

Burada özellikle `8080:80` port eşlemesini uygulamalı olarak görmek faydalı oldu.

### Kavrama Soruları

**1. Image ile container arasındaki fark nedir?**

Image, container oluşturmak için kullanılan yapıdır. Container ise bu image'dan oluşturulan çalışan örnektir.

**2. Docker neden "benim bilgisayarımda çalışıyordu" problemini azaltır?**

Uygulamanın ihtiyaç duyduğu ortam ve bağımlılıkları container içerisinde tutarak farklı bilgisayarlardaki ortam farklarını azaltmaya yardımcı olur.

**3. 8080:80 port eşlemesi ne anlama gelir?**

Bilgisayarımın 8080 portuna gelen isteklerin container içerisindeki 80 portuna yönlendirilmesi anlamına gelir.

### Gün Sonu

Bugün Docker'ın temel mantığını sadece okuyarak değil, Nginx çalıştırarak uygulamalı şekilde gördüm.

Özellikle container başlatma, durdurma ve silme işlemlerini kendim yapmak Docker'ın çalışma mantığını anlamama yardımcı oldu.

---

## Perşembe — Gün 5/48

### Dockerfile Yazmak

**Tarih: /**

### Bugün Öğrendiklerim

Bugün kendi Docker image'ımı oluşturmayı öğrendim.

Dockerfile'ın bir image'ın nasıl oluşturulacağını tanımlayan dosya olduğunu öğrendim.

`FROM`, `WORKDIR`, `COPY`, `RUN`, `EXPOSE` ve `CMD` komutlarının temel görevlerini inceledim.

Ayrıca Docker image'larının katmanlardan oluştuğunu ve build sırasında cache kullanılabildiğini öğrendim. Volume kavramının ise container dışında kalıcı veri tutmak için kullanılabileceğini gördüm.

### Yaptıklarım

Kendimi tanıtan basit bir HTML sayfası hazırladım.

Daha sonra Nginx tabanlı bir Dockerfile oluşturdum ve HTML dosyamı `COPY` komutuyla image içerisine ekledim.

`docker build` komutuyla kendi image'ımı oluşturdum. Ardından `docker run` kullanarak bu image'dan bir container çalıştırdım.

Tarayıcı üzerinden HTML sayfamı açarak container içerisinde doğru şekilde çalıştığını kontrol ettim.

Son olarak Dockerfile ve HTML dosyalarını Git repository'sine ekleyip commit ettim.

### Kavrama Soruları

**1. Dockerfile nedir?**

Docker image'ın hangi adımlarla oluşturulacağını belirleyen dosyadır.

**2. `COPY` ile `RUN` arasındaki fark nedir?**

`COPY` dosyaları image içerisine almak için kullanılır. `RUN` ise image oluşturulurken bir komut çalıştırmak için kullanılır.

**3. Container silindiğinde veriler neden kaybolabilir?**

Container içerisinde tutulan veriler container'ın yaşam döngüsüne bağlı olabileceği için container silindiğinde bu veriler de kaybolabilir. Kalıcı veriler için volume kullanılabilir.

### Gün Sonu

Bugün hazır bir image kullanmanın yanında kendi image'ımı nasıl oluşturabileceğimi öğrendim.

Dockerfile içerisindeki komutların sırasını ve her komutun image oluşturma sürecindeki görevini uygulamalı olarak görmüş oldum.

---

## Cuma — Gün 6/48

### Docker Compose ve Proje Mimarisi

**Tarih: /**

### Bugün Öğrendiklerim

Bugün Docker Compose kullanarak birden fazla servisin birlikte nasıl çalıştırılabileceğini öğrendim.

Docker network, environment variable ve volume kavramlarını daha detaylı inceleme fırsatım oldu.

PostgreSQL'in veritabanı olarak, Adminer'ın ise veritabanını yönetmek için kullanılan web tabanlı bir arayüz olarak kullanıldığını öğrendim.

Ayrıca StajDesk projesinde farklı servislerin tek başına değil, birbiriyle iletişim halinde çalıştığını ve Docker Compose'un bu yapıyı yönetmeyi kolaylaştırdığını gördüm.

### Yaptıklarım

Docker Compose kullanarak PostgreSQL ve Adminer servislerini çalıştırdım.

Adminer arayüzüne girerek PostgreSQL veritabanına bağlandım.

Volume kullanarak veritabanındaki verilerin container silinse bile korunabildiğini test ettim.

Docker network sayesinde PostgreSQL ve Adminer servislerinin birbirleriyle iletişim kurabildiğini gözlemledim.

### Kavrama Soruları

**1. Image ile container arasındaki farkı bir benzetmeyle açıklayınız.**

Image'ı bir kalıp, container'ı ise bu kalıptan oluşturulan ürün gibi düşünebilirim.

**2. Volume olmasaydı veritabanı containerı silindiğinde ne olurdu?**

Veriler container'ın kendi dosya sistemi içerisinde tutuluyorsa container ile birlikte kaybolabilir. Volume kullanıldığında veriler container'dan bağımsız olarak saklanabilir.

**3. Neden PostgreSQL'i Docker ile kullanıyoruz?**

Her geliştiricinin bilgisayarına PostgreSQL'i ayrı ayrı kurmak yerine Docker kullanarak daha standart ve kolay yönetilebilir bir geliştirme ortamı oluşturabiliriz.

### Git İşlemleri

Bugün yaptığım Docker Compose çalışmalarını Git'e commit ettim ve GitHub repository'me pushladım.

### Gün Sonu

Bugün Docker'ın sadece tek bir container çalıştırmaktan ibaret olmadığını, birden fazla servisin network ve volume gibi yapılar üzerinden birlikte çalışabileceğini gördüm.

PostgreSQL ve Adminer'ı birlikte çalıştırmak, ileride StajDesk projesinin veritabanı tarafını anlamam açısından faydalı oldu.

---

# Hafta 3 | C# Temelleri

## Çarşamba — Gün 7/48

### Neden .NET? İlk C# Programı

**Tarih: /**

### Bugün Öğrendiklerim

Bugün C# ve .NET tarafına giriş yaptım.

.NET'in C# gibi dillerle uygulama geliştirmek için kullanılan bir platform olduğunu öğrendim. C#'ın programlama dili, .NET'in ise bu dil ile geliştirilen uygulamaların oluşturulması ve çalıştırılması için kullanılan platform olduğunu daha net şekilde anladım.

VS Code'u geliştirme ortamı olarak kullanmaya devam ederken terminal üzerinden `dotnet new console` ile yeni bir proje oluşturmayı öğrendim.

`dotnet run` komutuyla oluşturduğum projeyi çalıştırdım.

`.csproj` dosyasının proje ile ilgili ayarların tutulduğu dosya olduğunu öğrendim.

Ayrıca `int`, `decimal`, `string`, `bool` ve `DateTime` gibi temel veri tiplerini inceleyerek hangi durumda hangi tipin kullanılabileceğini öğrenmeye başladım.

Kullanıcıdan `Console.ReadLine()` ile alınan değerlerin `string` olarak geldiğini ve gerektiğinde uygun veri tipine dönüştürülmesi gerektiğini öğrendim.

Özellikle `int.TryParse()` kullanımının hatalı kullanıcı girişlerinde programın doğrudan hata vermesini önlemek açısından önemli olduğunu gördüm.

### Yaptıklarım

`dotnet new console --force` komutuyla yeni bir konsol projesi oluşturdum.

`dotnet run` ile projemi çalıştırdım.

Kullanıcıdan `Console.ReadLine()` ile üç farklı not aldım.

Girilen değerlerin sayı olup olmadığını `int.TryParse()` ile kontrol ettim.

Ayrıca notların 0 ile 100 arasında olup olmadığını kontrol ederek geçersiz girişlerde kullanıcıdan tekrar veri aldım.

Bunun için `while` döngüsünü kullandım.

Üç notun ortalamasını `decimal` kullanarak hesapladım ve sonuca göre A, B, C, D veya F harf notunu belirledim.

Ortalama sonucunu iki ondalık basamakla ekrana yazdırdım.

Programı hem doğru hem de hatalı girişlerle test ettim.

C# projemde `bin` ve `obj` klasörlerinin repository'ye eklenmemesi için `.gitignore` kullandım.

Son olarak not hesaplama uygulamasını commit edip uzak repository'ye pushladım.

### Kavrama Soruları

**1. C# ile .NET arasındaki ilişki nedir?**

C# bir programlama dilidir. .NET ise C# ile uygulama geliştirmek, derlemek ve çalıştırmak için kullanılan platformdur.

**2. `dotnet new console` komutu ne işe yarar?**

Yeni bir .NET konsol uygulaması projesi oluşturmak için kullanılır.

**3. `dotnet run` komutu ne işe yarar?**

.NET projesini çalıştırmak için kullanılır.

**4. `.csproj` dosyası nedir?**

C# projesinin yapılandırma dosyasıdır. Projenin hedeflediği .NET sürümü ve diğer proje ayarları burada bulunabilir.

**5. `int` ile `string` arasındaki fark nedir?**

`int` tam sayıları, `string` ise metinleri tutmak için kullanılır. Örneğin `85` bir `int`, `"85"` ise bir `string` değeridir.

**6. `int.TryParse()` neden kullanılır?**

Bir metnin `int` türüne dönüştürülüp dönüştürülemeyeceğini kontrol etmek için kullanılır. Dönüşüm başarısız olduğunda exception oluşturmadan `false` döndürmesi kullanıcı girişlerini kontrol ederken faydalıdır.

**7. Neden notları 0 ile 100 arasında kontrol ettik?**

Not değerlerinin geçerli bir aralıkta olmasını sağlamak için kontrol yaptık. Böylece 0'dan küçük veya 100'den büyük değerlerin programa girmesini engelledik.

**8. `while` döngüsünü neden kullandık?**

Kullanıcı geçersiz bir değer girdiğinde tekrar veri almak için kullandık. Geçerli bir değer girildiğinde döngüden çıkılmasını sağladık.

**9. `decimal` neden ortalama hesabında kullanıldı?**

Ortalama sonucu tam sayı olmayabileceği için ondalıklı değerlerle daha uygun şekilde çalışmak amacıyla kullandım.

**10. `3m` ifadesindeki `m` ne anlama gelir?**

`m`, sayının `decimal` türünde olduğunu belirtir. Böylece ilgili işlem decimal türünde gerçekleştirilir.

### Gün Sonu

Bugün C# ile ilk konsol uygulamamı geliştirerek temel programlama yapılarını uygulamalı olarak öğrenmiş oldum.

Özellikle kullanıcıdan veri alma, veri tipleri, `TryParse`, koşul ifadeleri ve döngüler arasındaki ilişkiyi uygulama üzerinde görmek konuyu daha iyi anlamamı sağladı.

Üç not alan, hatalı girişleri kontrol eden, ortalama hesaplayan ve harf notunu belirleyen bir **Not Hesaplayıcı** uygulaması tamamladım.

Çalışmamı Git'e commit edip uzak repository'ye pushladım.

---

## Perşembe — Gün 8/48

### Kontrol Akışı ve Metotlar

**Tarih: 07/10/2026**

### Bugün Öğrendiklerim

Bugün C# tarafında kontrol yapıları ve metotlar üzerinde çalıştım.

`if / else`, `switch`, `for`, `while` ve `foreach` yapılarını tekrar ederek hangi durumda hangi yapının daha uygun olduğunu anlamaya çalıştım.

`for` döngüsünün tekrar sayısının belli olduğu durumlarda, `while` döngüsünün ise belirli bir koşul devam ettiği sürece işlem yapmak istediğim durumlarda kullanılabileceğini gördüm.

`foreach` kullanarak bir dizi veya koleksiyon içerisindeki elemanları tek tek dolaşmayı öğrendim.

Bugün ayrıca metot konusuna giriş yaptım. Bir işlemi tekrar tekrar yazmak yerine metot içerisine alarak gerektiğinde çağırmanın kodun daha düzenli olmasını sağladığını gördüm.

Metotlara parametre gönderilebildiğini ve metodun yaptığı işlemin sonucunu bir dönüş değeri ile geri verebildiğini öğrendim.

### Yaptıklarım

Bugün öğrendiklerimi birkaç küçük uygulama üzerinden pekiştirdim.

İlk olarak 1 ile 100 arasında rastgele bir sayı tutan bir **Sayı Tahmin Oyunu** geliştirdim.

Kullanıcıdan tahmin aldım ve `int.TryParse()` ile girilen değerin sayı olup olmadığını kontrol ettim.

Tahmin gizli sayıdan küçük olduğunda daha büyük, büyük olduğunda ise daha küçük bir sayı girilmesi gerektiğini belirttim.

Doğru tahmin edildiğinde kaç denemede sonuca ulaşıldığını ekrana yazdırdım.

Daha sonra `AsalMi(int sayi)` adında bir metot oluşturdum.

Bu metot kendisine verilen sayının asal olup olmadığını kontrol ederek `true` veya `false` döndürüyor.

`for` döngüsünü kullanarak 1 ile 100 arasındaki sayıları kontrol ettim ve asal olanları ekrana yazdırdım.

Son olarak **FizzBuzz** problemini çözdüm.

3'e tam bölünen sayılarda `Fizz`, 5'e tam bölünen sayılarda `Buzz`, hem 3'e hem de 5'e tam bölünen sayılarda `FizzBuzz` yazdırdım. Diğer sayıları ise normal şekilde ekrana yazdırdım.

### Kavrama Soruları

**1. `if / else` ne işe yarar?**

Bir koşulun sonucuna göre farklı işlemler yapmamızı sağlar.

**2. `for` ve `while` arasındaki fark nedir?**

`for` genellikle tekrar sayısının belli olduğu durumlarda kullanılır. `while` ise bir koşul doğru olduğu sürece çalışır.

**3. `foreach` ne için kullanılır?**

Dizi veya koleksiyon içerisindeki elemanları sırayla dolaşmak için kullanılır.

**4. Metot nedir?**

Belirli bir işi yapan ve ihtiyaç olduğunda tekrar çağrılabilen kod bölümüdür.

**5. Parametre nedir?**

Bir metoda dışarıdan bilgi göndermek için kullanılan değerdir.

**6. Dönüş değeri nedir?**

Metodun yaptığı işlem sonucunda çağıran yere geri gönderdiği değerdir. Örneğin `AsalMi` metodu `bool` değer döndürür.

**7. Anlamlı isimlendirme neden önemlidir?**

Kodun okunabilirliğini artırır. Bir değişkenin veya metodun ne yaptığını ismine bakarak daha kolay anlayabilmemizi sağlar.

### Gün Sonu

Bugün kontrol yapılarını ve metotları küçük uygulamalar geliştirerek pekiştirdim.

Özellikle bir metodun belirli bir işi kendi içerisinde yapmasının ve gerektiğinde tekrar çağrılabilmesinin kodu daha düzenli hale getirdiğini gördüm.

Sayı Tahmin Oyunu, Asal Sayı ve FizzBuzz olmak üzere üç farklı alıştırmayı tamamladım.

Günün sonunda yaptığım değişiklikleri Git'e commit ederek uzak repository'ye pushladım.
