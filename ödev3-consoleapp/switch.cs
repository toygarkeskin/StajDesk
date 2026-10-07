public class SwitchOrnekleri
{
    public static void Calistir()
    {
        Console.Write("Bir seçim yapınız: ");
        int secim = int.Parse(Console.ReadLine()!);

        switch (secim)
        {
            case 1:
                Console.WriteLine("Toplama seçildi.");
                break;

            case 2:
                Console.WriteLine("Çıkarma seçildi.");
                break;

            case 3:
                Console.WriteLine("Çarpma seçildi.");
                break;

            case 4:
                Console.WriteLine("Bölme seçildi.");
                break;

            default:
                Console.WriteLine("Geçersiz seçim.");
                break;
        }
    }
}