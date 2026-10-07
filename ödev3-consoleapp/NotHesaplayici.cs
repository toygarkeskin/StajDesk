public class NotHesaplayici
{
    public static void Calistir()
    {
        Console.WriteLine("=== NOT HESAPLAYICI ===");

        int note1;

        while (true)
        {
            Console.Write("1. Notu Giriniz: ");
            string input = Console.ReadLine()!;

            if (int.TryParse(input, out note1) && note1 >= 0 && note1 <= 100)
            {
                break;
            }

            Console.WriteLine("Geçersiz bir sayı girdiniz. Lütfen tekrar deneyin.");
        }

        int note2;

        while (true)
        {
            Console.Write("2. Notu Giriniz: ");
            string input = Console.ReadLine()!;

            if (int.TryParse(input, out note2) && note2 >= 0 && note2 <= 100)
            {
                break;
            }

            Console.WriteLine("Geçersiz bir sayı girdiniz. Lütfen tekrar deneyin.");
        }

        int note3;

        while (true)
        {
            Console.Write("3. Notu Giriniz: ");
            string input = Console.ReadLine()!;

            if (int.TryParse(input, out note3) && note3 >= 0 && note3 <= 100)
            {
                break;
            }

            Console.WriteLine("Geçersiz bir sayı girdiniz. Lütfen tekrar deneyin.");
        }

        decimal average = (note1 + note2 + note3) / 3m;

        Console.WriteLine($"Ortalama: {average:F2}");

        string letterGrade;

        if (average >= 90)
            letterGrade = "A";
        else if (average >= 80)
            letterGrade = "B";
        else if (average >= 70)
            letterGrade = "C";
        else if (average >= 60)
            letterGrade = "D";
        else
            letterGrade = "F";

        Console.WriteLine($"Harf Notu: {letterGrade}");
    }
}