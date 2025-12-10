using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace iLidar_SW
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        // LiDAR 제어 클래스
        private iTFS _lidar;

        // 메모리 관리 (GC 방지용)
        private ushort[] _imgBuffer;
        private GCHandle _imgHandle;
        private IntPtr _imgPtr;
        private iTFS.CallbackDelegate _callbackDelegate; // 콜백 참조 유지

        // 상태 플래그
        private bool _isRunning = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeLidarWrapper();
        }

        private void InitializeLidarWrapper()
        {
            _lidar = new iTFS();

            // 1. 버퍼 할당 (320x320, 16bit)
            _imgBuffer = new ushort[320 * 320];

            // 2. 메모리 고정 (C++ DLL이 안전하게 쓰도록)
            _imgHandle = GCHandle.Alloc(_imgBuffer, GCHandleType.Pinned);
            _imgPtr = _imgHandle.AddrOfPinnedObject();

            // 3. 콜백 정의 (GC되지 않도록 멤버변수에 저장)
            _callbackDelegate = new iTFS.CallbackDelegate(LidarDataCallback);

            // 4. 초기화
            if (!_lidar.Init(_imgPtr, _callbackDelegate))
            {
                MessageBox.Show("LiDAR SDK 초기화 실패! DLL 경로를 확인하세요.");
                Application.Current.Shutdown();
            }
        }

        // --- C++에서 호출되는 콜백 함수 (별도 스레드에서 실행됨) ---
        private void LidarDataCallback(IntPtr ptr)
        {
            if (!_isRunning) return;

            // UI 스레드로 작업 위임 (Dispatcher)
            Dispatcher.Invoke(() =>
            {
                ProcessFrame();
            });
        }

        // --- 영상 처리 및 UI 업데이트 ---
        private Point3DCollection _points = new Point3DCollection();

        private void ProcessFrame()
        {
            try
            {
                // 1. 메모리 주소(_imgPtr)를 OpenCV Mat으로 변환
                using (var fullMat = Mat.FromPixelData(320, 320, MatType.CV_16UC1, _imgPtr))
                {
                    // 상단 160줄 (Depth) 사용
                    var depthRect = new OpenCvSharp.Rect(0, 0, 320, 160);

                    // 2. 2D 이미지 처리 (컬러맵 적용)
                    using (var depthMat = new Mat(fullMat, depthRect))
                    using (var depthNorm = new Mat())
                    using (var colorMat = new Mat())
                    {
                        depthMat.ConvertTo(depthNorm, MatType.CV_8UC1, 255.0 / 8000.0);
                        Cv2.ApplyColorMap(depthNorm, colorMat, ColormapTypes.Jet);

                        // 마스킹 (데이터 없는 곳 검은색 처리)
                        using (var mask = new Mat())
                        {
                            Cv2.Threshold(depthNorm, mask, 1, 255, ThresholdTypes.Binary);
                            using (var resultMat = new Mat())
                            {
                                colorMat.CopyTo(resultMat, mask);
                                // 2D 이미지 UI 업데이트
                                DepthImage.Source = resultMat.ToBitmapSource();
                            }
                        }
                    }
                }

                // 3. 3D 포인트 클라우드 생성 로직
                var newPoints = new Point3DCollection();
                int width = 320;
                int height = 160;
                double fovFactor = 0.003; // 시야각 상수 (조절 가능)

                // _imgBuffer 배열은 이미 C++ DLL이 데이터를 채워놓은 상태이므로 바로 읽습니다.
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        ushort depth = _imgBuffer[index]; // 거리 값 (mm)

                        // 유효 거리 필터링 (10cm ~ 8m)
                        if (depth < 100 || depth > 8000) continue;

                        // 3D 좌표 변환
                        double z = depth / 1000.0; // 미터 단위
                        double x_pos = (x - width / 2.0) * z * fovFactor;
                        double y_pos = (y - height / 2.0) * z * fovFactor;

                        // 리스트에 점 추가 (Y, Z축 방향은 센서 설치 방향에 따라 부호 수정)
                        newPoints.Add(new Point3D(x_pos, -y_pos, -z));
                    }
                }

                // 4. 3D 뷰어 업데이트 (이 부분이 수정되었습니다)
                // Dispatcher를 써서 UI 스레드에서 갱신
                // (참고: 빈번한 업데이트는 성능 저하가 있을 수 있어 추후 최적화 가능)
                PointCloudModel.Points = newPoints;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        // --- 버튼 이벤트 핸들러 ---
        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            string ip = TxtIp.Text;
            ushort port = 7257; // 기본 포트

            // 연결 시도 (UI 멈춤 방지를 위해 비동기처럼 보이지만 여기선 간단히 처리)
            // 실제로는 Task.Run 등을 쓰는 것이 좋음
            bool connected = _lidar.Connect(ip, port);

            if (connected)
            {
                _lidar.Start();
                _isRunning = true;

                TxtStatus.Text = "Connected & Streaming";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
                BtnConnect.IsEnabled = false;
                BtnDisconnect.IsEnabled = true;
                TxtIp.IsEnabled = false;

                // 연결 후 파라미터 읽어오기 예시
                var paramsInfo = _lidar.GetParams();
                if (paramsInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Sensor SN: {paramsInfo.SensorSn}");
                }
            }
            else
            {
                MessageBox.Show("센서 연결 실패. IP와 네트워크 설정을 확인하세요.");
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            StopLidar();

            TxtStatus.Text = "Disconnected";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Gray;
            BtnConnect.IsEnabled = true;
            BtnDisconnect.IsEnabled = false;
            TxtIp.IsEnabled = true;
        }

        private void StopLidar()
        {
            _isRunning = false;
            if (_lidar != null)
            {
                _lidar.Stop();
                _lidar.Disconnect();
            }
        }

        // --- 종료 처리 ---
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopLidar();

            if (_lidar != null)
            {
                _lidar.Destroy();
            }

            // 고정된 메모리 해제
            if (_imgHandle.IsAllocated)
            {
                _imgHandle.Free();
            }
        }
    }
}
