using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BH_Install.Core.Helper
{
    public static class ExtensionParser
    {
        public static string ToHashEx(this string filePath)
        {
            if (File.Exists(filePath) == false)
                return string.Empty;
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }

        public static Version ToVersionEx(this string filePath)
        {
            if (File.Exists(filePath) == false)
                return new Version();
            var info = FileVersionInfo.GetVersionInfo(filePath);
            if (Version.TryParse(info.FileVersion, out var version))
                return version;

            return new Version();
        }

        /// <summary>
        /// object 객체를 uint형으로 변환. 변환실패 시 0 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static uint ToUintEx(this object o)
        {
            uint val = 0;

            if (o != null)
            {
                uint.TryParse(o.ToString(), out val);
            }

            return val;
        }

        public static int ToBoolIntEx(this bool val)
        {
            if (val)
                return 1;
            else
                return 0;
        }

        public static string SubstringEx(this object o, int startIdx, int length)
        {
            string result = o.ToStringEx();
            if (result.Length >= (startIdx + length))
                result = result.Substring(startIdx, length);
            else
                result = "";
            return result;
        }

        /// <summary>
        /// object 객체를 int형으로 변환. 변환실패 시 0 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static int ToIntEx(this object o)
        {
            int val = 0;

            if (o != null)
            {
                int.TryParse(o.ToString(), out val);
            }

            return val;
        }

        public static short ToShortEx(this object o)
        {
            short val = 0;

            if (o != null)
            {
                short.TryParse(o.ToString(), out val);
            }

            return val;
        }

        public static string ToDateTimeStringEx(this DateTime dt)
        {
            return string.Create(18, dt, static (span, value) =>
            {
                int y = value.Year, M = value.Month, d = value.Day;
                int h = value.Hour, m = value.Minute, s = value.Second;
                int ms = value.Millisecond;

                span[0] = (char)('0' + (y / 1000) % 10);
                span[1] = (char)('0' + (y / 100) % 10);
                span[2] = (char)('0' + (y / 10) % 10);
                span[3] = (char)('0' + y % 10);
                span[4] = (char)('0' + M / 10);
                span[5] = (char)('0' + M % 10);
                span[6] = (char)('0' + d / 10);
                span[7] = (char)('0' + d % 10);
                span[8] = (char)('0' + h / 10);
                span[9] = (char)('0' + h % 10);
                span[10] = (char)('0' + m / 10);
                span[11] = (char)('0' + m % 10);
                span[12] = (char)('0' + s / 10);
                span[13] = (char)('0' + s % 10);
                span[14] = '.';
                span[15] = (char)('0' + (ms / 100) % 10);
                span[16] = (char)('0' + (ms / 10) % 10);
                span[17] = (char)('0' + ms % 10);
            });
        }

        public static string ToDateStringEx(this DateTime dt)
        {
            return string.Create(14, dt, static (span, value) =>
            {
                var y = value.Year;
                var M = value.Month;
                var d = value.Day;
                var h = value.Hour;
                var m = value.Minute;
                var s = value.Second;

                span[0] = (char)('0' + y / 1000 % 10);
                span[1] = (char)('0' + y / 100 % 10);
                span[2] = (char)('0' + y / 10 % 10);
                span[3] = (char)('0' + y % 10);
                span[4] = (char)('0' + M / 10);
                span[5] = (char)('0' + M % 10);
                span[6] = (char)('0' + d / 10);
                span[7] = (char)('0' + d % 10);
                span[8] = (char)('0' + h / 10);
                span[9] = (char)('0' + h % 10);
                span[10] = (char)('0' + m / 10);
                span[11] = (char)('0' + m % 10);
                span[12] = (char)('0' + s / 10);
                span[13] = (char)('0' + s % 10);
            });
        }

        public static ushort ToUshortEx(this object o)
        {
            ushort val = 0;

            if (o != null)
            {
                ushort.TryParse(o.ToString(), out val);
            }

            return val;
        }

        /// <summary>
        /// object 객체를 long형으로 변환. 변환실패 시 0L 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static long ParseLongEx(this object o)
        {
            long val = 0L;

            if (o != null)
            {
                long.TryParse(o.ToString(), out val);
            }

            return val;
        }

        /// <summary>
        /// object 객체를 double형으로 변환. 변환실패 시 0d 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static double ToDoubleEx(this object o)
        {
            double val = 0d;

            if (o != null)
            {
                double.TryParse(o.ToString(), out val);
            }

            return val;
        }

        /// <summary>
        /// object 객체를 decimal형으로 변환. 변환실패 시 0m 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static decimal ToDecimalEx(this object o)
        {
            decimal val = decimal.Zero;

            if (o != null)
            {
                decimal.TryParse(o.ToString(), out val);
            }

            return val;
        }

        /// <summary>
        /// object 객체를 float형으로 변환. 변환실패 시 0f 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static float ToFloatEx(this object o)
        {
            float val = 0f;

            if (o != null)
            {
                float.TryParse(o.ToString(), out val);
            }

            return val;
        }

        /// <summary>
        /// object 객체를 string형으로 변환. 변환실패 시 string.Empty 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string ToStringEx(this object o)
        {
            string val = string.Empty;

            if (o != null)
            {
                val = o.ToString();
            }

            return val;
        }

        public static string ToJsonValueEx(this string o)
        {
            if (string.IsNullOrEmpty(o))
                return o;

            return o
                .Replace("\\", "\\\\")   // 역슬래시
                .Replace("\"", "\\\"")   // 큰따옴표
                .Replace("\b", "\\b")
                .Replace("\f", "\\f")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// object 객체를 bool형으로 변환. 변환실패 시 false 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static bool ToBoolEx(this object o)
        {
            bool val = false;

            if (o != null)
            {
                bool.TryParse(o.ToString(), out val);
            }

            return val;
        }

        public static object GetDefaultValue(Type t)
        {
            if (t.IsValueType)
            {
                return Activator.CreateInstance(t);
            }
            else
            {
                if (t.Equals(typeof(string)))
                {
                    return string.Empty;
                }
                else
                {
                    return null;
                }
            }
        }


        /// <summary>
        /// object 객체를 uint?형으로 변환. 변환실패 시 null 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static uint? ToUintNullEx(this object o)
        {
            uint? val = null;

            if (o != null)
            {
                uint inVal = 0;
                bool blResult = uint.TryParse(o.ToString(), out inVal);
                if (blResult)
                    val = inVal;
            }

            return val;
        }

        /// <summary>
        /// object 객체를 int?형으로 변환. 변환실패 시 null 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static int? ToIntNullEx(this object o)
        {
            int? val = null;

            if (o != null)
            {
                int inVal = 0;
                bool blResult = int.TryParse(o.ToString(), out inVal);
                if (blResult)
                    val = inVal;
            }

            return val;
        }

        /// <summary>
        /// object 객체를 long형으로 변환. 변환실패 시 null 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static long? ToLongNullEx(this object o)
        {
            long? val = null;

            if (o != null)
            {
                long inVal = 0L;
                bool blResult = long.TryParse(o.ToString(), out inVal);
                if (blResult)
                    val = inVal;
            }

            return val;
        }

        public static ulong ToUlongEx(this object o)
        {
            ulong val = 0;
            if (o != null)
            {
                ulong inVal = 0;
                bool blResult = ulong.TryParse(o.ToString(), out inVal);
                if (blResult)
                    val = inVal;
            }
            return val;
        }

        public static long ToLongEx(this object o)
        {
            long val = 0L;

            if (o != null)
            {
                long inVal = 0L;
                bool blResult = long.TryParse(o.ToString(), out inVal);
                if (blResult)
                    val = inVal;
            }

            return val;
        }

        /// <summary>
        /// object 객체를 decimal형으로 변환. 변환실패 시 null 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static decimal? ToDecimalNullEx(this object o)
        {
            decimal? val = null;

            if (o != null)
            {
                decimal inVal = decimal.Zero;
                bool blResult = decimal.TryParse(o.ToString(), out inVal);
                if (blResult)
                    val = inVal;
            }

            return val;
        }

        /// <summary>
        /// object 객체를 float형으로 변환. 변환실패 시 null 반환
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static float? ToFloatNullEx(this object o)
        {
            float? val = null;

            if (o != null)
            {
                float inVal = 0f;
                bool blResult = float.TryParse(o.ToString(), out inVal);
                if (blResult)
                    val = inVal;
            }

            return val;
        }

        public static char? ToCharNullEx(this object o)
        {
            char? var = null;
            if (o != null)
            {
                var = System.Convert.ToChar(o);
            }
            return var;
        }

        /// <summary>
        /// object 객체를 string형으로 변환. 변환실패 시 null 반환
        /// </summary>
        /// <param name="o"></param>
        /// <param name="inCludeEmpty">문자열이 empty일 경우도 null로 변환 시키려면 true</param>
        /// <returns></returns>
        public static string ToStringNullEx(this object o, bool inCludeEmpty = false)
        {
            string val = null;

            if (o != null)
            {
                val = o.ToString();
            }

            if (o != null && inCludeEmpty && string.IsNullOrEmpty(val))
            {
                val = null;
            }

            return val;
        }

        /// <summary>
        /// object 객체를 DateTime? 형으로 변환
        /// </summary>
        /// <param name="o"></param>
        /// <returns>변환에 성공하면 DateTime 객체를 반환, 변환에 실패하면 null 반환</returns>
        public static DateTime? ToDateTimeEx(this object o)
        {
            DateTime? val = null;

            if (o != null && !System.Convert.IsDBNull(o))
            {
                try
                {
                    string[] allowFomattings = new string[] { "yyyyMMdd", "yyyy-MM-dd", "yyyy-MM-dd tt h:m:s" };
                    System.Globalization.CultureInfo cultureInfo = new System.Globalization.CultureInfo("ko-KR");
                    DateTime ret = DateTime.MinValue;
                    if (DateTime.TryParseExact(o.ToString(), allowFomattings, cultureInfo, System.Globalization.DateTimeStyles.None, out ret))
                    {
                        val = ret;
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return val;
        }

        /// <summary>
        /// object 객체를 DateTime 형으로 변환
        /// </summary>
        /// <param name="o"></param>
        /// <returns>변환에 성공하면 DateTime 객체를 반환, 변환에 실패하면 기본값 반환(0001-01-01 12:00:00)</returns>
        public static DateTime ToDateTimeDefaultEx(this object o)
        {
            DateTime val = DateTime.MinValue;

            if (o != null && !System.Convert.IsDBNull(o))
            {
                try
                {
                    string strValue = o.ToStringEx();
                    if (strValue.Length == 6) strValue += "01";
                    if (strValue.Length == 8)
                    {
                        int temp = 0;
                        if (int.TryParse(strValue, out temp))
                        {
                            val = new DateTime(strValue.SubstringEx(0, 4).ToIntEx(), strValue.SubstringEx(4, 2).ToIntEx(), strValue.SubstringEx(6, 2).ToIntEx());
                            return val;
                        }
                    }

                    object valObj = TypeDescriptor.GetConverter(typeof(DateTime)).ConvertFromString(o.ToStringEx());
                    if (valObj is DateTime)
                    {
                        val = (DateTime)valObj;
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
            }

            return val;
        }

        public static byte[] ToByteArrayEx(this object o)
        {
            string strByte = o.ToStringEx();
            return Enumerable.Range(0, strByte.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(strByte.SubstringEx(x, 2), 16))
                             .ToArray();
        }

        /// <summary>
        /// 파라미터를 숫자로 바꾼 후 #,#처리 하여 반환합니다.
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string ToStringMoneyEx(this object o, bool WithDecimal = false, bool WonDisplay = false)
        {
            string format = "#,#";
            if (WithDecimal)
                format += ".#";
            return o.ToDecimalEx().ToString(format);
        }

        /// <summary>
        /// 실제 파일 크기를 바탕으로 B KB MB GB등으로 변경하여 리턴함
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string GetFileSizeFormat(this object size)
        {
            string[] units = new[] { "Byte", "KB", "MB", "GB" };

            double dSize = size.ToDecimalEx().ToDoubleEx();
            int addIdx = 0;
            while (dSize > 1024)
            {
                dSize = dSize / 1024;
                addIdx++;
            }

            string resultFormat = "#,0";
            if (dSize != (int)dSize)
                resultFormat = resultFormat + "." + (addIdx > 2 ? "##" : "#");
            return string.Format("{0}{1}", dSize.ToString(resultFormat), units[addIdx]);
        }

        /// <summary>
        /// 매개변수로 받은 문자열 중
        /// \ / : * ? " < > |
        /// 문자가 들어오면
        /// _ 로 변환하여 리턴함
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string ToEnableFileNameEx(this string fileName)
        {
            string result = fileName.ToStringEx();

            result = result.Replace("\\", "_")
                .Replace(@"\", "_")
                .Replace("/", "_")
                .Replace(":", "_")
                .Replace("?", "_")
                .Replace("\"", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("|", "_");
            return result;
        }
         

        /// <summary>
        /// 양력을 음력으로 변환
        /// </summary>
        /// <param name="solarDate"></param>
        /// <returns></returns>
        public static DateTime ConvertFromSolarDate(this string solarDate)
        {
            DateTime date = DateTime.Parse(solarDate);
            return ConvertFromSolarDate(date);
        }

        public static string ToRemoveAlphaFromColor(this string argb)
        {
            var str = argb.ToStringEx();

            if (str.StartsWith("#") && str.Length == 9)
            {
                return "#" + str.SubstringEx(3, 6);
            }
            else
                return str;
        }

        /// <summary>
        /// 양력을 음력으로 변환
        /// </summary>
        /// <param name="solarDate"></param>
        /// <returns></returns>
        public static DateTime ConvertFromSolarDate(this DateTime solarDate)
        {
            DateTime leapDate = DateTime.Now;
            KoreanLunisolarCalendar ksc = new KoreanLunisolarCalendar();
            int year = ksc.GetYear(solarDate);
            int month = ksc.GetMonth(solarDate);
            int day = ksc.GetDayOfMonth(solarDate);
            bool isLeapMonth;
            //윤달이 끼어 있으면
            if (ksc.GetMonthsInYear(year) > 12)
            {
                int leapMonth = ksc.GetLeapMonth(year);
                if (month >= leapMonth)
                {
                    isLeapMonth = (month == leapMonth);
                    month--;
                }
            }
            leapDate = new DateTime(year, month, day, 0, 0, 0, 0);
            return leapDate;
        }

        /// <summary>
        /// 음력을 양력으로 변환
        /// </summary>
        /// <param name="lunarDate"></param>
        /// <returns></returns>
        public static DateTime ConvertFromKoreaLunarDate(this string lunarDate)
        {
            DateTime date = DateTime.Parse(lunarDate);
            return ConvertFromKoreaLunarDate(date);
        }

        /// <summary>
        /// 음력을 양력으로 변환
        /// </summary>
        /// <param name="lunarDate"></param>
        /// <returns></returns>
        public static DateTime ConvertFromKoreaLunarDate(this DateTime lunarDate)
        {
            DateTime returnDate = new DateTime();
            KoreanLunisolarCalendar ksc = new KoreanLunisolarCalendar();
            int year = lunarDate.Year;
            int month = lunarDate.Month;
            int day = lunarDate.Day;
            if (ksc.GetMonthsInYear(year) > 12)
            {
                int leapMonth = ksc.GetLeapMonth(year);
                if (month >= leapMonth - 1)
                    returnDate = ksc.ToDateTime(year, month + 1, day, 0, 0, 0, 0);
                else
                    returnDate = ksc.ToDateTime(year, month, day, 0, 0, 0, 0);
            }
            else
                returnDate = ksc.ToDateTime(year, month, day, 0, 0, 0, 0);
            return returnDate;
        }
    }
}
