int sayi1;

Console.Write("Sayı Giriniz: ");

sayi1 = int.Parse(Console.ReadLine()!);

if (sayi1 > 0)
{
    Console.WriteLine("Pozitif bir sayı girdiniz.");
}
else if (sayi1 < 0)
{
    Console.WriteLine("Negatif bir sayı girdiniz.");
}
else
{
    Console.WriteLine("Sıfır girdiniz.");
}