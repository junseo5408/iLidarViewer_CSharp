using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;

public class iTFS
{
    // DLL 이름 (Windows: .dll 제외, Linux: .so 제외 - 보통 OS가 알아서 처리하거나 설정 필요)
    private const string DLL_NAME = "libilidar";

    // Callback Delegate 정의
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CallbackDelegate(IntPtr ptr);

    // P/Invoke 정의
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

    private bool isCreated = false;

    // 콜백이 GC되지 않도록 멤버 변수로 참조 유지
    private CallbackDelegate _callbackKeepAlive;

    public bool Init(IntPtr imgPtr, CallbackDelegate callback)
    {
        Console.WriteLine("Initializing wrapper class...");
        _callbackKeepAlive = callback; // GC 방지
        int result = ilidar_init(imgPtr, callback);
        if (result == 0)
        {
            Console.WriteLine("  Done.");
            return true;
        }
        Console.WriteLine("Fail to initialize.");
        return false;
    }

    public bool Connect(string sensorIpStr, ushort sensorPort)
    {
        if (!isCreated)
        {
            Console.WriteLine("Auto-creating interface...");
            // 센서 IP와 동일한 서브넷을 가진 호스트 IP 찾기
            var sensorIp = IPAddress.Parse(sensorIpStr);
            var hostIpInfo = GetMatchingHostIp(sensorIp);

            if (hostIpInfo.HasValue)
            {
                var (hostIp, subnetMask) = hostIpInfo.Value;
                var broadcastIp = GetBroadcastAddress(hostIp, subnetMask);

                // create 호출
                int res = ilidar_create(broadcastIp.GetAddressBytes(), hostIp.GetAddressBytes(), 7256);
                if (res != 0)
                {
                    Console.WriteLine("Fail to create interface.");
                    return false;
                }
                isCreated = true;
                Console.WriteLine("  Interface Created.");
            }
            else
            {
                Console.WriteLine("Invalid network setup. No matching adapter found.");
                return false;
            }
        }

        Console.WriteLine("Connecting to sensor...");
        int result = ilidar_connect(IPAddress.Parse(sensorIpStr).GetAddressBytes(), sensorPort);
        if (result != 0)
        {
            Console.WriteLine("Fail to connect.");
            return false;
        }
        Console.WriteLine("  Connected.");
        return true;
    }

    public LiDARParams GetParams()
    {
        byte[] buffer = new byte[166];
        int result = ilidar_get_params(buffer);
        if (result != 0) return null;
        return LiDARParams.Decode(buffer);
    }

    public bool SetParams(LiDARParams paramsObj)
    {
        byte[] buffer = paramsObj.Encode();
        return ilidar_set_params(buffer) == 0;
    }

    public void Store() => ilidar_store();
    public void Unlock() => ilidar_unlock();
    public void Start() => ilidar_start();
    public void Stop() => ilidar_stop();
    public void Disconnect() => ilidar_disconnect();
    public void Destroy()
    {
        ilidar_destroy();
        isCreated = false;
    }

    // --- Network Helpers (ipconfig 대체) ---
    private (IPAddress Ip, IPAddress Subnet)? GetMatchingHostIp(IPAddress targetIp)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;

            var ipProps = nic.GetIPProperties();
            foreach (var uni in ipProps.UnicastAddresses)
            {
                if (uni.Address.AddressFamily == AddressFamily.InterNetwork) // IPv4
                {
                    if (IsInSameSubnet(uni.Address, targetIp, uni.IPv4Mask))
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

        for (int i = 0; i < 4; i++)
        {
            if ((b1[i] & mask[i]) != (b2[i] & mask[i])) return false;
        }
        return true;
    }

    private IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        byte[] ipAdressBytes = address.GetAddressBytes();
        byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

        if (ipAdressBytes.Length != subnetMaskBytes.Length)
            throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

        byte[] broadcastAddress = new byte[ipAdressBytes.Length];
        for (int i = 0; i < broadcastAddress.Length; i++)
        {
            broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
        }
        return new IPAddress(broadcastAddress);
    }
}