# StajDesk Öğrenme Günlüğü

## Çarşamba — Gün 1/48

****Tarih: /****

### Bugün Öğrendiklerim

Stajın amacı, StajDesk projesi ve 16 haftalık staj yol haritası hakkında bilgi edindim.

Bir yazılım ekibinde Jira'nın görev takibi, Git'in sürüm kontrolü ve code review'nun kod kalitesini kontrol etmek için kullanıldığını öğrendim.

VS Code, Git, .NET SDK, Node.js ve Docker Desktop araçlarının kullanım amaçlarını öğrendim.

### Kurulum Kontrolleri

`dotnet --version` → 10.0.401

`node -v` → v26.10.0

`git --version` → git version 2.55.0.windows.5

`docker --version` → Docker version 29.8.0, build 88096ef

### Gün Sonu

Gerekli geliştirme araçlarını kurdum ve çalıştıklarını kontrol ettim. `stajdesk` klasörünü ve `LOG.md` dosyasını oluşturdum.

---

## Perşembe — Gün 2/48

****Tarih: /****

### Bugün Öğrendiklerim

Terminalde `cd`, `ls`, `dir` ve `mkdir` komutlarını öğrendim.

Göreli ve mutlak dosya yollarını öğrendim.

Git'te repository, commit, branch, merge ve remote kavramlarını öğrendim.

Anlamlı commit mesajlarının nasıl yazılması gerektiğini öğrendim.

### Yaptıklarım

Şirket Git sunucusunda `stajdesk` reposu oluşturdum ve bilgisayarıma klonladım.

`README.md` dosyası oluşturarak kendimi ve projeyi tanıttım.

En az 5 anlamlı commit oluşturdum.

Deneme branch'i açarak değişiklik yaptım ve ana dala merge ettim.

### Gün Sonu

Git üzerinde repository, commit, branch ve merge işlemlerini uyguladım. Depo bağlantısını mentorla paylaştım.

---

## Cuma — Gün 3/48

****Tarih: /****

### Bugün Öğrendiklerim

Web'in istemci ve sunucu mantığıyla çalıştığını öğrendim.

URL, DNS ve HTTP'nin web üzerindeki görevlerini öğrendim.

GET, POST, PUT ve DELETE metotlarını inceledim.

200, 201, 400, 401, 404 ve 500 HTTP durum kodlarının anlamlarını öğrendim.

JSON veri formatını ve tarayıcının Network sekmesini inceledim.

### Yaptıklarım

3 farklı web sitesinin Network sekmesindeki HTTP isteklerini inceledim.

| Site      | Metot | Durum Kodu |
| --------- | ----- | ---------- |
| Google    | GET   | 200        |
| GitHub    | GET   | 200        |
| Wikipedia | GET   | 200        |

`jsonplaceholder.typicode.com` üzerinde GET ve POST istekleri gönderdim.

POST isteğinde JSON formatında örnek veri kullandım.

### Kavrama Soruları

**1. Tarayıcıya bir adres yazıp Enter'a bastığınızda neler olur?**

URL işlenir, DNS ile alan adının IP adresi bulunur. Tarayıcı sunucuya HTTP/HTTPS isteği gönderir. Sunucu yanıt verir ve tarayıcı gelen verileri işleyerek sayfayı gösterir.

**2. Commit ile push arasındaki fark nedir?**

Commit değişiklikleri yerel Git geçmişine kaydeder. Push ise commitleri uzak repository'ye gönderir.

**3. 404 ile 500 arasındaki fark nedir?**

404 istenen kaynağın bulunamadığını, 500 ise sunucu tarafında beklenmeyen bir hata oluştuğunu gösterir.

### Gün Sonu

HTTP istek ve yanıtlarının nasıl çalıştığını, HTTP metotlarını ve durum kodlarını daha iyi anlamaya başladım. Postman/Bruno ve Network sekmesini kullanarak pratik yaptım.

---

# Hafta 2 | Docker ile Tanışma

## Çarşamba — Gün 4/48

### Konteyner nedir, neden Docker?

****Tarih: /****

### Bugün Öğrendiklerim

Docker'ın uygulamaları farklı bilgisayarlarda benzer ortamlarda çalıştırmak için kullanıldığını öğrendim.

"Benim bilgisayarımda çalışıyordu" problemini ve Docker'ın bu problemi azaltmadaki rolünü öğrendim.

Image ve container arasındaki farkı öğrendim.

Docker Hub, port mapping ve container yaşam döngüsü hakkında bilgi edindim.

### Yaptıklarım

`docker run hello-world` komutunu çalıştırarak Docker'ı test ettim.

`docker run -d -p 8080:80 nginx` komutu ile Nginx çalıştırdım.

`localhost:8080` üzerinden Nginx sayfasını açtım.

`docker ps`, `docker logs`, `docker stop` ve `docker rm` komutlarını kullandım.

8080 portunun container içerisindeki 80 portuna yönlendirildiğini gözlemledim.

### Kavrama Soruları

**1. Image ile container arasındaki fark nedir?**

Image, container oluşturmak için kullanılan şablondur. Container ise bu image'dan oluşturulan çalışan örnektir.

**2. Docker neden "benim bilgisayarımda çalışıyordu" problemini azaltır?**

Uygulamanın ihtiyaç duyduğu ortam ve bağımlılıkları container içerisinde tutarak ortam farklılıklarını azaltır.

**3. 8080:80 port eşlemesi ne anlama gelir?**

Bilgisayarımın 8080 portunun container içerisindeki 80 portuna bağlanmasıdır.

### Gün Sonu

Docker'ın temel çalışma mantığını öğrendim ve Nginx çalıştırarak container, image ve port mapping kavramlarını uygulamalı olarak gördüm.

---

## Perşembe — Gün 5/48

### Dockerfile Yazmak

****Tarih: /****

### Bugün Öğrendiklerim

Dockerfile'ın Docker image oluşturmak için kullanıldığını öğrendim.

`FROM`, `WORKDIR`, `COPY`, `RUN`, `EXPOSE` ve `CMD` komutlarının görevlerini öğrendim.

Image katmanları, build önbelleği ve volume kavramları hakkında bilgi edindim.

### Yaptıklarım

Kendimi tanıtan basit bir HTML sayfası hazırladım.

Nginx tabanlı bir Dockerfile oluşturdum.

HTML dosyamı `COPY` ile image içerisine ekledim.

`docker build` ile kendi image'ımı oluşturdum ve `docker run` ile çalıştırdım.

HTML sayfamı tarayıcı üzerinden görüntüledim.

Dockerfile ve HTML dosyalarımı Git reposuna commit ettim.

### Kavrama Soruları

**1. Dockerfile nedir?**

Docker image'ın nasıl oluşturulacağını belirleyen dosyadır.

**2. `COPY` ile `RUN` arasındaki fark nedir?**

`COPY` dosya kopyalamak, `RUN` ise image oluşturulurken komut çalıştırmak için kullanılır.

**3. Container silindiğinde veriler neden kaybolabilir?**

Container içerisindeki dosyalar container ile birlikte silinebileceği için kalıcı verilerde volume kullanılır.

### Gün Sonu

Dockerfile kullanarak kendi image'ımı oluşturmayı ve bu image'dan container çalıştırmayı öğrendim. Oluşturduğum dosyaları Git reposuna ekledim.

---

## Cuma — Gün 6/48

### Docker Compose ve Proje Mimarisi

****Tarih: /****

### Bugün Öğrendiklerim

Docker Compose'un birden fazla containerı birlikte yönetmek için kullanıldığını öğrendim.

Docker network, environment variable ve volume kavramlarını öğrendim.

PostgreSQL'in veritabanı, Adminer'ın ise veritabanını yönetmek için kullanılan bir arayüz olduğunu öğrendim.

StajDesk projesindeki servislerin nasıl birlikte çalıştığı hakkında bilgi edindim.

### Yaptıklarım

Docker Compose ile PostgreSQL ve Adminer servislerini çalıştırdım.

Adminer üzerinden PostgreSQL veritabanına bağlandım.

Volume kullanarak verilerin container silinse bile korunmasını sağladım ve test ettim.

Docker network üzerinden servislerin birbiriyle iletişim kurduğunu gözlemledim.

### Kavrama Soruları

**1. Image ile container arasındaki farkı bir benzetmeyle açıklayınız.**

Image'ı bir kalıp, containerı ise bu kalıptan oluşturulan gerçek ürün gibi düşünebilirim.

**2. Volume olmasaydı veritabanı containerı silindiğinde ne olurdu?**

Container içerisindeki veriler kaybolabilirdi. Volume kullanarak verilerin korunmasını sağlayabiliriz.

**3. Neden PostgreSQL'i Docker ile kullanıyoruz?**

Her geliştiricinin bilgisayarına ayrı ayrı kurulum yapmak yerine Docker ile daha benzer ve kolay yönetilebilir bir ortam oluşturabiliriz.

### Git İşlemleri

Gün 6'da yaptığım Docker Compose çalışmalarını Git'e commit ettim ve GitHub repository'me pushladım.

### Gün Sonu

Bugün Docker Compose ile PostgreSQL ve Adminer servislerini birlikte çalıştırdım. Volume, network ve environment variable kavramlarını uygulamalı olarak öğrendim. Yaptığım çalışmaları GitHub'a pushladım.
