// Tema, sayfa çizilmeden önce uygulanır (beyaz yanıp sönmeyi önler).
// Ayrı dosyada tutulur: Content-Security-Policy satır içi betiklere izin vermez (XSS koruması).
try {
  var t = localStorage.getItem('stajdesk-theme')
  if (t === 'dark' || (!t && matchMedia('(prefers-color-scheme: dark)').matches)) document.documentElement.classList.add('dark')
} catch (e) {}
