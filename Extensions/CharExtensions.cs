namespace IotDirector.Extensions
{
    public static class CharExtensions
    {
        public static bool IsLower(this char character)
        {
            return character >= 97 && character <= 122;
        }
        
        public static bool IsUpper(this char character)
        {
            return character >= 65 && character <= 90;
        }

        public static char ToLower(this char character)
        {
            if (character.IsUpper())
                return (char) (character + 32);

            return character;
        }

        public static char ToUpper(this char character)
        {
            if (character.IsLower())
                return (char) (character - 32);

            return character;
        }
    }
}