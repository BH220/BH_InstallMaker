using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using BH_Uninstall.ViewModels;

namespace BH_Uninstall.Views
{
    //제거 창. 화면 전환·진입 애니메이션·버튼 스타일은 XAML 바인딩과 트리거가 그린다. 여기에는 창 자체에 관한 처리만 둔다.
    public partial class MainView : Window
    {
        public MainView(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            //뷰모델이 닫기를 요청하면(닫기·취소) 창을 닫는다
            vm.CloseRequested += (_, _) => Close();

            //로그가 추가되면 끝으로 스크롤한다
            ((INotifyCollectionChanged)vm.Logs).CollectionChanged += (_, _) => LogScroll.ScrollToEnd();

            //창이 닫힐 때 뷰모델에 알린다 (제거가 끝났으면 자기 exe 삭제를 예약한다)
            Closing += (_, _) => vm.OnWindowClosing();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
