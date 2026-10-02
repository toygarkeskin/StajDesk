StajDesk Öğrenme Günlüğü

Çarşamba — Gün 1/48
Tarih: /

Bugün Öğrendiklerim
Stajın amacı ve StajDesk projesi hakkında bilgi edindim.
16 haftalık staj yol haritasını inceledim.
Bir yazılım ekibinin çalışma düzeni hakkında bilgi edindim.
Jira'nın görev takibi için kullanıldığını öğrendim.
Git'in sürüm kontrolü için kullanıldığını öğrendim.
Kod incelemesinin (code review) neden yapıldığını öğrendim.
VS Code, Git, .NET SDK, Node.js ve Docker Desktop araçlarının kullanım amaçlarını öğrendim.

Kurulum Kontrolleri
text dotnet --version → 10.0.401
node -v → v26.10.0
git --version → git version 2.55.0.windows.5
docker --version → Docker version 29.8.0, build 88096ef

Gün Sonu
Gerekli geliştirme araçlarını kurdum ve çalışıp çalışmadıklarını terminal üzerinden kontrol ettim. stajdesk klasörünü oluşturdum ve içerisine LOG.md dosyasını ekledim.

Perşembe — Gün 2/48
Tarih: /

Bugün Öğrendiklerim
Terminalde cd, ls, dir ve mkdir komutlarını öğrendim.
Göreli ve mutlak dosya yolları arasındaki farkı öğrendim.
Git'in ne olduğunu ve neden kullanıldığını öğrendim.
Repository (repo), commit, branch, merge ve remote kavramlarını öğrendim.
Anlamlı commit mesajlarının nasıl yazılması gerektiğini öğrendim.

Yaptıklarım
Şirket Git sunucusunda stajdesk reposu oluşturdum.
Repoyu bilgisayarıma klonladım.
README.md dosyası oluşturdum.
Kendimi ve projeyi tanıtan bilgiler ekledim.
En az 5 anlamlı commit oluşturdum.
Deneme branch'i oluşturdum.
Branch üzerinde değişiklik yaptım.
Değişiklikleri ana dala merge ettim.

Gün Sonu
StajDesk deposunu oluşturdum ve bilgisayarıma klonladım. Git üzerinde commit, branch ve merge işlemlerini uyguladım. Depo bağlantısını mentorla paylaştım.

Cuma — Gün 3/48
Tarih: /

Bugün Öğrendiklerim
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

Yaptıklarım
Tarayıcı geliştirici araçlarını açtım ve Network sekmesini inceledim.
3 farklı web sitesinde yapılan HTTP isteklerini gözlemledim.
İsteklerin hangi HTTP metodunu kullandığını inceledim.
İsteklerin durum kodlarını kontrol ettim.
İstek adreslerini ve URL yapılarını inceledim.
jsonplaceholder.typicode.com üzerinde GET isteği gönderdim.
GET isteğinden dönen JSON verisini inceledim.
Postman/Bruno kullanarak POST isteği gönderdim.
POST isteğinde JSON formatında örnek veri gönderdim.
HTTP isteği ile HTTP yanıtı arasındaki farkı gözlemledim.

Network İncelemeleri
| Site | Metot | Adres | Durum Kodu |
| Google | GET | https://www.google.com/ | 200 |
| GitHub | GET | https://github.com/ | 200 |
| Wikipedia | GET | https://www.wikipedia.org/ | 200 |

Postman / Bruno Çalışması
jsonplaceholder.typicode.com üzerinde GET ve POST istekleri gönderdim.

GET isteğinde sunucudan JSON formatında veri aldım.

POST isteğinde ise sunucuya JSON formatında örnek veri gönderdim.

json
{
"title": "StajDesk",
"body": "HTTP ve JSON çalışması",
"userId": 1
}

Gönderdiğim isteğe sunucudan gelen yanıtı inceleyerek JSON verisinin nasıl kullanıldığını gördüm.

Kavrama Soruları

1. Tarayıcıya bir adres yazıp Enter'a bastığınızda neler olur?

Tarayıcıya bir adres yazıp Enter'a bastığımda öncelikle URL işlenir. Alan adının hangi sunucuya ait olduğunu bulmak için DNS kullanılır. Daha sonra tarayıcı sunucuya HTTP veya HTTPS üzerinden bir istek gönderir. Sunucu isteği işleyerek tarayıcıya bir yanıt gönderir. Tarayıcı da gelen verileri işleyerek web sayfasını ekranda gösterir.

2. Commit ile push arasındaki fark nedir?

Commit, yaptığım değişiklikleri kendi bilgisayarımdaki Git geçmişine kaydetmektir. Push ise commitlediğim değişiklikleri uzak repository'ye göndermektir. Yani commit değişiklikleri yerel olarak kaydeder, push ise bu değişiklikleri remote repository'ye gönderir.

3. 404 ile 500 durum kodu arasındaki fark nedir? Hangisi kimin hatasıdır?

404 durum kodu, istenen kaynağın bulunamadığını gösterir. Örneğin yanlış veya bulunmayan bir URL'ye istek gönderildiğinde 404 alınabilir.

500 durum kodu ise sunucu tarafında beklenmeyen bir hata oluştuğunu gösterir. 404 genellikle istenen kaynağın bulunamamasıyla ilgiliyken, 500 sunucunun isteği işlerken hata yaşadığını gösterir.

Gün Sonu
Bugün web'in istemci ve sunucu arasındaki çalışma mantığını öğrendim. HTTP metotlarını, durum kodlarını ve JSON veri formatını inceledim. Network sekmesinden farklı web sitelerindeki istekleri gözlemledim. Postman/Bruno kullanarak GET ve POST istekleri gönderdim. Böylece tarayıcıya bir adres yazdığımda arka planda gerçekleşen işlemleri daha iyi anlamaya başladım.

Hafta 2 | Docker ile Tanışma

Haftanın hedefi: Konteyner mantığını anlamak; image, container, volume ve docker compose kavramlarını uygulamalı öğrenmek.

Çarşamba — Gün 4/48
Konteyner nedir, neden Docker?
Tarih: /

Bugün Öğrendiklerim
Docker'ın uygulamaların farklı bilgisayarlarda aynı şekilde çalışmasını sağlamak için kullanılan bir konteyner teknolojisi olduğunu öğrendim.
"Benim bilgisayarımda çalışıyordu" probleminin uygulamanın çalıştığı ortamın farklı olmasından kaynaklanabileceğini öğrendim.
Docker'ın uygulama ve ihtiyaç duyduğu ortamı konteyner içerisinde çalıştırarak bu problemi azaltmayı amaçladığını öğrendim.
Image ve container arasındaki farkı öğrendim.
Docker image'ın uygulamanın çalışması için gerekli dosya ve yapılandırmaları içeren bir şablon olduğunu öğrendim.
Container'ın bir image kullanılarak oluşturulan ve çalışan örnek olduğunu öğrendim.
Docker Hub'ın hazır Docker image'larının bulunduğu bir platform olduğunu öğrendim.
Port mapping işleminin bilgisayardaki bir portu konteyner içerisindeki bir porta bağlamak için kullanıldığını öğrendim.
Docker containerlarının oluşturulabileceğini, çalıştırılabileceğini, durdurulabileceğini ve silinebileceğini öğrendim.
Docker konteynerlerinin yaşam döngüsü hakkında bilgi edindim.

Yaptıklarım
docker run hello-world komutunu çalıştırdım.
Hello World çıktısını inceleyerek Docker'ın düzgün şekilde çalıştığını kontrol ettim.
docker run -d -p 8080:80 nginx komutunu kullanarak Nginx web sunucusunu Docker container içerisinde çalıştırdım.
Tarayıcı üzerinden localhost:8080 adresine giderek Nginx web sunucusunu görüntüledim.
docker ps komutunu kullanarak çalışan containerları listeledim.
docker logs komutunu kullanarak container loglarını inceledim.
docker stop komutunu kullanarak çalışan containerı durdurdum.
docker rm komutunu kullanarak durdurduğum containerı sildim.
Port mapping sayesinde bilgisayarımdaki 8080 portu üzerinden container içerisindeki 80 portuna erişildiğini gözlemledim.

Docker Komutları
docker run → Yeni bir container oluşturup çalıştırmak için kullanılır.
docker ps → Çalışan containerları listelemek için kullanılır.
docker logs → Container içerisinde oluşan logları görüntülemek için kullanılır.
docker stop → Çalışan bir containerı durdurmak için kullanılır.
docker rm → Durdurulmuş bir containerı silmek için kullanılır.
docker images → Bilgisayarda bulunan Docker image'larını listelemek için kullanılır.
docker pull → Docker Hub üzerinden image indirmek için kullanılır.

Kavrama Soruları

1. Image ile container arasındaki fark nedir?

Image, container oluşturmak için kullanılan hazır bir şablondur. Container ise bu image kullanılarak oluşturulan ve çalışan uygulama ortamıdır. Yani image yapı olarak hazır bir paketken, container bu paketin çalışan halidir.

2. Docker neden "benim bilgisayarımda çalışıyordu" problemini azaltır?

Docker uygulamanın ihtiyaç duyduğu ortamı ve bağımlılıkları container içerisinde çalıştırdığı için farklı bilgisayarlardaki ortam farklılıklarını azaltır. Böylece uygulamanın farklı bilgisayarlarda daha benzer bir ortamda çalışması sağlanabilir.

3. 8080:80 port eşlemesi ne anlama gelir?

8080:80 port eşlemesinde bilgisayarımın 8080 portu container içerisindeki 80 portuna bağlanır. Böylece tarayıcıdan localhost:8080 adresine yapılan istek container içerisindeki 80 portunda çalışan Nginx web sunucusuna ulaşır.

Gün Sonu
Bugün Docker'ın temel çalışma mantığını ve konteyner kavramını öğrendim. Image ile container arasındaki farkı inceledim. Docker Hub'ın ne amaçla kullanıldığını öğrendim. hello-world image'ını çalıştırarak Docker'ın düzgün çalıştığını kontrol ettim. Nginx containerı oluşturarak port mapping işlemini uyguladım. docker ps, docker logs, docker stop ve docker rm komutlarını kullanarak containerların yaşam döngüsünü uygulamalı olarak gözlemledim. Böylece Docker'ın uygulamaları izole bir ortamda çalıştırma mantığını daha iyi anlamaya başladım.
