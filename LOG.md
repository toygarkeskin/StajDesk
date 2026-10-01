# StajDesk Öğrenme Günlüğü

## Çarşamba — Gün 1/48

**Tarih:** **/**

### Bugün Öğrendiklerim

* Stajın amacı ve StajDesk projesi hakkında bilgi edindim.
* 16 haftalık staj yol haritasını inceledim.
* Bir yazılım ekibinin çalışma düzeni hakkında bilgi edindim.
* Jira'nın görev takibi için kullanıldığını öğrendim.
* Git'in sürüm kontrolü için kullanıldığını öğrendim.
* Kod incelemesinin (code review) neden yapıldığını öğrendim.
* VS Code, Git, .NET SDK, Node.js ve Docker Desktop araçlarının kullanım amaçlarını öğrendim.

### Kurulum Kontrolleri

text
dotnet --version → 10.0.401
node -v          → v26.10.0
git --version    → git version 2.55.0.windows.5
docker --version → Docker version 29.8.0, build 88096ef


### Gün Sonu

Gerekli geliştirme araçlarını kurdum ve çalışıp çalışmadıklarını terminal üzerinden kontrol ettim. `stajdesk` klasörünü oluşturdum ve içerisine `LOG.md` dosyasını ekledim.

---

## Perşembe — Gün 2/48

**Tarih:** **/**

### Bugün Öğrendiklerim

* Terminalde `cd`, `ls`, `dir` ve `mkdir` komutlarını öğrendim.
* Göreli ve mutlak dosya yolları arasındaki farkı öğrendim.
* Git'in ne olduğunu ve neden kullanıldığını öğrendim.
* Repository (repo), commit, branch, merge ve remote kavramlarını öğrendim.
* Anlamlı commit mesajlarının nasıl yazılması gerektiğini öğrendim.

### Yaptıklarım

* Şirket Git sunucusunda `stajdesk` reposu oluşturdum.
* Repoyu bilgisayarıma klonladım.
* `README.md` dosyası oluşturdum.
* Kendimi ve projeyi tanıtan bilgiler ekledim.
* En az 5 anlamlı commit oluşturdum.
* Deneme branch'i oluşturdum.
* Branch üzerinde değişiklik yaptım.
* Değişiklikleri ana dala merge ettim.

### Gün Sonu

StajDesk deposunu oluşturdum ve bilgisayarıma klonladım. Git üzerinde commit, branch ve merge işlemlerini uyguladım. Depo bağlantısını mentorla paylaştım.

---

## Cuma — Gün 3/48

**Tarih:** **/**

### Bugün Öğrendiklerim

* Web'in istemci (client) ve sunucu (server) mantığıyla çalıştığını öğrendim.
* Tarayıcının istemci olarak sunucuya istek gönderdiğini öğrendim.
* URL'nin bir web sayfasına veya kaynağa ulaşmak için kullanılan adres olduğunu öğrendim.
* DNS'in alan adlarını IP adresleriyle eşleştirdiğini öğrendim.
* HTTP'nin istemci ve sunucu arasındaki iletişimi sağlamak için kullanıldığını öğrendim.
* GET, POST, PUT ve DELETE HTTP metotlarının ne amaçla kullanıldığını öğrendim.
* HTTP durum kodlarının sunucunun isteğe verdiği sonucu gösterdiğini öğrendim.
* 200, 201, 400, 401, 404 ve 500 durum kodlarının ne anlama geldiğini öğrendim.
* JSON veri formatının istemci ve sunucu arasında veri göndermek için kullanıldığını öğrendim.
* Tarayıcı geliştirici araçlarında bulunan Network sekmesinin web isteklerini incelemek için kullanıldığını öğrendim.

### Yaptıklarım

* Tarayıcı geliştirici araçlarını açtım ve Network sekmesini inceledim.
* 3 farklı web sitesinde yapılan HTTP isteklerini gözlemledim.
* İsteklerin hangi HTTP metodunu kullandığını inceledim.
* İsteklerin durum kodlarını kontrol ettim.
* İstek adreslerini ve URL yapılarını inceledim.
* `jsonplaceholder.typicode.com` üzerinde GET isteği gönderdim.
* GET isteğinden dönen JSON verisini inceledim.
* Postman/Bruno kullanarak POST isteği gönderdim.
* POST isteğinde JSON formatında örnek veri gönderdim.
* HTTP isteği ile HTTP yanıtı arasındaki farkı gözlemledim.

### Network İncelemeleri

| Site    | Metot | Adres | Durum Kodu |
| Google  | GET   |  `https://www.google.com/` | 200 |
| GitHub  | GET   |   `https://github.com/`    | 200 |
|Wikipedia| GET   |`https://www.wikipedia.org/`| 200 |

### Postman / Bruno Çalışması

`jsonplaceholder.typicode.com` üzerinde GET ve POST istekleri gönderdim.

GET isteğinde sunucudan JSON formatında veri aldım.

POST isteğinde ise sunucuya JSON formatında örnek veri gönderdim.

json
{
  "title": "StajDesk",
  "body": "HTTP ve JSON çalışması",
  "userId": 1
}


Gönderdiğim isteğe sunucudan gelen yanıtı inceleyerek JSON verisinin nasıl kullanıldığını gördüm.

### Kavrama Soruları

**1. Tarayıcıya bir adres yazıp Enter'a bastığınızda neler olur?**

Tarayıcıya bir adres yazıp Enter'a bastığımda öncelikle URL işlenir. Alan adının hangi sunucuya ait olduğunu bulmak için DNS kullanılır. Daha sonra tarayıcı sunucuya HTTP veya HTTPS üzerinden bir istek gönderir. Sunucu isteği işleyerek tarayıcıya bir yanıt gönderir. Tarayıcı da gelen verileri işleyerek web sayfasını ekranda gösterir.

**2. Commit ile push arasındaki fark nedir?**

Commit, yaptığım değişiklikleri kendi bilgisayarımdaki Git geçmişine kaydetmektir. Push ise commitlediğim değişiklikleri uzak repository'ye göndermektir. Yani commit değişiklikleri yerel olarak kaydeder, push ise bu değişiklikleri remote repository'ye gönderir.

**3. 404 ile 500 durum kodu arasındaki fark nedir? Hangisi kimin hatasıdır?**

404 durum kodu, istenen kaynağın bulunamadığını gösterir. Örneğin yanlış veya bulunmayan bir URL'ye istek gönderildiğinde 404 alınabilir.

500 durum kodu ise sunucu tarafında beklenmeyen bir hata oluştuğunu gösterir. 404 genellikle istenen kaynağın bulunamamasıyla ilgiliyken, 500 sunucunun isteği işlerken hata yaşadığını gösterir.

### Gün Sonu

Bugün web'in istemci ve sunucu arasındaki çalışma mantığını öğrendim. HTTP metotlarını, durum kodlarını ve JSON veri formatını inceledim. Network sekmesinden farklı web sitelerindeki istekleri gözlemledim. Postman/Bruno kullanarak GET ve POST istekleri gönderdim. Böylece tarayıcıya bir adres yazdığımda arka planda gerçekleşen işlemleri daha iyi anlamaya başladım.
