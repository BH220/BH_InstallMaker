using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace BH_Install.Core
{
    //어셈블리 리소스의 .ico 에서 원하는 크기에 가장 맞는 프레임을 골라 Image 에 쓸 BitmapSource 로 돌려준다.
    //Image.Source 에 .ico 를 바로 주면 첫 프레임(대개 16px)이 쓰여 흐려지므로 직접 고른다.
    public static class IconResourceLoader
    {
        //resourceName: pack://application:,,,/ 뒤에 붙는 경로. 어셈블리를 명시하는 "BH_Install;component/program_icon.ico" 형식을 권한다. 실패하면 null.
        public static BitmapSource? LoadBestFrame(string resourceName, int desiredPixels)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/{resourceName}", UriKind.Absolute);
                using Stream? stream = Application.GetResourceStream(uri)?.Stream;
                if (stream is null) return null;

                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0) return null;

                //원하는 크기 이상 중 가장 작은 것, 없으면 가장 큰 것
                BitmapFrame best = decoder.Frames
                    .Where(f => f.PixelWidth >= desiredPixels)
                    .OrderBy(f => f.PixelWidth)
                    .FirstOrDefault()
                    ?? decoder.Frames.OrderByDescending(f => f.PixelWidth).First();

                return best;
            }
            catch
            {
                return null;
            }
        }
    }
}