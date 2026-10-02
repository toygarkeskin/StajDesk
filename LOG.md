# StajDesk Öğrenme Günlüğü

## Çarşamba — Gün 1/48

**Tarih: /**

### Bugün Öğrendiklerim

Stajın amacı ve StajDesk projesi hakkında bilgi edindim.
16 haftalık staj yol haritasını inceledim.
Bir yazılım ekibinin çalışma düzeni hakkında bilgi edindim.
Jira'nın görev takibi için kullanıldığını öğrendim.
Git'in sürüm kontrolü için kullanıldığını öğrendim.
Kod incelemesinin (code review) neden yapıldığını öğrendim.
VS Code, Git, .NET SDK, Node.js ve Docker Desktop araçlarının kullanım amaçlarını öğrendim.

### Kurulum Kontrolleri

`dotnet --version` → 10.0.401
`node -v` → v26.10.0
`git --version` → git version 2.55.0.windows.5
`docker --version` → Docker version 29.8.0, build 88096ef

### Gün Sonu

Gerekli geliştirme araçlarını kurdum ve çalışıp çalışmadıklarını terminal üzerinden kontrol ettim. `stajdesk` klasörünü oluşturdum ve içerisine `LOG.md` dosyasını ekledim.

## Perşembe — Gün 2/48

**Tarih: /**

### Bugün Öğrendiklerim

Terminalde `cd`, `ls`, `dir` ve `mkdir` komutlarını öğrendim.
Göreli ve mutlak dosya yolları arasındaki farkı öğrendim.
Git'in ne olduğunu ve neden kullanıldığını öğrendim.
Repository (repo), commit, branch, merge ve remote kavramlarını öğrendim.
Anlamlı commit mesajlarının nasıl yazılması gerektiğini öğrendim.

### Yaptıklarım

Şirket Git sunucusunda `stajdesk` reposu oluşturdum.
Repoyu bilgisayarıma klonladım.
`README.md` dosyası oluşturdum.
Kendimi ve projeyi tanıtan bilgiler ekledim.
En az 5 anlamlı commit oluşturdum.
Deneme branch'i oluşturdum.
Branch üzerinde değişiklik yaptım.
Değişiklikleri ana dala merge ettim.

### Gün Sonu

StajDesk deposunu oluşturdum ve bilgisayarıma klonladım. Git üzerinde commit, branch ve merge işlemlerini uyguladım. Depo bağlantısını mentorla paylaştım.

## Cuma — Gün 3/48

**Tarih: /**

### Bugün Öğrendiklerim

Web'in istemci (client) ve sunucu (server) mantığıyla çalıştığını öğrendim.
Tarayıcının istemci olarak sunucuya istek gönderdiğini öğrendim.
URL'nin bir web sayfasına veya kaynağa ulaşmak için kullanılan adres olduğunu öğrendim.
DNS'in alan adlarını IP adresleriyle eşleştirdiğini öğrendim.
HTTP'nin istemci ve sunucu arasındaki iletişimi sağlamak için kullanıldığını öğrendim.
GET, POST, PUT ve DELETE HTTP metotlarının ne amaçla kullanıldığını öğrendim.
HTTP durum kodlarının sunucunun isteğe verdiği sonucu gösterdiğini öğrendim.
200, 201, 400, 401, 404 ve 500 durum kodlarının ne anlama geldiğini öğrendim.
JSON veri formatının istemci ve sunucu arasında veri göndermek için kullanıldığını öğrendim.
Tarayıcı geliştirici araçlarında bulunan Network sekmesinin web isteklerini incelemek için kullanıldığını öğrendim.

### Yaptıklarım

Tarayıcı geliştirici araçlarını açtım ve Network sekmesini inceledim.
3 farklı web sitesinde yapılan HTTP isteklerini gözlemledim.
İsteklerin HTTP metotlarını ve durum kodlarını kontrol ettim.
İstek adreslerini ve URL yapılarını inceledim.
`jsonplaceholder.typicode.com` üzerinde GET isteği gönderdim.
GET isteğinden dönen JSON verisini inceledim.
Postman/Bruno kullanarak POST isteği gönderdim.
POST isteğinde JSON formatında örnek veri gönderdim.
HTTP isteği ile HTTP yanıtı arasındaki farkı gözlemledim.

### Network İncelemeleri

| Site      | Metot | Adres                      | Durum Kodu |
| --------- | ----- | -------------------------- | ---------- |
| Google    | GET   | https://www.google.com/    | 200        |
| GitHub    | GET   | https://github.com/        | 200        |
| Wikipedia | GET   | https://www.wikipedia.org/ | 200        |

### Postman / Bruno Çalışması

`jsonplaceholder.typicode.com` üzerinde GET ve POST istekleri gönderdim.

GET isteğinde sunucudan JSON formatında veri aldım. POST isteğinde ise sunucuya JSON formatında örnek veri gönderdim.

```json
{
  "title": "StajDesk",
  "body": "HTTP ve JSON çalışması",
  "userId": 1
}
```

Gönderdiğim isteğe sunucudan gelen yanıtı inceleyerek JSON verisinin nasıl kullanıldığını gördüm.

### Kavrama Soruları

**1. Tarayıcıya bir adres yazıp Enter'a bastığınızda neler olur?**

Tarayıcıya bir adres yazıp Enter'a bastığımda öncelikle URL işlenir. Alan adının hangi sunucuya ait olduğunu bulmak için DNS kullanılır. Daha sonra tarayıcı sunucuya HTTP veya HTTPS üzerinden bir istek gönderir. Sunucu isteği işleyerek tarayıcıya bir yanıt gönderir. Tarayıcı da gelen verileri işleyerek web sayfasını ekranda gösterir.

**2. Commit ile push arasındaki fark nedir?**

Commit, yaptığım değişiklikleri kendi bilgisayarımdaki Git geçmişine kaydetmektir. Push ise commitlediğim değişiklikleri uzak repository'ye göndermektir. Yani commit değişiklikleri yerel olarak kaydeder, push ise bu değişiklikleri remote repository'ye gönderir.

**3. 404 ile 500 durum kodu arasındaki fark nedir? Hangisi kimin hatasıdır?**

404 durum kodu, istenen kaynağın bulunamadığını gösterir. Örneğin yanlış veya bulunmayan bir URL'ye istek gönderildiğinde 404 alınabilir.

500 durum kodu ise sunucu tarafında beklenmeyen bir hata oluştuğunu gösterir. 404 kaynağın bulunamamasıyla, 500 ise sunucunun isteği işlerken yaşadığı hatayla ilgilidir.

### Gün Sonu

Bugün web'in istemci ve sunucu arasındaki çalışma mantığını öğrendim. HTTP metotlarını, durum kodlarını ve JSON veri formatını inceledim. Network sekmesinden farklı web sitelerindeki istekleri gözlemledim. Postman/Bruno kullanarak GET ve POST istekleri gönderdim. Böylece tarayıcıya bir adres yazdığımda arka planda gerçekleşen işlemleri daha iyi anlamaya başladım.

# Hafta 2 | Docker ile Tanışma

**Haftanın hedefi:** Konteyner mantığını anlamak; image, container, volume ve Docker Compose kavramlarını uygulamalı öğrenmek.

## Çarşamba — Gün 4/48

### Konteyner nedir, neden Docker?

**Tarih: /**

### Bugün Öğrendiklerim

Docker'ın uygulamaları farklı bilgisayarlarda benzer ortamlarda çalıştırmak için kullanıldığını öğrendim.
"Benim bilgisayarımda çalışıyordu" probleminin ortam ve bağımlılık farklılıklarından kaynaklanabileceğini öğrendim.
Image ve container arasındaki farkı öğrendim.
Docker image'ın uygulama için gerekli dosya ve yapılandırmaları içeren bir şablon, container'ın ise bu image'dan oluşturulan çalışan örnek olduğunu öğrendim.
Docker Hub'ın hazır image'ların bulunduğu bir platform olduğunu öğrendim.
Port mapping işleminin bilgisayardaki bir portu container içerisindeki bir porta bağladığını öğrendim.
Containerların oluşturulabileceğini, çalıştırılabileceğini, durdurulabileceğini ve silinebileceğini öğrendim.

### Yaptıklarım

`docker run hello-world` komutunu çalıştırarak Docker'ın düzgün çalıştığını kontrol ettim.
`docker run -d -p 8080:80 nginx` komutu ile Nginx web sunucusunu container içerisinde çalıştırdım.
Tarayıcı üzerinden `localhost:8080` adresine giderek Nginx'i görüntüledim.
`docker ps` ile çalışan containerları listeledim.
`docker logs` ile container loglarını inceledim.
`docker stop` ile containerı durdurdum.
`docker rm` ile durdurduğum containerı sildim.
8080 portunun container içerisindeki 80 portuna yönlendirildiğini gözlemledim.

### Docker Komutları

`docker run` → Container oluşturup çalıştırmak için kullanılır.
`docker ps` → Çalışan containerları listeler.
`docker logs` → Container loglarını görüntüler.
`docker stop` → Çalışan containerı durdurur.
`docker rm` → Durdurulmuş containerı siler.
`docker images` → Bilgisayardaki image'ları listeler.
`docker pull` → Docker Hub üzerinden image indirir.

### Kavrama Soruları

**1. Image ile container arasındaki fark nedir?**

Image, container oluşturmak için kullanılan şablondur. Container ise bu image'dan oluşturulan çalışan örnektir.

**2. Docker neden "benim bilgisayarımda çalışıyordu" problemini azaltır?**

Uygulamanın ihtiyaç duyduğu ortam ve bağımlılıkları container içerisinde tuttuğu için farklı bilgisayarlardaki ortam farklılıklarını azaltır.

**3. 8080:80 port eşlemesi ne anlama gelir?**

Bilgisayarımın 8080 portunun container içerisindeki 80 portuna bağlanması anlamına gelir. Böylece `localhost:8080` üzerinden Nginx'e erişilebilir.

### Gün Sonu

Bugün Docker'ın temel çalışma mantığını ve container kullanımını öğrendim. Nginx çalıştırarak port mapping işlemini uyguladım. Temel Docker komutlarını kullanarak container oluşturma, görüntüleme, durdurma ve silme işlemlerini gerçekleştirdim.

## Perşembe — Gün 5/48

### Dockerfile yazmak

**Tarih: /**

### Bugün Öğrendiklerim

Dockerfile'ın Docker image oluşturmak için kullanılan bir dosya olduğunu öğrendim.
`FROM`, `WORKDIR`, `COPY`, `RUN`, `EXPOSE` ve `CMD` komutlarının kullanım amaçlarını öğrendim.
Docker image'larının katmanlardan oluştuğunu ve build önbelleğinin daha önce oluşturulan katmanları tekrar kullanabildiğini öğrendim.
Volume kullanarak verilerin container'ın yaşam döngüsünden bağımsız saklanabileceğini öğrendim.

### Yaptıklarım

Kendimi tanıtan basit bir HTML sayfası hazırladım.
Nginx tabanlı bir Dockerfile oluşturdum.
Hazırladığım HTML dosyasını `COPY` komutu ile Nginx içerisine kopyaladım.
`docker build` komutu ile kendi image'ımı oluşturdum.
`docker images` komutu ile image'ımı kontrol ettim.
`docker run` komutu ile image'ımdan container oluşturup çalıştırdım.
Tarayıcı üzerinden HTML sayfamı görüntüledim.
Dockerfile ve HTML dosyalarımı Git reposuna ekleyerek commit ettim.

### Dockerfile Komutları

`FROM` → Temel image'ı belirler.
`WORKDIR` → Çalışma dizinini belirler.
`COPY` → Dosyaları image içerisine kopyalar.
`RUN` → Image oluşturulurken komut çalıştırır.
`EXPOSE` → Kullanılacak portu belirtir.
`CMD` → Container başlatıldığında çalışacak varsayılan komutu belirler.

### Kavrama Soruları

**1. Dockerfile nedir ve ne amaçla kullanılır?**

Dockerfile, Docker image'ın nasıl oluşturulacağını belirleyen dosyadır. İçerisinde kullanılacak image, dosyalar ve çalıştırılacak komutlar gibi bilgiler bulunur.

**2. `COPY` ile `RUN` arasındaki fark nedir?**

`COPY` dosyaları image içerisine kopyalamak için, `RUN` ise image oluşturulurken komut çalıştırmak için kullanılır.

**3. Container silindiğinde veriler neden kaybolabilir?**

Container içerisindeki veriler container'ın kendi dosya sisteminde tutuluyorsa container silindiğinde bu veriler de kaybolabilir. Kalıcı veriler için volume kullanılabilir.

### Gün Sonu

Bugün Dockerfile kullanarak kendi image'ımı oluşturmayı öğrendim. Temel Dockerfile komutlarını inceledim. Kendimi tanıtan HTML sayfasını Nginx tabanlı bir image içerisinde çalıştırdım. `docker build` ve `docker run` komutlarını kullanarak image oluşturma ve container çalıştırma işlemlerini uyguladım. Ayrıca image katmanları, build önbelleği ve volume hakkında bilgi edindim. Oluşturduğum dosyaları Git reposuna commit ettim.