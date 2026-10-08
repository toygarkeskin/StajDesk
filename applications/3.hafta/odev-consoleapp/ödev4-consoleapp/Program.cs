using System;
using System.Collections.Generic;
using System.Linq;

List<Student> ogrenciler = new List<Student>
{
    new Student { Ad = "Ahmet", Sinif = 9, Not = 65 },
    new Student { Ad = "Mehmet", Sinif = 9, Not = 78 },
    new Student { Ad = "Ayşe", Sinif = 10, Not = 92 },
    new Student { Ad = "Zeynep", Sinif = 10, Not = 85 },
    new Student { Ad = "Can", Sinif = 11, Not = 70 },
    new Student { Ad = "Ece", Sinif = 11, Not = 95 },
    new Student { Ad = "Burak", Sinif = 12, Not = 58 },
    new Student { Ad = "Elif", Sinif = 12, Not = 88 },
    new Student { Ad = "Mert", Sinif = 10, Not = 74 },
    new Student { Ad = "Selin", Sinif = 9, Not = 90 }
};

// 1. Öğrencileri göster
Console.WriteLine("Öğrenci sayısı: " + ogrenciler.Count);

foreach (Student ogrenci in ogrenciler)
{
    Console.WriteLine($"{ogrenci.Ad} - {ogrenci.Sinif}. sınıf - {ogrenci.Not}");
}

// 2. Notu 70 üstü olan öğrenciler
Console.WriteLine("\nNotu 70 üstü öğrenciler:");

var notuYetmisUstu = ogrenciler.Where(o => o.Not > 70);

foreach (Student ogrenci in notuYetmisUstu)
{
    Console.WriteLine($"{ogrenci.Ad} - {ogrenci.Not}");
}

// 3. Sınıfa göre ortalama
Console.WriteLine("\nSınıf ortalamaları:");

var sinifOrtalamalari = ogrenciler.GroupBy(o => o.Sinif);

foreach (var grup in sinifOrtalamalari)
{
    decimal ortalama = grup.Average(o => o.Not);

    Console.WriteLine($"{grup.Key}. sınıf: {ortalama:F2}");
}

// 4. En yüksek not alan öğrenci
Console.WriteLine("\nEn yüksek not alan öğrenci:");

var enYuksek = ogrenciler
    .OrderByDescending(o => o.Not)
    .FirstOrDefault();

if (enYuksek != null)
{
    Console.WriteLine($"{enYuksek.Ad} - {enYuksek.Not}");
}

// Programın hemen kapanmaması için
Console.ReadLine();

// Öğrenci sınıfı
class Student
{
    public string Ad { get; set; } = "";
    public int Sinif { get; set; }
    public decimal Not { get; set; }
}