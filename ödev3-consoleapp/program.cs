Console.WriteLine("===== GÜN 8 ÇALIŞMALARI =====");
Console.WriteLine("1 - Not Hesaplayıcı");
Console.WriteLine("2 - Switch");
Console.WriteLine("3 - For");
Console.WriteLine("4 - While");
Console.WriteLine("5 - Foreach");
Console.WriteLine("6 - Sayı Tahmin Oyunu");
Console.WriteLine("7 - Asal Sayılar");
Console.WriteLine("8 - FizzBuzz");
Console.WriteLine("9 - Çıkış");

Console.Write("Seçiminiz: ");

int secim = int.Parse(Console.ReadLine()!);

switch (secim)
{
    case 1:
        NotHesaplayici.Calistir();
        break;

    case 2:
        SwitchOrnekleri.Calistir();
        break;

    case 3:
        ForOrnekleri.Calistir();
        break;

    case 4:
        WhileOrnekleri.Calistir();
        break;

    case 5:
        ForeachOrnekleri.Calistir();
        break;

    case 6:
        SayiTahminOyunu.Calistir();
        break;

    case 7:
        AsalSayilar.Calistir();
        break;

    case 8:
        FizzBuzz.Calistir();
        break;

    case 9:
        Console.WriteLine("Program kapatıldı.");
        break;

    default:
        Console.WriteLine("Geçersiz seçim.");
        break;
}