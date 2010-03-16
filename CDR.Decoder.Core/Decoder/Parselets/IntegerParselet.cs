using System;

namespace CDR.Decoder.Parselets
{
    class IntegerParselet : GenericParselet
    {
        public IntegerParselet()
            : base()
        {
            RegisterMethod("Int", ValueAsInteger);
            RegisterMethod("Bin", ValueAsBinary);
            RegisterMethod("Hex", ValueAsHex);
            DefaultValueType = "Int";
        }

        public static int ArrayToInteger(byte[] value)
        {
            int v = 0;
            for (int pos = 0; pos < value.Length; pos++)
            {
                v = v << 8 | value[pos] & 0xFF;
            }
            return v;
        }

        public static string ValueAsInteger(byte[] value)
        {
            if (value == null)
                return String.Empty;
            return ArrayToInteger(value).ToString();
        }


        public static string ValueAsHex(byte[] value)
        {
            return (value == null) ? String.Empty : String.Concat("H'", BitConverter.ToString(value, 0));
        }

        public static string ValueAsBinary(byte[] value)
        {
            string res = String.Empty;

            for (int x = 0; x < value.Length; x++)
            {
                res = String.Concat(
                    res
                    , Convert.ToString(value[x] >> 4, 2).PadLeft(4, '0')
                    , " "
                    , Convert.ToString(value[x] & 0xF, 2).PadLeft(4, '0')
                    , x < value.Length - 1 ? " " : ""
                    );
            }

            return String.Concat("B'", res);
        }
    }
}
