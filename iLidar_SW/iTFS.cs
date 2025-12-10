using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Linq;

public class iTFS
{
    // DLL 이름 (Windows: libilidar.dll)
    private const string DLL_NAME = "libilidar";

    // ---------------- [DLL Import 정의] ----------------
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CallbackDelegate(IntPtr ptr);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_init(IntPtr img_ptr, CallbackDelegate callback);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_create(byte[] dest_ip, byte[] src_ip, ushort port);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_destroy();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_connect(byte[] sensor_ip, ushort port);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_disconnect();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_get_params(byte[] buffer);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_set_params(byte[] buffer);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_store();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_lock();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_unlock();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_start();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ilidar_stop();
    // ----------------------------------------------------

    private bool isCreated = false;
    private CallbackDelegate _callbackKeepAlive;

    // 초기화
    public bool Init(IntPtr imgPtr, CallbackDelegate callback)
    {
        Console.WriteLine("Initializing wrapper...");
        _callbackKeepAlive = callback;
        return ilidar_init(imgPtr, callback) == 0;
    }

    // ★★★ [수정됨] 연결 함수 (사용자가 입력한 IP를 최우선으로 사용) ★★★
    public bool Connect(string sensorIpStr, ushort sensorPort)
    {
        // 1. 사용자 입력 IP 파싱
        IPAddress sensorIp;
        if (!IPAddress.TryParse(sensorIpStr, out sensorIp))
        {
            Console.WriteLine($"[Error] 잘못된 IP 형식입니다: {sensorIpStr}");
            return false;
        }

        // 2. 인터페이스가 아직 안 만들어졌다면 생성 시도
        if (!isCreated)
        {
            Console.WriteLine($"[Network] 센서 IP({sensorIpStr})와 통신 가능한 내 PC의 IP를 찾습니다...");

            // 사용자가 입력한 센서 IP와 같은 대역(서브넷)에 있는 내 PC의 랜카드를 찾음
            var hostInfo = FindLocalIpMatchingSensor(sensorIp);

            if (hostInfo != null)
            {
                var hostIp = hostInfo.Value.Ip;
                var subnet = hostInfo.Value.Subnet;
                var broadcastIp = GetBroadcastAddress(hostIp, subnet);

                Console.WriteLine($"  -> 찾음! 내 PC IP: {hostIp}, 서브넷: {subnet}");
                Console.WriteLine("  -> 인터페이스 생성 중...");

                // create(브로드캐스트IP, 내IP, 포트)
                int res = ilidar_create(broadcastIp.GetAddressBytes(), hostIp.GetAddressBytes(), 7256);
                if (res != 0)
                {
                    Console.WriteLine("[Error] 인터페이스 생성 실패 (ilidar_create fail)");
                    return false;
                }
                isCreated = true;
            }
            else
            {
                Console.WriteLine("[Error] 이 컴퓨터에서 센서와 연결할 수 있는 IP를 못 찾았습니다.");
                Console.WriteLine("       (PC와 센서가 같은 네트워크 대역인지 확인하세요)");
                return false;
            }
        }

        // 3. 센서에 연결 시도
        Console.WriteLine($"Connecting to sensor {sensorIpStr}...");
        if (ilidar_connect(sensorIp.GetAddressBytes(), sensorPort) != 0)
        {
            Console.WriteLine("[Error] 연결 실패 (Timeout or Unreachable)");
            return false;
        }

        Console.WriteLine("Connected!");
        return true;
    }

    // ★★★ [핵심] 안전장치가 걸린 설정 전송 함수 ★★★
    public bool SetParams(LiDARParams paramsObj)
    {
        // 1. 가장 위험한 실수 방지: IP가 0.0.0.0인지 검사
        if (IsZeroIp(paramsObj.DataSensorIp))
        {
            // 사용자가 입력한 IP도 아니고, 읽어온 값도 0이면 절대 보내지 않음
            Console.WriteLine("[SAFETY BLOCK] 센서 IP 설정값이 0.0.0.0 입니다! 전송을 차단합니다.");
            return false;
        }

        // 2. 맥 주소 검사
        if (IsZeroMac(paramsObj.DataMacAddr))
        {
            Console.WriteLine("[SAFETY BLOCK] MAC 주소가 비어있습니다! 전송을 차단합니다.");
            return false;
        }

        // 안전하면 전송
        byte[] buffer = paramsObj.Encode();
        if (ilidar_set_params(buffer) == 0)
        {
            Console.WriteLine("[Success] 설정 전송 완료.");
            return true;
        }

        Console.WriteLine("[Fail] 설정 전송 실패.");
        return false;
    }

    public LiDARParams GetParams()
    {
        byte[] buffer = new byte[166];
        if (ilidar_get_params(buffer) != 0) return null;
        return LiDARParams.Decode(buffer);
    }

    // --- 유틸리티 함수들 ---

    public void Store() => ilidar_store();
    public void Unlock() => ilidar_unlock();
    public void Start() => ilidar_start();
    public void Stop() => ilidar_stop();
    public void Disconnect() => ilidar_disconnect();
    public void Destroy() { ilidar_destroy(); isCreated = false; }

    private bool IsZeroIp(byte[] ip)
    {
        return ip == null || (ip[0] == 0 && ip[1] == 0 && ip[2] == 0 && ip[3] == 0);
    }

    private bool IsZeroMac(byte[] mac)
    {
        return mac == null || mac.All(b => b == 0);
    }

    // 사용자가 입력한 센서 IP와 통신 가능한 내 PC의 IP 찾기
    private (IPAddress Ip, IPAddress Subnet)? FindLocalIpMatchingSensor(IPAddress sensorIp)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            var ipProps = nic.GetIPProperties();
            foreach (var uni in ipProps.UnicastAddresses)
            {
                if (uni.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    // 서브넷 마스킹을 통해 같은 네트워크인지 확인
                    if (IsInSameSubnet(uni.Address, sensorIp, uni.IPv4Mask))
                    {
                        return (uni.Address, uni.IPv4Mask);
                    }
                }
            }
        }
        return null;
    }

    private bool IsInSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress subnet)
    {
        byte[] b1 = ip1.GetAddressBytes();
        byte[] b2 = ip2.GetAddressBytes();
        byte[] mask = subnet.GetAddressBytes();
        for (int i = 0; i < 4; i++) if ((b1[i] & mask[i]) != (b2[i] & mask[i])) return false;
        return true;
    }

    private IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        byte[] ip = address.GetAddressBytes();
        byte[] mask = subnetMask.GetAddressBytes();
        byte[] broadcast = new byte[ip.Length];
        for (int i = 0; i < broadcast.Length; i++) broadcast[i] = (byte)(ip[i] | (mask[i] ^ 255));
        return new IPAddress(broadcast);
    }
}