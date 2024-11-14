namespace Aon.Utilities
{
    public static class RandomNumberTable
    {
        public static int GenerateRandomNumber()
        {
            var seed = (int)DateTime.Now.Ticks & 0x0000FFFF;
            var random = new Random(seed);
            return random.Next(0, 10); // Generates a number between 0 and 9
        }
    }
}
