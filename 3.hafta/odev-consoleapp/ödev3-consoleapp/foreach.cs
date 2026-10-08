public class ForeachOrnekleri
{
    public static void Calistir()
    {
        Console.WriteLine("=== FOREACH DÖNGÜSÜ ===");

        string[] isimler = { "Ahmet", "Mehmet", "Ayşe", "Zeynep" };

        foreach (string isim in isimler)
        {
            Console.WriteLine(isim);
        }
    }
}