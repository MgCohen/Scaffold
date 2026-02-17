namespace Utility.Random
{
    using Random = System.Random;
    
    public static class RandomExtensions
    {
        public static int GetRandom(int min, int max)
        {
            if (min > max)
            {
                return max;
            }

            Random random = new Random();
            return random.Next(min, max + 1); // Random value between min and max (inclusive)
        }
        
        public static long GetRandom(long min, long max)
        {
            return GetRandom((int)min, (int)max);
        }
    }
}