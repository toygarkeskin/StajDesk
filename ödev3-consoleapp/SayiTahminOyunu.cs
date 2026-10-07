public class SayiTahminOyunu
{
    public static void Calistir()
    {
        Console.WriteLine("=== SAYI TAHMİN OYUNU ===");

        Random random = new Random();

        int gizliSayi = random.Next(1, 101);
        int denemeSayisi = 0;
        int tahmin;

        while (true)
        {
            Console.Write("1-100 arasında bir sayı tahmin edin: ");

            if (!int.TryParse(Console.ReadLine(), out tahmin))
            {
                Console.WriteLine("Lütfen geçerli bir sayı girin.");
                continue;
            }

            denemeSayisi++;

            if (tahmin < gizliSayi)
            {
                Console.WriteLine("Daha büyük bir sayı girin.");
            }
            else if (tahmin > gizliSayi)
            {
                Console.WriteLine("Daha küçük bir sayı girin.");
            }
            else
            {
                Console.WriteLine($"Tebrikler! {denemeSayisi} denemede bildiniz.");
                break;
            }
        }
    }
}