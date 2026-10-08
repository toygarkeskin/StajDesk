public class AsalSayilar
{
    public static bool AsalMi(int sayi)
    {
        if (sayi < 2)
        {
            return false;
        }

        for (int i = 2; i < sayi; i++)
        {
            if (sayi % i == 0)
            {
                return false;
            }
        }

        return true;
    }

    public static void Calistir()
    {
        Console.WriteLine("=== 1-100 ARASI ASAL SAYILAR ===");

        for (int sayi = 1; sayi <= 100; sayi++)
        {
            if (AsalMi(sayi))
            {
                Console.WriteLine(sayi);
            }
        }
    }
}