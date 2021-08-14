
namespace AJut
{
    using System;

    public static class EnumXT
    {

        public static bool HasPartialFlag(this Enum ThisBitField, Enum test)
        {
            return ( ThisBitField.BoxCast<int>() & test.BoxCast<int>()) != 0;
        }

        public static TEnum SetFlag<TEnum>(this Enum ThisBitField, TEnum test)
        {
            return (ThisBitField.BoxCast<int>() | test.BoxCast<int>()).BoxCast<TEnum>();
        }

        public static TEnum RemoveFlag<TEnum>(this Enum ThisBitField, TEnum test)
        {
            return (ThisBitField.BoxCast<int>() & ~test.BoxCast<int>()).BoxCast<TEnum>();
        }

        public static TEnum ToggleFlag<TEnum>(this Enum ThisBitField, TEnum flag)
        {
            return ThisBitField.HasFlag(flag.BoxCast<Enum>()) ? ThisBitField.RemoveFlag(flag) : ThisBitField.SetFlag(flag);
        }

        public static bool IsBitField<TEnum>()
        {
            return typeof(TEnum).IsTaggedWithAttribute<FlagsAttribute>();
        }
    }
}
